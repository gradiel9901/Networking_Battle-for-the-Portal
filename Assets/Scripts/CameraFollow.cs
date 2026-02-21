using UnityEngine;

namespace Com.MyCompany.MyGame
{
    // fallback camera that follows a target, only used if there's no Cinemachine
    public class CameraFollow : MonoBehaviour
    {
        private void Awake()
        {
            // if we already have a cinemachine camera in the scene just turn this off
            if (FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>() != null)
            {
                Debug.LogWarning("[CameraFollow] Disabling self because CinemachineCamera was found.");
                enabled = false;
                return;
            }
        }

        [SerializeField] private float smoothSpeed = 0.125f;
        [SerializeField] private Vector3 offset = new Vector3(0, 10, -10);

        private Transform target;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            // lerp toward the target position each frame for smooth following
            Vector3 desiredPosition = target.position + offset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
            transform.position = smoothedPosition;

            transform.LookAt(target);
        }
    }
}
