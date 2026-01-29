using Fusion;
using UnityEngine;

namespace Com.MyCompany.MyGame
{
    public class LobbyPlayerData : NetworkBehaviour
    {
        [Networked] public NetworkBool IsReady { get; set; }

        public override void Spawned()
        {
            // Initial state can be false
            IsReady = false;
            
            // Persist this object across scenes so we can use it to spawn into the game world
            DontDestroyOnLoad(gameObject);
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
                var spawnPos = new Vector3(0, 1, 0); // Basic spawn point
                var playerObj = Runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, Object.InputAuthority);
                
                // Optional: Store connection between LobbyData and GamePlayer?
            }
        }
    }
}
