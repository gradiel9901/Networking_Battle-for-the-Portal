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

[Header("UI References")]
        [SerializeField] private GameObject deathVfxPrefab;
        [SerializeField] private GameObject graveVisualPrefab;
        
        [Header("Movement Settings")]
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _runSpeed = 7.5f;
        [SerializeField] private float _crouchSpeed = 2.5f;
        [SerializeField] private float _jumpHeight = 1.5f;
        [SerializeField] private float _attackDuration = 1.0f;
        [SerializeField] private float _respawnDelay = 10f;
        [SerializeField] private float _respawnHeightOffset = 3f;
        [SerializeField] private int _playerAttackDamage = 100;
        [SerializeField] private float _npcStunDuration = 3f;

        [Header("Camera")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private Transform _itemHoldPoint;
        public Transform ItemHoldPoint => _itemHoldPoint;

        [Header("Interaction")]
        [SerializeField] private float _pickupRadius = 1.0f;
        [SerializeField] private float _pickupOffset = 1.5f;
        #endregion

        #region Networked Properties
        [Networked] public NetworkString<_16> PlayerName { get; set; }
        [Networked] public NetworkAnimatorData AnimatorData { get; set; }
        [Networked] public NetworkObject HeldItem { get; set; }
        [Networked] public TickTimer AttackTimer { get; set; }

[Networked] public int CurrentHealth { get; set; }
        [Networked] public int TeamIndex { get; set; }
        [Networked] public NetworkBool HasFinished { get; set; }
        [Networked] public TickTimer RespawnTimer { get; set; }
        public const int MaxHealth = 100;

[Networked] public float _verticalVelocity { get; set; }

[Networked] public Vector3 DebugServerPosition { get; set; }
        #endregion

        #region Local Variables
        private CharacterController _cc;
        private Vector3 _spawnPosition;
        private Camera _cachedMainCamera;
        private string _cachedPlayerName;
        private TMP_Text _spawnedNameText;
        private Transform _overheadHpBarTransform;
        private GameObject _spawnedGrave;
        private int _lastVisibleHealth;
        
        private bool _wasGrounded = true;
        private bool _wasAttacking = false;

        public static NetworkPlayer Local { get; private set; }
        #endregion

        #region Fusion Callbacks
        public override void Spawned()
        {

            if (HasInputAuthority)
            {
                Local = this;

if (_playerCamera != null)
                {
                    _playerCamera.gameObject.SetActive(true);
                    _playerCamera.tag = "MainCamera"; 
                    
                    var listener = _playerCamera.GetComponent<AudioListener>();
                    if (listener != null) listener.enabled = true;
                }

var launcher = FindFirstObjectByType<FusionLauncher>();
                if (launcher != null) RPC_SetPlayerName(launcher.GetLocalPlayerName());
                else RPC_SetPlayerName($"Player {Id}");

if (StaminaController.Instance != null)
                    StaminaController.Instance.UpdateHealth(CurrentHealth, MaxHealth);

Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {

                if (_playerCamera != null)
                {
                    _playerCamera.gameObject.SetActive(false);
                    var listener = _playerCamera.GetComponent<AudioListener>();
                    if (listener != null) listener.enabled = false;
                }

if (!HasStateAuthority)
                {
                    var cc = GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                }
            }

            if (HasStateAuthority)
            {
                CurrentHealth = MaxHealth;
                AnimatorData = new NetworkAnimatorData();
            }

SetupUI();
            
            _meshRenderer = GetComponentInChildren<Renderer>();
            _animator = GetComponentInChildren<Animator>();

            _lastVisibleHealth = CurrentHealth;

_spawnPosition = transform.position;
        }

public override void Despawned(NetworkRunner runner, bool hasState)
        {
             if (Local == this) Local = null;
        }

public override void FixedUpdateNetwork()
        {

            if (HasStateAuthority && CurrentHealth <= 0 && RespawnTimer.Expired(Runner))
            {
                RespawnPlayer();
            }

if (CurrentHealth > 0 && GetInput(out NetworkInputData input))
            {

                Vector3 moveDirection = input.InputVector.normalized;

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

if (_cc == null) _cc = GetComponent<CharacterController>();
                
                bool isGrounded = false;

                if (_cc != null && _cc.enabled)
                {

                    if (!Runner.IsResimulation)
                    {

                        Vector3 spherePosition = transform.position + Vector3.up * _cc.radius;
                        isGrounded = Physics.SphereCast(spherePosition, _cc.radius * 0.9f, Vector3.down, out RaycastHit hit, _cc.radius + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

if (isGrounded && _verticalVelocity < 0)
                        {
                            _verticalVelocity = -2f;
                        }

if (input.JumpInput && isGrounded)
                        {
                            _verticalVelocity = Mathf.Sqrt(2f * 9.81f * _jumpHeight);
                        }

_verticalVelocity -= 9.81f * Runner.DeltaTime;
                        _verticalVelocity = Mathf.Max(_verticalVelocity, -20f);

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

                    if (_playerCamera != null) 
                    {

                        Vector3 pickupCenter = transform.position + Vector3.up + (transform.forward * _pickupOffset);

Collider[] hits = Physics.OverlapSphere(pickupCenter, _pickupRadius, ~0, QueryTriggerInteraction.Collide);
                        
                        NetworkItem closestItem = null;
                        float closestDist = float.MaxValue;

                        foreach (var hit in hits)
                        {
                            if (hit.CompareTag("Item"))
                            {
                                var item = hit.GetComponent<NetworkItem>();
                                if (item != null && item.Holder == null)
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
                            closestItem.Pickup(this);
                    }
                }

bool isAttacking = AttackTimer.IsRunning && !AttackTimer.Expired(Runner);

                if (input.AttackInput && HeldItem != null && !isAttacking)
                {
                    AttackTimer = TickTimer.CreateFromSeconds(Runner, _attackDuration);
                    isAttacking = true;

if (HasStateAuthority)
                    {
                        Vector3 hitCenter = transform.position + Vector3.up + (transform.forward * _pickupOffset);
                        Collider[] hitColliders = Physics.OverlapSphere(hitCenter, _pickupRadius);

                        NetworkPlayer closestPlayer = null;
                        NetworkNPC closestNPC = null;
                        float closestDist = float.MaxValue;

                        foreach (var col in hitColliders)
                        {
                            float dist = Vector3.Distance(transform.position, col.transform.position);
                            if (dist >= closestDist) continue;

                            var tp = col.GetComponentInParent<NetworkPlayer>();
                            if (tp != null && tp != this)
                            {
                                closestPlayer = tp;
                                closestNPC = null;
                                closestDist = dist;
                                continue;
                            }

                            var npc = col.GetComponentInParent<NetworkNPC>();
                            if (npc != null)
                            {
                                closestNPC = npc;
                                closestPlayer = null;
                                closestDist = dist;
                            }
                        }

                        if (closestPlayer != null)
                        {
                            Debug.Log($"[NetworkPlayer] {PlayerName} hit Player: {closestPlayer.PlayerName}");
                            closestPlayer.RPC_TakeDamage(_playerAttackDamage);
                        }
                        else if (closestNPC != null)
                        {
                            Debug.Log($"[NetworkPlayer] {PlayerName} stunned an NPC!");
                            closestNPC.Stun(_npcStunDuration);
                        }
                    }
                }

transform.rotation = Quaternion.Euler(0, input.LookRotation.x, 0);

if (_playerCamera != null)
                {
                    float pitchAngle = input.LookRotation.y;
                    _playerCamera.transform.localRotation = Quaternion.Euler(pitchAngle, 0, 0);

}

bool movingForward = input.InputVector.z > 0.1f;
                bool movingBack = input.InputVector.z < -0.1f;
                bool movingRight = input.InputVector.x > 0.1f;
                bool movingLeft = input.InputVector.x < -0.1f;

DebugServerPosition = transform.position;

AnimatorData = new NetworkAnimatorData()
                {
                    Speed = moveVelocity.magnitude, 
                    Jump = input.JumpInput, 
                    Forward = movingForward,
                    Back = movingBack,
                    Left = movingLeft,
                    Right = movingRight,
                    Crouch = input.CrouchInput,
                    IsGrounded = isGrounded,
                    IsAttacking = isAttacking
                };
            }    
        }

        public override void Render()
        {

            if (_animator != null)
            {

if (AnimatorData.IsAttacking)
                {
                    PlayAnimation("Attack_Melee");
                }

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

            // --- Sound Manager Integration ---
            if (HasInputAuthority && Com.MyCompany.MyGame.SoundManager.Instance != null)
            {
                // Walk vs Run evaluates actual magnitude of inputs/velocity from animator data
                bool isMoving = AnimatorData.Speed > 0.1f;
                bool isRunning = isMoving && AnimatorData.Speed > (_walkSpeed + 0.1f); // Lower threshold to ensure run registers
                bool isWalking = isMoving && !isRunning;
                
                // Set movement sound loop (walk/run) or stop it if idle
                Com.MyCompany.MyGame.SoundManager.Instance.SetPlayerMovementSound(isWalking, isRunning);

                // Play jump sound exactly when we transition to the jump state
                if (!AnimatorData.IsGrounded && _wasGrounded)
                {
                     Com.MyCompany.MyGame.SoundManager.Instance.PlaySFX(Com.MyCompany.MyGame.SoundManager.Instance.JumpClip);
                }

                // Play attack sound exactly when we transition to the attack state
                if (AnimatorData.IsAttacking && !_wasAttacking)
                {
                     Com.MyCompany.MyGame.SoundManager.Instance.PlaySFX(Com.MyCompany.MyGame.SoundManager.Instance.AttackClip);
                }

                _wasGrounded = AnimatorData.IsGrounded;
                _wasAttacking = AnimatorData.IsAttacking;
            }
            // ---------------------------------

if (_cachedMainCamera == null) _cachedMainCamera = Camera.main;

            if (_spawnedNameText != null)
            {

                string currentName = PlayerName.ToString();
                if (currentName != _cachedPlayerName)
                {
                    _cachedPlayerName = currentName;
                    _spawnedNameText.text = currentName;
                }
                if (_cachedMainCamera != null)
                    _spawnedNameText.transform.rotation = _cachedMainCamera.transform.rotation;
            }

            if (_overheadHpBarTransform != null)
            {

                if (_lastVisibleHealth != CurrentHealth)
                {
                    float pct = (float)CurrentHealth / MaxHealth;
                    _overheadHpBarTransform.localScale = new Vector3(pct, 1f, 1f);
                }
                if (_cachedMainCamera != null)
                    _overheadHpBarTransform.parent.rotation = _cachedMainCamera.transform.rotation;
            }

if (HasInputAuthority && _lastVisibleHealth != CurrentHealth
                && StaminaController.Instance != null)
            {
                StaminaController.Instance.UpdateHealth(CurrentHealth, MaxHealth);
            }

if (_lastVisibleHealth > 0 && CurrentHealth <= 0) Die();
            if (_lastVisibleHealth <= 0 && CurrentHealth > 0) RespawnVisuals();

if (HasInputAuthority && CurrentHealth <= 0 && DeathUIManager.Instance != null)
            {
                int remaining = Mathf.CeilToInt(RespawnTimer.RemainingTime(Runner) ?? 0f);
                DeathUIManager.Instance.UpdateCountdown(remaining);
            }
            
            _lastVisibleHealth = CurrentHealth;
        }

private int _spectatingIndex = -1;

        public void SpectateNextPlayer()
        {

            if (CurrentHealth > 0 && !HasFinished) return;

            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            var otherPlayers = new System.Collections.Generic.List<NetworkPlayer>();

            foreach (var p in allPlayers)
            {

                if (p != this) 
                {
                    otherPlayers.Add(p);
                }
            }

            if (otherPlayers.Count == 0) return;

_spectatingIndex = (_spectatingIndex + 1) % otherPlayers.Count;
            var target = otherPlayers[_spectatingIndex];

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

            GameObject textObj = new GameObject("DynamicNameTag");
            textObj.transform.SetParent(this.transform);
            textObj.transform.localPosition = new Vector3(0, 2.0f, 0); 
            _spawnedNameText = textObj.AddComponent<TextMeshPro>();
            _spawnedNameText.alignment = TextAlignmentOptions.Center;
            _spawnedNameText.fontSize = 4;
            _spawnedNameText.color = Color.white;
            _spawnedNameText.rectTransform.sizeDelta = new Vector2(5, 1);

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

if (CurrentHealth <= 0 && !RespawnTimer.IsRunning)
                RespawnTimer = TickTimer.CreateFromSeconds(Runner, _respawnDelay);
        }

        private void RespawnPlayer()
        {
            CurrentHealth = MaxHealth;
            RespawnTimer = default;
            _verticalVelocity = 0f;

if (_cc == null) _cc = GetComponent<CharacterController>();
            if (_cc != null)
            {
                _cc.enabled = false;
                transform.position = _spawnPosition + Vector3.up * _respawnHeightOffset;
                _cc.enabled = true;
            }
            else
            {
                transform.position = _spawnPosition + Vector3.up * _respawnHeightOffset;
            }

            Debug.Log($"[NetworkPlayer] {PlayerName} respawned!");
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

                Vector3 center = transform.position + Vector3.up + (transform.forward * _pickupOffset);
                Gizmos.DrawWireSphere(center, _pickupRadius);
            }

if (Application.isPlaying)
            {

                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, 0.5f);

Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(DebugServerPosition, 0.6f);
                
                if (HasInputAuthority)
                {

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

        }
        
    }
}
