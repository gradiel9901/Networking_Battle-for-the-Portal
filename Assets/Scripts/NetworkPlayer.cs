using System;
using Fusion;
using Network;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;
using Unity.Cinemachine;
using Com.MyCompany.MyGame;

namespace Network
{
    [RequireComponent(typeof(NetworkTransform))]
    public class NetworkPlayer : NetworkBehaviour
    {
        #region References
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Grounded = Animator.StringToHash("IsGrounded");
        private static readonly int Crouching = Animator.StringToHash("IsCrouching");

        [SerializeField] private Renderer _meshRenderer;
        [SerializeField] private Animator _animator;
        
        // --- PORTED REFERENCES FROM PlayerController ---
        [Header("UI References")]
        [SerializeField] private GameObject deathVfxPrefab;
        [SerializeField] private GameObject graveVisualPrefab;
        
        [Header("Movement Settings")]
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _runSpeed = 7.5f;
        [SerializeField] private float _crouchSpeed = 2.5f;
        [SerializeField] private float _jumpHeight = 1.5f;
        [SerializeField] private float _attackDuration = 1.0f; // Duration of melee attack animation

        [Header("Camera")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private Transform _itemHoldPoint;
        public Transform ItemHoldPoint => _itemHoldPoint;

        [Header("Interaction")]
        [SerializeField] private float _pickupRadius = 1.0f;
        [SerializeField] private float _pickupOffset = 1.5f; // Distance in front of camera
        #endregion

        #region Networked Properties
        [Networked] public NetworkString<_16> PlayerName { get; set; }
        [Networked] public NetworkAnimatorData AnimatorData { get; set; }
        [Networked] public NetworkObject HeldItem { get; set; }
        [Networked] public TickTimer AttackTimer { get; set; }
        
        // --- PORTED PROPERTIES ---
        [Networked] public int CurrentHealth { get; set; }
        [Networked] public int TeamIndex { get; set; }
        [Networked] public NetworkBool HasFinished { get; set; }
        public const int MaxHealth = 100;
        
        // MOVED: Velocity must be networked for prediction to work correctly (reset on rewind)
        [Networked] public float _verticalVelocity { get; set; }
        
        // DEBUG: Visualize Server Position
        [Networked] public Vector3 DebugServerPosition { get; set; }
        #endregion

        #region Local Variables
        private CharacterController _cc;
        private TMP_Text _spawnedNameText;
        private Transform _overheadHpBarTransform;
        private GameObject _spawnedGrave;
        private HPBarController _hpBar;
        private int _lastVisibleHealth;
        
        public static NetworkPlayer Local { get; private set; }
        #endregion

        #region Fusion Callbacks
        public override void Spawned()
        {
            // ... (Keep existing Spawned logic)
            if (HasInputAuthority) // client
            {
                Local = this;
                
                // Camera Setup
                if (_playerCamera != null)
                {
                    _playerCamera.gameObject.SetActive(true);
                    _playerCamera.tag = "MainCamera"; 
                    
                    var listener = _playerCamera.GetComponent<AudioListener>();
                    if (listener != null) listener.enabled = true;
                }
                
                // Standalone / Launcher Name Support
                var launcher = FindFirstObjectByType<FusionLauncher>();
                if (launcher != null) RPC_SetPlayerName(launcher.GetLocalPlayerName());
                else RPC_SetPlayerName($"Player {Id}");
                
                // UI Setup
                _hpBar = FindFirstObjectByType<HPBarController>();
                if (_hpBar != null) _hpBar.UpdateHealth(CurrentHealth, MaxHealth);
                
                // Lock Cursor on Spawn
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else // Remote Proxy
            {
                // Disable Camera & Listener for remote players
                if (_playerCamera != null)
                {
                    _playerCamera.gameObject.SetActive(false);
                    var listener = _playerCamera.GetComponent<AudioListener>();
                    if (listener != null) listener.enabled = false;
                }
                
                // Disable CharacterController on proxies that we don't control
                // Host keeps CC enabled because it has StateAuthority over all players
                if (!HasStateAuthority)
                {
                    var cc = GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                }
            }

            if (HasStateAuthority) // server
            {
                CurrentHealth = MaxHealth; // Reset Health
                AnimatorData = new NetworkAnimatorData(); // Defaults are fine
            }
            
            // Shared Setup (Visuals)
            SetupUI();
            
            _meshRenderer = GetComponentInChildren<Renderer>();
            _animator = GetComponentInChildren<Animator>();

            _lastVisibleHealth = CurrentHealth;
        }


        public override void Despawned(NetworkRunner runner, bool hasState)
        {
             if (Local == this) Local = null;
        }


        public override void FixedUpdateNetwork()
        {
            // Movement Logic
            if (CurrentHealth > 0 && GetInput(out NetworkInputData input))
            {
                // Calculate Move Vector
                Vector3 moveDirection = input.InputVector.normalized;
                
                // Determine Current Speed
                float currentSpeed = _walkSpeed;

                if (input.CrouchInput) 
                {
                    currentSpeed = _crouchSpeed;
                }
                else if (input.SprintInput) 
                {
                    currentSpeed = _runSpeed;
                }

                Vector3 moveVelocity = moveDirection * currentSpeed;

                // Move via CharacterController
                if (_cc == null) _cc = GetComponent<CharacterController>();
                
                bool isGrounded = false;

                if (_cc != null && _cc.enabled)
                {
                    // ALL physics must run together — skip entirely during resimulation
                    if (!Runner.IsResimulation)
                    {
                        // Check Grounded (Robust SphereCast)
                        Vector3 spherePosition = transform.position + Vector3.up * _cc.radius;
                        isGrounded = Physics.SphereCast(spherePosition, _cc.radius * 0.9f, Vector3.down, out RaycastHit hit, _cc.radius + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

                        // Reset velocity if grounded
                        if (isGrounded && _verticalVelocity < 0)
                        {
                            _verticalVelocity = -2f;
                        }

                        // Jump Logic
                        if (input.JumpInput && isGrounded)
                        {
                            _verticalVelocity = Mathf.Sqrt(2f * 9.81f * _jumpHeight);
                        }

                        // Apply Gravity
                        _verticalVelocity -= 9.81f * Runner.DeltaTime;
                        _verticalVelocity = Mathf.Max(_verticalVelocity, -20f);

                        // Move
                        Vector3 finalMove = moveVelocity + Vector3.up * _verticalVelocity;
                        _cc.Move(finalMove * Runner.DeltaTime);
                    }
                }
                else if (!Runner.IsResimulation)
                {
                    transform.position += moveVelocity * Runner.DeltaTime;
                }

                if (input.InteractInput)
                {
                   Debug.Log($"[NetworkPlayer] Interact Input Detected on Tick {Runner.Tick}");
                   
                    // Sphere Overlap for Items
                    if (_playerCamera != null) 
                    {
                        // Use Player Body Transform + Offset
                        Vector3 pickupCenter = transform.position + Vector3.up + (transform.forward * _pickupOffset);
                        
                        // Debugging
                        // Debug.Log($"[NetworkPlayer] Checking Sphere at {pickupCenter} with Radius {_pickupRadius}");

                        Collider[] hits = Physics.OverlapSphere(pickupCenter, _pickupRadius);
                        
                        NetworkItem closestItem = null;
                        float closestDist = float.MaxValue;

                        foreach (var hit in hits)
                        {
                            if (hit.CompareTag("Item"))
                            {
                                var item = hit.GetComponent<NetworkItem>();
                                if (item != null && item.Holder == null) // Only pick up if not held
                                {
                                    float dist = Vector3.Distance(transform.position, hit.transform.position);
                                    if (dist < closestDist)
                                    {
                                        closestItem = item;
                                        closestDist = dist;
                                    }
                                }
                            }
                        }

                        if (closestItem != null)
                        {
                             Debug.Log($"[NetworkPlayer] Picking up Closest Item: {closestItem.name}");
                             closestItem.Pickup(this);
                        }
                        else
                        {
                             // Debug.Log($"[NetworkPlayer] No Items found in range.");
                        }
                    }
                }

                // Attack Logic with Timer
                bool isAttacking = AttackTimer.IsRunning && !AttackTimer.Expired(Runner);

                if (input.AttackInput && HeldItem != null && !isAttacking)
                {
                    AttackTimer = TickTimer.CreateFromSeconds(Runner, _attackDuration);
                    isAttacking = true;
                }

                // Rotation (First Person Look)
                // Yaw (Body)
                transform.rotation = Quaternion.Euler(0, input.LookRotation.x, 0);
                
                // Pitch (Camera) - Only if we have a camera assigned
                if (_playerCamera != null)
                {
                    float pitchAngle = input.LookRotation.y;
                    _playerCamera.transform.localRotation = Quaternion.Euler(pitchAngle, 0, 0);
                    
                    // Debug Pitch
                    // Debug.Log($"[NetworkPlayer] Pitch Input: {pitchAngle}, Camera LocalRot X: {_playerCamera.transform.localRotation.eulerAngles.x}");
                }

                // Movement direction booleans for Animation
                // InputVector is now relative to the look direction 
                bool movingForward = input.InputVector.z > 0.1f;
                bool movingBack = input.InputVector.z < -0.1f;
                bool movingRight = input.InputVector.x > 0.1f;
                bool movingLeft = input.InputVector.x < -0.1f;
                
                // Sync State
                DebugServerPosition = transform.position;
                
                // Update Animator Data
                AnimatorData = new NetworkAnimatorData()
                {
                    Speed = moveVelocity.magnitude, 
                    Jump = input.JumpInput, 
                    Forward = movingForward,
                    Back = movingBack,
                    Left = movingLeft,
                    Right = movingRight,
                    Crouch = input.CrouchInput,
                    IsGrounded = isGrounded, // Synced state
                    IsAttacking = isAttacking // Only attack if timer is running
                };
            }    
        }

        public override void Render()
        {
            // 2. Animation Sync (Legacy / Play Mode)
            if (_animator != null)
            {
                // Priority: Attack > Jump (Airborne) > Move > Idle
                
                if (AnimatorData.IsAttacking)
                {
                    PlayAnimation("Attack_Melee");
                }
                // Use IsGrounded state for Jumping animation logic
                else if (!AnimatorData.IsGrounded)
                {
                    PlayAnimation("Jumping"); 
                }
                else
                {
                    if (AnimatorData.Speed > 0.1f)
                    {
                        if (AnimatorData.Crouch)
                        {
                            PlayAnimation("Crouched_Walking");
                        }
                        // Dynamic threshold for Running: Check if speed is significantly higher than WalkSpeed
                        else if (AnimatorData.Speed > _walkSpeed + 0.5f) 
                        {
                             PlayAnimation("Running");
                        }
                        else
                        {
                             PlayAnimation("Walking");
                        }
                    }
                    else
                    {
                        if (AnimatorData.Crouch) PlayAnimation("Crouched_Idle");
                        else PlayAnimation("Idle");
                    }
                }
            }
            else
            {
                 if (HasInputAuthority) Debug.LogWarning($"[NetworkPlayer] Animator is NULL on {PlayerName}! Assign it in Inspector.");
            }

            // 3. UI Sync (Name & Health)
            if (_spawnedNameText != null) 
            {
                _spawnedNameText.text = PlayerName.ToString();
                // Face Camera
                var cam = Camera.main;
                if (cam != null) _spawnedNameText.transform.rotation = cam.transform.rotation;
            }
            
            if (_overheadHpBarTransform != null)
            {
                float pct = (float)CurrentHealth / MaxHealth;
                _overheadHpBarTransform.localScale = new Vector3(pct, 1, 1);
                var cam = Camera.main;
                if (cam != null) _overheadHpBarTransform.parent.rotation = cam.transform.rotation;
            }
            
            // 4. Local Health UI
            if (HasInputAuthority && _hpBar != null && _lastVisibleHealth != CurrentHealth)
            {
                 _hpBar.UpdateHealth(CurrentHealth, MaxHealth);
            }
            
            // 5. Death Logic
            if (_lastVisibleHealth > 0 && CurrentHealth <= 0) Die();
            if (_lastVisibleHealth <= 0 && CurrentHealth > 0) RespawnVisuals();
            
            _lastVisibleHealth = CurrentHealth;
        }



        private int _spectatingIndex = -1;

        public void SpectateNextPlayer()
        {
            // Allow spectating if dead OR finished
            if (CurrentHealth > 0 && !HasFinished) return;

            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            var otherPlayers = new System.Collections.Generic.List<NetworkPlayer>();

            foreach (var p in allPlayers)
            {
                // Must be a different player
                if (p != this) 
                {
                    otherPlayers.Add(p);
                }
            }

            if (otherPlayers.Count == 0) return;

            // Increment index
            _spectatingIndex = (_spectatingIndex + 1) % otherPlayers.Count;
            var target = otherPlayers[_spectatingIndex];

            // TODO: Implement new Camera Spectating Logic here since virtualCamera/fpsCamera are removed.
            Debug.Log($"Spectating: {target.PlayerName}");
        }

        #endregion

        #region Custom Gameplay (Ported)

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetPlayerName(string name)
        {
            this.PlayerName = name;
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SendChat(string message)
        {
             RPC_BroadcastChat(message);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_BroadcastChat(string message)
        {
            string formattedMessage = $"{PlayerName}: {message}";
            var chat = FindFirstObjectByType<ChatManager>();
            if (chat != null) chat.AddMessageToHistory(formattedMessage);
        }

        private void Die()
        {
            if (deathVfxPrefab != null) Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
            
            if (graveVisualPrefab != null && _spawnedGrave == null)
                _spawnedGrave = Instantiate(graveVisualPrefab, transform.position, transform.rotation);

            if (_meshRenderer != null) _meshRenderer.enabled = false;
            
            if (HasInputAuthority && DeathUIManager.Instance != null) DeathUIManager.Instance.ToggleDeathScreen(true);
        }

        private void RespawnVisuals()
        {
            if (_meshRenderer != null) _meshRenderer.enabled = true;
            if (_spawnedGrave != null) Destroy(_spawnedGrave);
             if (HasInputAuthority && DeathUIManager.Instance != null) DeathUIManager.Instance.ToggleDeathScreen(false);
        }

        private void SetupUI()
        {
            // Name Tag
            GameObject textObj = new GameObject("DynamicNameTag");
            textObj.transform.SetParent(this.transform);
            textObj.transform.localPosition = new Vector3(0, 2.0f, 0); 
            _spawnedNameText = textObj.AddComponent<TextMeshPro>();
            _spawnedNameText.alignment = TextAlignmentOptions.Center;
            _spawnedNameText.fontSize = 4;
            _spawnedNameText.color = Color.white;
            _spawnedNameText.rectTransform.sizeDelta = new Vector2(5, 1);
            
            // Health Bar
            GameObject hpBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hpBg.name = "HealthBarBG";
            hpBg.transform.SetParent(transform);
            hpBg.transform.localPosition = new Vector3(0, 1.7f, 0);
            hpBg.transform.localScale = new Vector3(1.0f, 0.15f, 1f);
            Destroy(hpBg.GetComponent<Collider>());
            hpBg.GetComponent<Renderer>().material.color = Color.black;

            GameObject hpFg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hpFg.name = "HealthBarFG";
            hpFg.transform.SetParent(hpBg.transform);
            hpFg.transform.localPosition = new Vector3(0, 0, -0.01f);
            hpFg.transform.localScale = Vector3.one;
            Destroy(hpFg.GetComponent<Collider>());
            hpFg.GetComponent<Renderer>().material.color = Color.green;
            
            _overheadHpBarTransform = hpFg.transform;
        }

        #endregion
        
        #region Unity Callbacks
        // Debug Input for Self Damage
        private void Update()
        {
            if(!HasInputAuthority) return;
            if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            {
               RPC_TakeDamage(10);
            }
        }
        
        [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
        public void RPC_TakeDamage(int damage)
        {
            CurrentHealth -= damage;
            if (CurrentHealth < 0) CurrentHealth = 0;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetFinished()
        {
            HasFinished = true;
            Debug.Log($"[NetworkPlayer] {PlayerName} has finished the race!");
        }



        private void OnDrawGizmos()
        {
            if (_playerCamera != null)
            {
                Gizmos.color = Color.yellow;
                // Use Player Body Transform + Offset
                Vector3 center = transform.position + Vector3.up + (transform.forward * _pickupOffset);
                Gizmos.DrawWireSphere(center, _pickupRadius);
            }
            
            // Debug Visualization: Server vs Client
            if (Application.isPlaying)
            {
                // Green: Where *I* think I am (Client Prediction)
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
                
                // Red: Where the *Server* says I am
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(DebugServerPosition, 0.6f);
                
                if (HasInputAuthority)
                {
                    // Draw line between them
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(transform.position, DebugServerPosition);
                }
            }
        }
        
        #endregion
        
        private string _lastAnimation;
        private void PlayAnimation(string animName)
        {
            if (_lastAnimation == animName) return;
            _lastAnimation = animName;
            _animator.Play(animName);
            // Debug.Log($"Playing Animation: {animName}");
        }
        
    }
}
