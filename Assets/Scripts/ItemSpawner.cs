using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Network
{
    public class ItemSpawner : NetworkBehaviour
    {
        [Header("Spawner Settings")]
        [SerializeField] private NetworkPrefabRef _itemPrefab;
        [SerializeField] private float _spawnInterval = 5f;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private Vector2 _spawnAreaSize = new Vector2(10, 10); // X and Z Size
        [SerializeField] private Collider _groundCollider; // Optional: Use this collider's bounds
        [SerializeField] private Vector3 _spawnRotationEstimate = new Vector3(-90, 0, 0); // Default to standing up

        private float _spawnTimer;

        public override void Spawned()
        {
            // Reset timer on spawn
            _spawnTimer = 0;
        }

        public override void FixedUpdateNetwork()
        {
            // Only Server Spawns Items
            if (!Object.HasStateAuthority) return;

            _spawnTimer += Runner.DeltaTime;
            if (_spawnTimer >= _spawnInterval)
            {
                _spawnTimer = 0;
                SpawnItem();
            }
        }

        private void SpawnItem()
        {
            Debug.Log($"[ItemSpawner] Spawner {Id} IS Spawning Item...");
            Quaternion rotation = Quaternion.Euler(_spawnRotationEstimate);

            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                // Random Point from defined list
                int index = UnityEngine.Random.Range(0, _spawnPoints.Length);
                Runner.Spawn(_itemPrefab, _spawnPoints[index].position, rotation);
            }
            else if (_groundCollider != null)
            {
                 // Random point within Ground Collider Bounds
                 Vector3 spawnPos = GetRandomPositionOnCollider();
                 if (spawnPos != Vector3.zero)
                 {
                     Runner.Spawn(_itemPrefab, spawnPos, rotation);
                 }
            }
            else
            {
                // Random Ground Spawn (Fallback)
                Vector3 spawnPos = GetRandomGroundPosition();
                if (spawnPos != Vector3.zero)
                {
                    Runner.Spawn(_itemPrefab, spawnPos, rotation);
                }
            }
        }

        private Vector3 GetRandomPositionOnCollider()
        {
            Bounds bounds = _groundCollider.bounds;
            
            // Try a few times to find a valid spot
            for (int i = 0; i < 10; i++)
            {
                float randomX = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
                float randomZ = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
                
                // Raycast down from top of bounds to find the surface
                Vector3 origin = new Vector3(randomX, bounds.max.y + 1f, randomZ);
                
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, bounds.size.y + 2f))
                {
                    // Ensure we hit the specific ground collider
                    if (hit.collider == _groundCollider)
                    {
                        return hit.point + Vector3.up * 0.5f;
                    }
                }
            }
            
            return Vector3.zero;
        }

        private Vector3 GetRandomGroundPosition()
        {
            // Generate Random Offset
            float randomX = UnityEngine.Random.Range(-_spawnAreaSize.x / 2, _spawnAreaSize.x / 2);
            float randomZ = UnityEngine.Random.Range(-_spawnAreaSize.y / 2, _spawnAreaSize.y / 2);
            
            // Start from high up relative to the spawner's position
            Vector3 originInfo = transform.position + new Vector3(randomX, 10f, randomZ); 
            
            // Raycast Down to find Ground
            if (Physics.Raycast(originInfo, Vector3.down, out RaycastHit hit, 50f)) 
            {
                return hit.point + Vector3.up * 0.5f; // Slight offset to prevent clipping
            }
            
            return Vector3.zero; // Failed to find ground
        }
    }
}
