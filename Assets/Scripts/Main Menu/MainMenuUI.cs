using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using System.Linq;

namespace Com.MyCompany.MyGame
{
    public class MainMenuUI : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Menu Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject joinPanel;

        [Header("Main Menu UI")]
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button openJoinPanelButton; 
        [SerializeField] private Button quitButton;

        [Header("Join UI")]
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button backButton;

        [Header("Lobby UI")]
        [SerializeField] private TextMeshProUGUI codeText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Button lobbyPlayButton; // For host to start game potentially
        [SerializeField] private TextMeshProUGUI lobbyPlayButtonText; // To change text between "Play" and "Ready"
        [SerializeField] private Button lobbyCloseButton;
        
        [Header("Prefabs")]
        [SerializeField] private NetworkPrefabRef lobbyPlayerPrefab;

        private NetworkRunner _runner;
        private HashSet<LobbyPlayerData> _spawnedPlayers = new HashSet<LobbyPlayerData>();

        private void Start()
        {
            // Initial State
            mainMenuPanel.SetActive(true);
            lobbyPanel.SetActive(false);
            if (joinPanel != null) joinPanel.SetActive(false);

            // Main Menu Buttons
            createRoomButton.onClick.AddListener(CreateRoom);
            if (openJoinPanelButton != null) openJoinPanelButton.onClick.AddListener(OpenJoinPanel);
            quitButton.onClick.AddListener(QuitGame);
            
            // Join Panel Buttons
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(JoinRoom);
            if (backButton != null) backButton.onClick.AddListener(CloseJoinPanel);

            // Lobby Buttons
            if(lobbyCloseButton != null)
                lobbyCloseButton.onClick.AddListener(CloseLobby);
                
            if (lobbyPlayButton != null)
                lobbyPlayButton.onClick.AddListener(OnLobbyPlayClicked);
        }

        private void CreateRoom()
        {
            string roomCode = GenerateRandomCode(6);
            StartGame(GameMode.Host, roomCode);
        }

        private void OpenJoinPanel()
        {
            mainMenuPanel.SetActive(false);
            if (joinPanel != null) joinPanel.SetActive(true);
            
            // Focus input field if possible
            if (joinCodeInput != null) joinCodeInput.Select();
        }

        private void CloseJoinPanel()
        {
            if (joinPanel != null) joinPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
        }

        private void JoinRoom()
        {
            if (joinCodeInput != null && !string.IsNullOrEmpty(joinCodeInput.text))
            {
                StartGame(GameMode.Client, joinCodeInput.text.ToUpper()); // Ensure uppercase if codes are usually uppercase
            }
            else
            {
                Debug.LogWarning("Join Code is empty!");
            }
        }

