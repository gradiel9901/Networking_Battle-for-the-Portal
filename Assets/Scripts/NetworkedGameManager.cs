using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace Network
{
    public class NetworkedGameManager : NetworkBehaviour
    {
        #region Public Variables
        [SerializeField] private NetworkPrefabRef[] _playerPrefabs;
        [SerializeField] private TextMeshProUGUI _playerCountText;
        [SerializeField] private TextMeshProUGUI _timerCountText;


        [Header("Spawn Settings")]
        #endregion
        
        private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new();
        private List<Vector3> _usedSpawnPositions = new();
        private NavMeshTriangulation _navMeshData;
        
        [SerializeField] private int maxPlayers = 1;
        [SerializeField] private float timerBeforeStart = 3.0f;
        private bool hasGameStarted = false;
        
        #region Networked Properties
        [Networked] public TickTimer RoundStartTimer { get; set; }
        #endregion

        public override void Spawned()
        {
            Debug.Log("[NetworkedGameManager] Spawned. Subscribing to Session events.");

            _navMeshData = NavMesh.CalculateTriangulation();
            Debug.Log($"[NetworkedGameManager] NavMesh loaded: {_navMeshData.vertices.Length} vertices, {_navMeshData.indices.Length / 3} triangles");

            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.OnPlayerJoinedEvent += OnPlayerJoined;
                NetworkSessionManager.Instance.OnPlayerLeftEvent += OnPlayerLeft;
             
                if (Runner.IsServer)
                {
                     foreach(var p in NetworkSessionManager.Instance.JoinedPlayers)
                     {
                         if(!_spawnedCharacters.ContainsKey(p)) OnPlayerJoined(p);
                     }
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.OnPlayerJoinedEvent -= OnPlayerJoined;
                NetworkSessionManager.Instance.OnPlayerLeftEvent -= OnPlayerLeft;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (RoundStartTimer.Expired(Runner))
            {
                RoundStartTimer = default;
                OnGameStarted();
            }
        }

        public override void Render()
        {
            if (_playerCountText != null)
                _playerCountText.text = $"Players: {Runner.ActivePlayers.Count()}/{maxPlayers}";
            
            if (_timerCountText != null)
            {
                 if (RoundStartTimer.IsRunning)
                    _timerCountText.text = ((int)RoundStartTimer.RemainingTime(Runner).GetValueOrDefault() + 1).ToString();
                else
                    _timerCountText.text = "";
            }
        }

        private void OnPlayerJoined(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            
            Debug.Log($"[NetworkedGameManager] Handling Join: {player.PlayerId}");

            if (NetworkSessionManager.Instance.JoinedPlayers.Count >= maxPlayers)
            {
                if (!RoundStartTimer.IsRunning && !hasGameStarted)
                {
                     Debug.Log("Starting Timer!");
                     RoundStartTimer = TickTimer.CreateFromSeconds(Runner, timerBeforeStart);
                }
            }
            
            if (hasGameStarted)
            {
                SpawnPlayer(player);
            }
        }
        
        private void OnPlayerLeft(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            
            if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
            {
                Runner.Despawn(networkObject);
                _spawnedCharacters.Remove(player);
            }
        }

        private void OnGameStarted()
        {
            if (hasGameStarted) return; 
            
            Debug.Log("Game Started! Spawning Players and NPCs...");
            hasGameStarted = true;
            _usedSpawnPositions.Clear();
            
            foreach (var playerRef in NetworkSessionManager.Instance.JoinedPlayers)
            {
                SpawnPlayer(playerRef);
            }

        }
        
        private void SpawnPlayer(PlayerRef player)
        {
            if (_spawnedCharacters.ContainsKey(player)) return;

            int characterIndex = 0;
            var token = Runner.GetPlayerConnectionToken(player);
            if (token != null && token.Length == 4)
            {
                 characterIndex = System.BitConverter.ToInt32(token, 0);
                 Debug.Log($"[NetworkedGameManager] Player {player.PlayerId} selected Character Index: {characterIndex}");
            }
            else
            {
                 Debug.LogWarning($"[NetworkedGameManager] No Token found for Player {player.PlayerId}, defaulting to Index 0");
            }

            if (_playerPrefabs == null || _playerPrefabs.Length == 0)
            {
                Debug.LogError("[NetworkedGameManager] No Player Prefabs assigned!");
                return;
            }
            
            if (characterIndex < 0 || characterIndex >= _playerPrefabs.Length) characterIndex = 0;

            Vector3 spawnPos = GetRandomNavMeshPosition(2f);

            var networkObject = Runner.Spawn(_playerPrefabs[characterIndex], spawnPos, Quaternion.identity, player);
            _spawnedCharacters.Add(player, networkObject);
            _usedSpawnPositions.Add(spawnPos);
            
            Debug.Log($"Spawned Character {characterIndex} for Player {player.PlayerId} at {spawnPos}");
        }


        private Vector3 GetRandomPointOnNavMesh()
        {
            if (_navMeshData.indices.Length == 0)
            {
                Debug.LogError("[NetworkedGameManager] NavMesh has no triangles! Bake the NavMesh first.");
                return transform.position + Vector3.up * 2f;
            }

            int triangleCount = _navMeshData.indices.Length / 3;
            int randomTriangle = UnityEngine.Random.Range(0, triangleCount);

            Vector3 v0 = _navMeshData.vertices[_navMeshData.indices[randomTriangle * 3]];
            Vector3 v1 = _navMeshData.vertices[_navMeshData.indices[randomTriangle * 3 + 1]];
            Vector3 v2 = _navMeshData.vertices[_navMeshData.indices[randomTriangle * 3 + 2]];

            float r1 = UnityEngine.Random.value;
            float r2 = UnityEngine.Random.value;

            if (r1 + r2 > 1f)
            {
                r1 = 1f - r1;
                r2 = 1f - r2;
            }

            return v0 + r1 * (v1 - v0) + r2 * (v2 - v0);
        }

        private Vector3 GetRandomNavMeshPosition(float minSeparation = 2f)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                Vector3 candidate = GetRandomPointOnNavMesh();

                bool tooClose = false;
                foreach (var usedPos in _usedSpawnPositions)
                {
                    if (Vector3.Distance(candidate, usedPos) < minSeparation)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    return candidate + Vector3.up * 0.5f;
                }
            }

            Debug.LogWarning("[NetworkedGameManager] Could not find position with enough separation, using best random.");
            return GetRandomPointOnNavMesh() + Vector3.up * 0.5f;
        }


    }
}