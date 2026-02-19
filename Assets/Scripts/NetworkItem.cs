using Fusion;
using UnityEngine;

namespace Network
{
    [RequireComponent(typeof(NetworkTransform))]
    public class NetworkItem : NetworkBehaviour
    {
        [SerializeField] private float _rotationSpeed = 50f;
        [SerializeField] private Vector3 _heldRotationOffset = Vector3.zero; // Rotation offset when held
        [SerializeField] private Vector3 _heldPositionOffset = Vector3.zero; // Position offset when held
        
        [Networked] public NetworkObject Holder { get; set; }

        public override void FixedUpdateNetwork()
        {
            if (Holder != null)
            {
                // Being Held: Sync to Holder's ItemHoldPoint
                var holderPlayer = Holder.GetComponent<NetworkPlayer>();
                if (holderPlayer != null && holderPlayer.ItemHoldPoint != null)
                {
                    // Apply Position Offset (Relative to Hold Point Rotation)
                    transform.position = holderPlayer.ItemHoldPoint.position + (holderPlayer.ItemHoldPoint.rotation * _heldPositionOffset);
                    
                    // Apply Rotation Offset
                    transform.rotation = holderPlayer.ItemHoldPoint.rotation * Quaternion.Euler(_heldRotationOffset);
                }
            }
            else
            {
                // Floating: Simple rotation effect
                transform.Rotate(Vector3.up * _rotationSpeed * Runner.DeltaTime);
            }
        }

        public void Pickup(NetworkPlayer player)
        {
            if (!HasStateAuthority) return;
            
            Debug.Log($"[NetworkItem] Picked up by {player.PlayerName}");
            Holder = player.Object;
            player.HeldItem = Object;
            
            // Disable ALL colliders on item and children to prevent pushing the player
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
            
            // If using Rigidbody, set to Kinematic
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            // Immediately move to hold point to prevent overlap with CharacterController
            if (player.ItemHoldPoint != null)
            {
                transform.position = player.ItemHoldPoint.position + (player.ItemHoldPoint.rotation * _heldPositionOffset);
                transform.rotation = player.ItemHoldPoint.rotation * Quaternion.Euler(_heldRotationOffset);
            }
        }
    }
}
