using Fusion;
using UnityEngine;

namespace Network
{
    public struct NetworkInputData : INetworkInput
    {
        public Vector3 InputVector;
        public Vector2 LookRotation;
        public NetworkBool JumpInput;
        public NetworkBool SprintInput;
        public NetworkBool CrouchInput;
        public NetworkBool InteractInput;
        public NetworkBool AttackInput;
    }
}
