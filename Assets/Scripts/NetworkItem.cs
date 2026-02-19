using Fusion;
using UnityEngine;

namespace Network
{
    [RequireComponent(typeof(NetworkTransform))]
    public class NetworkItem : NetworkBehaviour
    {
        [SerializeField] private float _rotationSpeed = 50f;
        [SerializeField] private Vector3 _heldRotationOffset = Vector3.zero;
        [SerializeField] private Vector3 _heldPositionOffset = Vector3.zero;

        [Networked] public NetworkObject Holder { get; set; }

        public override void Spawned()
        {

foreach (var col in GetComponentsInChildren<Collider>())
                col.isTrigger = true;

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

                transform.Rotate(Vector3.up * _rotationSpeed * Runner.DeltaTime);
            }
        }

        public void Pickup(NetworkPlayer player)
        {
            if (!HasStateAuthority) return;

            Debug.Log($"[NetworkItem] Picked up by {player.PlayerName}");
            Holder = player.Object;
            player.HeldItem = Object;

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
