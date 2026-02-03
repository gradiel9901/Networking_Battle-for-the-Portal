using Fusion;
using UnityEngine;

namespace Com.MyCompany.MyGame
{
    public class LobbyPlayerData : NetworkBehaviour
    {
        [Networked] public NetworkBool IsReady { get; set; }
        [Networked] public NetworkString<_16> PlayerName { get; set; }
        [Networked] public int TeamIndex { get; set; }

        public override void Spawned()
        {
            // Initial state can be false
            IsReady = false;
            
            // Persist this object across scenes so we can use it to spawn into the game world
            DontDestroyOnLoad(gameObject);

            if (Object.HasInputAuthority)
            {
                var launcher = FindFirstObjectByType<FusionLauncher>();
                string myName = launcher != null ? launcher.GetLocalPlayerName() : $"Player {Object.InputAuthority.PlayerId}";
                int myTeam = launcher != null ? launcher.GetLocalPlayerTeamIndex() : 0;
                RPC_SetPlayerName(myName);
                RPC_SetTeam(myTeam);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetPlayerName(string name)
        {
            PlayerName = name;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetTeam(int teamIndex)
        {
            TeamIndex = teamIndex;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(bool ready)
        {
            IsReady = ready;
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestSpawnInGame(NetworkPrefabRef playerPrefab)
        {
            // This is called on the Host by the Client
            if (Runner.IsServer)
            {
                Vector3 spawnPos = new Vector3(0, 1, 0); // Basic spawn point

                // Find Spawn Zones via FusionLauncher
                // Use FindAnyObjectByType to include inactive ones if needed
                var launcher = FindFirstObjectByType<FusionLauncher>(FindObjectsInactive.Include);
                Transform spawnTransform = null;

                if (launcher != null)
                {
                    Debug.Log($"[SPAWN DEBUG] Server processing spawn for Player '{PlayerName}'. TeamIndex: {TeamIndex}");
                    
                    if (TeamIndex == 0) spawnTransform = launcher.GetTeamASpawnPoint();
                    else if (TeamIndex == 1) spawnTransform = launcher.GetTeamBSpawnPoint();
                    
                    if (spawnTransform != null) Debug.Log($"[SPAWN DEBUG] Launcher returned spawn point: {spawnTransform.name}");
                    else Debug.LogWarning($"[SPAWN DEBUG] Launcher returned NULL for Team {TeamIndex}");

                    // CHECK FOR BROKEN/MISSING REFERENCE (Common scene load issue)
                    if (spawnTransform == null)
                    {
                        Debug.LogWarning($"[SPAWN WARNING] FusionLauncher reference for Team {TeamIndex} is NULL! Attempting fallback search...");
                    }
                }
                
                // Fallback Name Search if FusionLauncher didn't help
                if (spawnTransform == null)
                {
                     string targetName = (TeamIndex == 0) ? "Team A Spawn" : "Team B Spawn"; // Try spaced first
                     GameObject foundObj = GameObject.Find(targetName);
                     
                     if (foundObj == null) 
                     {
                         targetName = (TeamIndex == 0) ? "TeamASpawn" : "TeamBSpawn"; // Try no space
                         foundObj = GameObject.Find(targetName);
                     }

                     if (foundObj != null)
                     {
                         spawnTransform = foundObj.transform;
                         Debug.Log($"[SPAWN RECOVERY] Found '{foundObj.name}' by name. Using it.");
                     }
                }

                if (spawnTransform != null) 
                {
                     // Generate random position within local bounds (-0.5 to 0.5 is the standard Cube mesh range)
                     Vector3 randomLocalPos = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
                     // Convert local point to world point (handles Rotation, Scale, and Position)
                     spawnPos = spawnTransform.TransformPoint(randomLocalPos);
                     Debug.Log($"[SPAWN SUCCESS] Spawning Player on Team {TeamIndex} at {spawnPos} (Zone: {spawnTransform.name})");
                }
                else
                {
                     Debug.LogError($"[SPAWN FAILED] Could not find ANY Spawn Zone for Team {TeamIndex}. Spawning at Default.");
                     spawnPos = new Vector3(0, 2, 0); 
                }

                var playerObj = Runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, Object.InputAuthority);
                
                var pc = playerObj.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.PlayerName = PlayerName;
                    pc.TeamIndex = TeamIndex; // Pass the team index to the player controller
                }
            }
        }
    }
}
