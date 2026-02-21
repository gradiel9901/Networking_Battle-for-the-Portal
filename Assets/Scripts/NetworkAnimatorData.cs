using Fusion;
using UnityEngine;

namespace Network
{
    // this struct gets synced over the network every tick to drive the animator
    // NetworkBool is like a bool but Fusion can replicate it properly
    public struct NetworkAnimatorData : INetworkStruct
    {
        public float Speed;
        public NetworkBool Jump;
        public NetworkBool Forward;
        public NetworkBool Back;
        public NetworkBool Left;
        public NetworkBool Right;
        public NetworkBool Crouch;
        public NetworkBool IsGrounded;
        public NetworkBool IsAttacking;
    }
}