        private string GenerateRandomCode(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            System.Random random = new System.Random();
            char[] result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }
            return new string(result);
        }

        private async void StartGame(GameMode mode, string sessionName)
        {
            // Create Runner
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;

            var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (scene.IsValid) {
                sceneInfo.AddSceneRef(scene, LoadSceneMode.Single);
            }

            await _runner.StartGame(new StartGameArgs()
            {
                GameMode = mode,
                SessionName = sessionName,
                Scene = sceneInfo,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
                // We need to disable object initialization if we want to manually spawn for now, 
                // but usually spawning happens after. Let's just pass defaults.
            });
        }

        private void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void CloseLobby()
        {
            if (_runner != null)
            {
                _runner.Shutdown();
            }
            mainMenuPanel.SetActive(true);
            lobbyPanel.SetActive(false);
            if (joinPanel != null) joinPanel.SetActive(false);
            _spawnedPlayers.Clear();
        }
        
        private void OnLobbyPlayClicked()
        {
             if (_runner == null) return;

             if (_runner.IsServer)
             {
                 // Host: Start the actual game level
                 if (CheckAllPlayersReady())
                 {
                     Debug.Log("HOST STARTING GAME!");
                     // Load SampleScene. Assumes it is in Build Settings!
                     // We use SceneUtility to find the index from the path.
                     int buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/SampleScene.unity");
                     
                     if (buildIndex >= 0)
                     {
                        _runner.LoadScene(SceneRef.FromIndex(buildIndex));
                     }
                     else
                     {
                         Debug.LogError("SampleScene not found in Build Settings! Please add 'Assets/Scenes/SampleScene.unity' to Build Settings.");
                     }
                 }
             }
             else
             {
                 // Client: Toggle Ready
                 var localPlayerObj = _runner.GetPlayerObject(_runner.LocalPlayer);
                 if (localPlayerObj != null)
                 {
                     var lobbyData = localPlayerObj.GetComponent<LobbyPlayerData>();
                     if (lobbyData != null)
                     {
                         lobbyData.RPC_SetReady(!lobbyData.IsReady);
                     }
                 }
             }
        }
        
        private bool CheckAllPlayersReady()
        {
            // If solo, we are always ready? Or force ready? Let's say solo host is always ready.
            if (_spawnedPlayers.Count == 0) return true; // Handling edge case

            foreach (var p in _spawnedPlayers)
            {
                if (!p.IsReady) return false;
            }
            return true;
        }

        private void UpdateLobbyUI()
        {
            if (_runner != null && _runner.IsRunning)
            {
                 codeText.text = $"Code: {_runner.SessionInfo.Name}";
                 playerCountText.text = $"Players: {_runner.SessionInfo.PlayerCount}/{_runner.SessionInfo.MaxPlayers}";

                 if (_runner.IsServer)
                 {
                     bool allReady = CheckAllPlayersReady();
                     if (lobbyPlayButtonText != null) lobbyPlayButtonText.text = "PLAY";
                     lobbyPlayButton.interactable = allReady;
                     // Optional: Visual change for Play button enabled/disabled
                     var colors = lobbyPlayButton.colors;
                     colors.normalColor = allReady ? Color.green : Color.gray;
                     lobbyPlayButton.colors = colors;
                 }
                 else
                 {
                     // Client View
                     var localPlayerObj = _runner.GetPlayerObject(_runner.LocalPlayer);
                     bool isReady = false;
                     if (localPlayerObj != null)
                     {
                         var lobbyData = localPlayerObj.GetComponent<LobbyPlayerData>();
                         if (lobbyData != null) isReady = lobbyData.IsReady;
                     }
                     
                     if (lobbyPlayButtonText != null) lobbyPlayButtonText.text = "READY";
                     // Visual Feedback
                     var colors = lobbyPlayButton.colors;
                     colors.normalColor = isReady ? Color.green : Color.white;
                     lobbyPlayButton.colors = colors;
                     
                     lobbyPlayButton.interactable = true;
                 }
            }
        }
        
        private void FixedUpdate()
        {
            // Continuously update UI for readiness changes (could be event based, but polling is simpler here)
            // Check if lobbyPanel still exists (it might be destroyed on scene load)
            if (lobbyPanel != null && lobbyPanel.activeSelf)
            {
                UpdateLobbyUI();
            }
        }

        #region INetworkRunnerCallbacks implementation

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) 
        {
             if (runner == _runner)
            {
                if (runner.IsServer)
                {
                    // Host spawns the lobby data object for the new player
                     Vector3 spawnPos = Vector3.zero;
                     NetworkObject networkPlayerObject = runner.Spawn(lobbyPlayerPrefab, spawnPos, Quaternion.identity, player);
                     
                     // IMPORTANT: Register this object as the player's main object so GetPlayerObject works
                     runner.SetPlayerObject(player, networkPlayerObject);

                     // Keep track of it
                     var lobbyData = networkPlayerObject.GetComponent<LobbyPlayerData>();
                     if (lobbyData != null) 
                     {
                         _spawnedPlayers.Add(lobbyData);
                         
                         // Auto-ready the host so they don't block the game start
                         if (player == runner.LocalPlayer)
                         {
                             lobbyData.IsReady = true;
                         }
                     }
                }
                
                // UI Updates for everyone
                if (player == runner.LocalPlayer)
                {
                    mainMenuPanel.SetActive(false);
                    lobbyPanel.SetActive(true);
                    if (joinPanel != null) joinPanel.SetActive(false);
                }
                UpdateLobbyUI();
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) 
        {
             if (runner == _runner)
            {
                // Remove from list if tracking
                 _spawnedPlayers.RemoveWhere(p => p == null || p.Object == null || p.Object.InputAuthority == player);
                UpdateLobbyUI();
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) 
        {
             if (runner == _runner)
            {
                mainMenuPanel.SetActive(true);
                lobbyPanel.SetActive(false);
                if (joinPanel != null) joinPanel.SetActive(false);
                _spawnedPlayers.Clear();
                
                // Cleanup runner if it wasn't destroyed
                if(_runner != null)
                    Destroy(_runner);
                _runner = null;
            }
        }
        
        // When we receive the spawned object on client/host
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) 
        {
           // Optionally track here if needed for clients to know about other clients
           var lobbyData = obj.GetComponent<LobbyPlayerData>();
           if (lobbyData != null) _spawnedPlayers.Add(lobbyData);
        }
        
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            var lobbyData = obj.GetComponent<LobbyPlayerData>();
            if (lobbyData != null) _spawnedPlayers.Remove(lobbyData);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
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

        #endregion
    }
}
