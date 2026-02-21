using Fusion;
using UnityEngine;

namespace Network
{
    [RequireComponent(typeof(NetworkTransform))]
    // a pickup item that spins when idle and follows the holder's hand when picked up
    public class NetworkItem : NetworkBehaviour
    {
        [SerializeField] private float _rotationSpeed = 50f;
        [SerializeField] private Vector3 _heldRotationOffset = Vector3.zero;   // tweak in inspector
        [SerializeField] private Vector3 _heldPositionOffset = Vector3.zero;

        // [Networked] so all clients know who's holding this
        [Networked] public NetworkObject Holder { get; set; }

        public override void Spawned()
        {
            // make all colliders triggers so item doesnt block movement
            foreach (var col in GetComponentsInChildren<Collider>())
                col.isTrigger = true;

            // kinematic so physics doesnt interfere with the networked position
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.detectCollisions = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (Holder != null)
            {
                // snap to the holder's hand position every tick
                var holderPlayer = Holder.GetComponent<NetworkPlayer>();
                if (holderPlayer != null && holderPlayer.ItemHoldPoint != null)
                {
                    transform.position = holderPlayer.ItemHoldPoint.position
                        + (holderPlayer.ItemHoldPoint.rotation * _heldPositionOffset);
                    transform.rotation = holderPlayer.ItemHoldPoint.rotation
                        * Quaternion.Euler(_heldRotationOffset);
                }
            }
            else
            {
                // spin the item slowly when its sitting on the ground waiting to be picked up
                transform.Rotate(Vector3.up * _rotationSpeed * Runner.DeltaTime);
            }
        }

        public void Pickup(NetworkPlayer player)
        {
            // only the server should actually assign the holder
            if (!HasStateAuthority) return;

            Debug.Log($"[NetworkItem] Picked up by {player.PlayerName}");
            Holder = player.Object;
            player.HeldItem = Object;

            // immediately snap to hand position on pickup
            if (player.ItemHoldPoint != null)
            {
                transform.position = player.ItemHoldPoint.position
                    + (player.ItemHoldPoint.rotation * _heldPositionOffset);
                transform.rotation = player.ItemHoldPoint.rotation
                    * Quaternion.Euler(_heldRotationOffset);
            }
        }
    }
}
