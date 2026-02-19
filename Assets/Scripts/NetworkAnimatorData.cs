using Fusion;
using UnityEngine;

namespace Network
{
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
