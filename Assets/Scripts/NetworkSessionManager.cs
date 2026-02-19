using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Network
{
    public class NetworkSessionManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        #region Public Variables
        // Singleton
        public static NetworkSessionManager Instance { get; private set; }

        public IReadOnlyList<PlayerRef> JoinedPlayers => _joinedPlayers;
    
        public event Action<PlayerRef> OnPlayerJoinedEvent;
        public event Action<PlayerRef> OnPlayerLeftEvent;
        
        [Header("Input Settings")]
        [SerializeField] public float LookSensitivity = 0.5f;

        // Character Selection
        public int LocalCharacterIndex { get; set; } = 0;
        #endregion
    
        #region Private Variables
        private NetworkRunner _networkRunner;
        private List<PlayerRef> _joinedPlayers = new();
        
        private InputSystem_Actions _inputActions;
        private bool _crouchToggle;
        private float _yaw;
        private float _pitch;
        #endregion

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // Initialize Input Actions
                _inputActions = new InputSystem_Actions();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void OnEnable()
        {
            if (_inputActions != null) _inputActions.Enable();
        }
        
        private void OnDisable()
        {
            if (_inputActions != null) _inputActions.Disable();
        }

        private void Start()
        {
           // No initial lock. Waiting for Player Spawn.
        }

        private void Update()
        {
            // Only handle cursor lock if we have a local player spawned
            if (NetworkPlayer.Local == null) return;

            // Toggle Cursor Lock for debugging
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            
            if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.None)
            {
                 Cursor.lockState = CursorLockMode.Locked;
                 Cursor.visible = false;
            }
        }

        public async void StartGame(GameMode game, byte[] connectionToken = null)
        {
            if (_networkRunner != null) return;

            _networkRunner = gameObject.AddComponent<NetworkRunner>();
            _networkRunner.ProvideInput = true;
        
            var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if(scene.IsValid)
                sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);

            var args = new StartGameArgs()
            {
                GameMode = game,
                SessionName = "TestSession",
                Scene = scene,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
                ConnectionToken = connectionToken
            };

            await _networkRunner.StartGame(args);
            Debug.Log($"[NetworkSessionManager] Game Started in mode: {game}");
        }

        // Helper for GUI buttons
        public void StartHost() => StartGame(GameMode.Host);
        public void StartClient() => StartGame(GameMode.Client);

        #region Used Fusion Callbacks
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_inputActions == null) return;

            var data = new NetworkInputData();

            // 1. Try to read from InputSystem_Actions
            Vector2 move = _inputActions.Player.Move.ReadValue<Vector2>();
            bool jump = _inputActions.Player.Jump.IsPressed();
            bool sprint = _inputActions.Player.Sprint.IsPressed();
            bool crouchTriggered = _inputActions.Player.Crouch.triggered;

            // 2. FALLBACK: Direct Keyboard Check (if Action fails but Keyboard exists)
            // This detects if WASD is pressed but Action returns (0,0)
            if (move == Vector2.zero && Keyboard.current != null)
            {
                var kb = Keyboard.current;
                float x = 0, y = 0;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1;
                
                if (x != 0 || y != 0)
                {
                    move = new Vector2(x, y);
                    Debug.LogWarning($"[NetworkSessionManager] Action 'Move' is ZERO, but Keyboard WASD is pressed! Using Fallback. Check Input Actions Asset.");
                }

                // Fallback for buttons if needed (though user said Jump/Crouch work)
                if (!jump && kb.spaceKey.isPressed) jump = true;
            }

            // MOUSE LOOK
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                 Vector2 mouseDelta = Vector2.zero;
                 if (Mouse.current != null)
                 {
                     mouseDelta = Mouse.current.delta.ReadValue();
                 }
                 
                 // Accumulate Angles
                 _yaw += mouseDelta.x * LookSensitivity; 
                 _pitch -= mouseDelta.y * LookSensitivity;
                 _pitch = Mathf.Clamp(_pitch, -89f, 89f);
                 
                 data.LookRotation = new Vector2(_yaw, _pitch);
            }
            else
            {
                // Maintain current rotation if unlocked
                 data.LookRotation = new Vector2(_yaw, _pitch);
            }

            // Calculate Input Vector relative to Look Rotation YAW
            Quaternion lookRotation = Quaternion.Euler(0, _yaw, 0);
            Vector3 forward = lookRotation * Vector3.forward;
            Vector3 right = lookRotation * Vector3.right;

            data.InputVector = (forward * move.y + right * move.x).normalized;

            // Jump
            data.JumpInput = jump;
            // ... (Rest of Input Handling)

            // Fallback for Sprint (Left Shift)
            if (!sprint && Keyboard.current != null)
            {
                 if (Keyboard.current.leftShiftKey.isPressed) 
                 {
                     sprint = true;
                     // Debug.LogWarning("[NetworkSessionManager] Sprint Action failed, using Fallback LeftShift");
                 }
            }

            // Sprint
            data.SprintInput = sprint;

            // Crouch (Hold Logic)
            data.CrouchInput = _inputActions.Player.Crouch.IsPressed();
            
            // Fallback for Crouch (Left Ctrl)
            if (!data.CrouchInput && Keyboard.current != null)
            {
                if (Keyboard.current.leftCtrlKey.isPressed)
                {
                    data.CrouchInput = true;
                }
            }
            
            // Interact (E Key) - Direct Check for now as it's not in Input Actions yet
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                data.InteractInput = true;
            }

            // Attack (Left Mouse Button)
            if (Mouse.current != null && Mouse.current.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked)
            {
                data.AttackInput = true;
            }

            if (data.SprintInput)
            {
                 Debug.Log($"[NetworkSessionManager] Sprint is TRUE");
            }

            input.Set(data);
        }
    
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NetworkSessionManager] Player Joined: {player}");
            if (!runner.IsServer) return;
            
            _joinedPlayers.Add(player);
            OnPlayerJoinedEvent?.Invoke(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
             Debug.Log($"[NetworkSessionManager] Player Left: {player}");
            if (!runner.IsServer) return;
            
            _joinedPlayers.Remove(player);
            OnPlayerLeftEvent?.Invoke(player);
        }
    
        #endregion

        #region Unused Fusion Callbacks
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        #endregion
    }
}
