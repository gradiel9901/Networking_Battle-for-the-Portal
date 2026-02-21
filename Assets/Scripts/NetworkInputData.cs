using Fusion;
using UnityEngine;

namespace Network
{
    // this is what gets sent from client to server each tick so the host knows what the player is doing
    // INetworkInput tells Fusion to collect and sync this automatically
    public struct NetworkInputData : INetworkInput
    {
        public Vector3 InputVector;       // movement direction (world space)
        public Vector2 LookRotation;      // x = yaw, y = pitch
        public NetworkBool JumpInput;
        public NetworkBool SprintInput;
        public NetworkBool CrouchInput;
        public NetworkBool InteractInput;
        public NetworkBool AttackInput;
    }
}
