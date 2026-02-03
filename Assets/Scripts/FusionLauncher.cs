using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem; 

namespace Com.MyCompany.MyGame
{
    public class FusionLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private TMP_Dropdown colorDropdown;
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject controlPanel;
        [SerializeField] private GameObject progressLabel;

        [Header("Game Settings")]
        [SerializeField] private NetworkPrefabRef playerPrefab; 
        [SerializeField] private Transform teamASpawnPoint;
        [SerializeField] private Transform teamBSpawnPoint;
 
        private NetworkRunner _runner;

        private void Start()
        {
            // Check if a runner already exists (passed from Main Menu)
            var existingRunner = FindFirstObjectByType<NetworkRunner>();
            
            if (existingRunner != null && existingRunner.IsRunning)
            {
                _runner = existingRunner;
                _runner.AddCallbacks(this);
                
                // We are already connected. 
                // Show UI but make the button just "Spawn" us, or Spawn immediately if we don't want to pick details.
                // User said "typed their names", so let's keep the UI but change the button action.
                
                controlPanel.SetActive(true);
                progressLabel.SetActive(false);
                
                // Remove old listener that starts a new game
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(SpawnMyCharacter);
                
                // Change button text if possible to "Spawn"
                var btnText = startButton.GetComponentInChildren<TMP_Text>();
                if (btnText) btnText.text = "SPAWN";
            }
            else
            {
                // Standalone mode (Testing SampleScene directly)
                startButton.onClick.AddListener(StartGame);
                controlPanel.SetActive(true);
                progressLabel.SetActive(false);
            }
        }
        
        private void SpawnMyCharacter()
        {
             if (_runner == null || !_runner.IsRunning) return;
             
             controlPanel.SetActive(false);

             // Find our persistent LobbyPlayerData
             var playerObject = _runner.GetPlayerObject(_runner.LocalPlayer);
             if (playerObject != null)
             {
                 var lobbyData = playerObject.GetComponent<LobbyPlayerData>();
                 if (lobbyData != null)
                 {
                     // Use the existing networked object to request the spawn!
                     lobbyData.RPC_RequestSpawnInGame(playerPrefab);
                     return;
                 }
             }

             // Fallback for Host (Host can always spawn directly)
             if (_runner.IsServer)
             {
                 Vector3 spawnPosition = new Vector3(UnityEngine.Random.Range(-5, 5), 1, UnityEngine.Random.Range(-5, 5));
                 _runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, _runner.LocalPlayer);
             }
             else
             {
                 Debug.LogError("Could not find LobbyPlayerData to request spawn!");
             }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_RequestSpawn(string name, int colorIndex, RpcInfo info = default)
        {
             // Host receives this
             SpawnPlayer(info.Source);
        }

        private void SpawnPlayer(PlayerRef player)
        {
            Vector3 spawnPosition = new Vector3(UnityEngine.Random.Range(-5, 5), 1, UnityEngine.Random.Range(-5, 5));
            NetworkObject networkPlayerObject = _runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
        }

        async void StartGame()
        {
            controlPanel.SetActive(false);
            progressLabel.SetActive(true);
            
            DontDestroyOnLoad(gameObject);

            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;

            var scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(scene, UnityEngine.SceneManagement.LoadSceneMode.Single);
            
            await _runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = "TestRoom",
                Scene = sceneInfo,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });
        }

        #region INetworkRunnerCallbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            // If in standalone mode, spawn immediately
            // If in standalone mode, spawn immediately
            if (runner.IsServer && controlPanel.activeSelf == false) 
            {
                 // STANDALONE MODE SPAWN LOGIC (Host vs World)
                 // 1. Determine Host Team
                 int hostTeamIndex = GetLocalPlayerTeamIndex(); 

                 // 2. Assign Player Team
                 int teamIndex = 0;
                 if (player == runner.LocalPlayer)
                 {
                     teamIndex = hostTeamIndex;
                 }
                 else
                 {
                     // Remote Players get the OPPOSITE team to ensure fighting
                     teamIndex = (hostTeamIndex == 0) ? 1 : 0;
                 }

                 Transform spawnTransform = (teamIndex == 0) ? teamASpawnPoint : teamBSpawnPoint;
                 
                 // Fallback: If Inspector reference is missing, find by name
                 if (spawnTransform == null)
                 {
                     Debug.LogWarning($"[FusionLauncher] Inspector SpawnPoint for Team {teamIndex} is NULL. Searching by name...");
                     string targetName = (teamIndex == 0) ? "TeamASpawn" : "TeamBSpawn";
                     GameObject foundObj = GameObject.Find(targetName);
                     if (foundObj == null) foundObj = GameObject.Find((teamIndex == 0) ? "Team A Spawn" : "Team B Spawn");
                     
                     if (foundObj != null) spawnTransform = foundObj.transform;
                 }

                 Vector3 spawnPosition;
                 if (spawnTransform != null)
                 {
                     Vector3 randomLocalPos = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), 0, UnityEngine.Random.Range(-0.5f, 0.5f));
                     spawnPosition = spawnTransform.TransformPoint(randomLocalPos);
                 }
                 else
                 {
                     spawnPosition = new Vector3(0, 2, 0); 
                     Debug.LogWarning("Spawn Zone NOT FOUND! Spawning at 0,2,0");
                 }

                 Debug.Log($"[Standalone Spawn] Spawning Player {player.PlayerId} (Host? {player == runner.LocalPlayer}) as Team {teamIndex} at {spawnPosition}");
                 runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

        public void OnInput(NetworkRunner runner, NetworkInput input) 
        {
            var data = new NetworkInputData();

            // Check Chat State - Stop movement if chat is open
            var chatMgr = FindFirstObjectByType<ChatManager>();
            if (chatMgr != null && chatMgr.IsChatOpen)
            {
                data.direction = Vector2.zero;
                input.Set(data);
                return;
            }

            float x = 0;
            float y = 0;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) y += 1;
                if (Keyboard.current.sKey.isPressed) y -= 1;
                if (Keyboard.current.aKey.isPressed) x -= 1;
                if (Keyboard.current.dKey.isPressed) x += 1;
                
                data.isInteractPressed = Keyboard.current.fKey.isPressed;
            }

            data.direction = new Vector2(x, y);
            
            if (Camera.main != null)
            {
                data.lookYaw = Camera.main.transform.eulerAngles.y;
            }

            if (x != 0 || y != 0 || data.isInteractPressed) Debug.Log($"Input: Move {x},{y} Interact: {data.isInteractPressed}");

            input.Set(data);
        }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        #endregion

        public string GetLocalPlayerName()
        {
            return nameInputField.text;
        }

        public int GetLocalPlayerTeamIndex()
        {
            // Assuming Dropdown options are: 0: Team A, 1: Team B
            return colorDropdown.value; 
        }

        public Transform GetTeamASpawnPoint()
        {
            return teamASpawnPoint;
        }

        public Transform GetTeamBSpawnPoint()
        {
            return teamBSpawnPoint;
        }
    }
}
