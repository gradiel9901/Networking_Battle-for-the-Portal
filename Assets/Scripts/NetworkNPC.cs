using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace Network
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class NetworkNPC : NetworkBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float _detectionRange = 12f;
        [SerializeField] private float _detectionAngle = 60f;
        [SerializeField] private LayerMask _wallLayerMask = ~0;

        [Header("Chase Settings")]
        [SerializeField] private float _chaseSpeed = 10f;
        [SerializeField] private float _attackRange = 1.5f;
        [SerializeField] private int _attackDamage = 100;

        [Header("Contact Damage")]
        [SerializeField] private int _contactDamage = 25;
        [SerializeField] private float _contactDamageInterval = 1f;

        [Header("Roam Settings")]
        [SerializeField] private float _roamSpeed = 2f;
        [SerializeField] private float _roamRadius = 10f;
        [SerializeField] private float _roamWaitTime = 2f;

        private NavMeshAgent _agent;
        private Animator _animator;
        private NetworkPlayer _currentTarget;
        private string _lastAnimation;
        private float _chaseLoseMultiplier = 1.5f;
        private int _detectionTick;
        [SerializeField] private int _detectionInterval = 3;
        private Vector3 _lastSetDestination;
        [SerializeField] private float _destinationUpdateDistance = 0.8f;
        private float _contactDamageCooldown;

        [Networked] private TickTimer RoamWaitTimer { get; set; }
        [Networked] private TickTimer StunTimer { get; set; }
        [Networked] private NPCState CurrentState { get; set; }

        private enum NPCState
        {
            Idle,
            Roaming,
            Chasing,
            Stunned
        }

        public override void Spawned()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();

foreach (var col in GetComponentsInChildren<Collider>())
                col.isTrigger = true;

            if (HasStateAuthority)
            {
                _agent.enabled = true;
                _agent.speed = _roamSpeed;
                CurrentState = NPCState.Roaming;
                SetNewRoamDestination();
            }
            else
            {
                _agent.enabled = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            switch (CurrentState)
            {
                case NPCState.Roaming:
                case NPCState.Idle:
                    HandleRoamState();

_detectionTick++;
                    if (_detectionTick >= _detectionInterval)
                    {
                        _detectionTick = 0;
                        NetworkPlayer detected = DetectPlayerInCone();
                        if (detected != null)
                        {
                            _currentTarget = detected;
                            CurrentState = NPCState.Chasing;
                            _agent.speed = _chaseSpeed;
                        }
                    }
                    break;

                case NPCState.Chasing:
                    HandleChaseState();
                    break;

                case NPCState.Stunned:
                    HandleStunState();
                    break;
            }
        }

        public override void Render()
        {
            if (_animator == null) return;

            switch (CurrentState)
            {
                case NPCState.Chasing:
                    PlayAnimation("NPC_Run");
                    break;

                case NPCState.Roaming:
                    if (_agent != null && _agent.enabled && _agent.velocity.magnitude > 0.1f)
                        PlayAnimation("NPC_Walk");
                    else
                        PlayAnimation("NPC_Idle");
                    break;

                case NPCState.Stunned:
                    PlayAnimation("NPC_Stunned");
                    break;

                default:
                    PlayAnimation("NPC_Idle");
                    break;
            }
        }

        private void PlayAnimation(string animName)
        {
            if (_lastAnimation == animName) return;
            _lastAnimation = animName;
            _animator.Play(animName);
        }

        #region Roam Logic

        private void HandleRoamState()
        {
            _agent.speed = _roamSpeed;

if (RoamWaitTimer.IsRunning && !RoamWaitTimer.Expired(Runner))
            {
                _agent.isStopped = true;
                CurrentState = NPCState.Idle;
                return;
            }

if (RoamWaitTimer.Expired(Runner))
            {
                RoamWaitTimer = default;
                SetNewRoamDestination();
            }

            CurrentState = NPCState.Roaming;
            _agent.isStopped = false;

if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
            {
                RoamWaitTimer = TickTimer.CreateFromSeconds(Runner, _roamWaitTime);
                CurrentState = NPCState.Idle;
            }
        }

        private void SetNewRoamDestination()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * _roamRadius;
                Vector3 randomPoint = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

                if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                    return;
                }
            }

        }

        #endregion

        #region Cone Detection

        private NetworkPlayer DetectPlayerInCone()
        {
            NetworkPlayer closest = null;
            float closestDist = float.MaxValue;

            var allPlayers = Runner.GetAllBehaviours<NetworkPlayer>();

            foreach (var player in allPlayers)
            {
                if (player.CurrentHealth <= 0) continue;
                if (player.HasFinished) continue;

                Vector3 dirToPlayer = player.transform.position - transform.position;
                float distance = dirToPlayer.magnitude;

                if (distance > _detectionRange) continue;

                dirToPlayer.y = 0;
                float angle = Vector3.Angle(transform.forward, dirToPlayer.normalized);

                if (angle > _detectionAngle) continue;

                Vector3 rayOrigin = transform.position + Vector3.up * 1f;
                Vector3 rayTarget = player.transform.position + Vector3.up * 1f;
                Vector3 rayDir = (rayTarget - rayOrigin).normalized;
                float rayDist = Vector3.Distance(rayOrigin, rayTarget);

                if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, rayDist, _wallLayerMask))
                {
                    if (hit.transform.root != player.transform.root)
                    {
                        continue;
                    }
                }

                if (distance < closestDist)
                {
                    closestDist = distance;
                    closest = player;
                }
            }

            return closest;
        }

        #endregion

        #region Chase Logic

        private void HandleChaseState()
        {
            if (_currentTarget == null || _currentTarget.CurrentHealth <= 0)
            {
                ReturnToRoaming();
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);

            if (distanceToTarget > _detectionRange * _chaseLoseMultiplier)
            {
                ReturnToRoaming();
                return;
            }

            if (distanceToTarget <= _attackRange)
            {
                KillTarget();
                return;
            }

            _agent.isStopped = false;

float sqrMoved = (_currentTarget.transform.position - _lastSetDestination).sqrMagnitude;
            float threshold = _destinationUpdateDistance * _destinationUpdateDistance;
            if (sqrMoved > threshold)
            {
                _lastSetDestination = _currentTarget.transform.position;
                _agent.SetDestination(_currentTarget.transform.position);
            }

            Vector3 lookDir = (_currentTarget.transform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    Runner.DeltaTime * 8f
                );
            }
        }

        private void KillTarget()
        {
            if (_currentTarget != null && _currentTarget.CurrentHealth > 0)
                _currentTarget.RPC_TakeDamage(_attackDamage);

            ReturnToRoaming();
        }

        private void ReturnToRoaming()
        {
            _currentTarget = null;
            CurrentState = NPCState.Roaming;
            _agent.speed = _roamSpeed;
            if (_agent != null && _agent.enabled) _agent.isStopped = false;
            SetNewRoamDestination();
        }

        #endregion

        #region Stun Logic

        public void Stun(float duration)
        {
            if (!HasStateAuthority) return;

            _currentTarget = null;
            CurrentState = NPCState.Stunned;
            StunTimer = TickTimer.CreateFromSeconds(Runner, duration);
            if (_agent != null && _agent.enabled) _agent.isStopped = true;
        }

        private void HandleStunState()
        {
            if (_agent != null && _agent.enabled) _agent.isStopped = true;

            if (StunTimer.Expired(Runner))
            {
                StunTimer = default;
                ReturnToRoaming();
            }
        }

        #endregion

        #region Contact Damage

private void OnTriggerStay(Collider other)
        {
            if (!HasStateAuthority) return;
            if (CurrentState == NPCState.Stunned) return;

if (_contactDamageCooldown > 0f)
            {
                _contactDamageCooldown -= Time.deltaTime;
                return;
            }

            var player = other.GetComponentInParent<NetworkPlayer>();
            if (player != null && player.CurrentHealth > 0 && !player.HasFinished)
            {
                player.RPC_TakeDamage(_contactDamage);
                _contactDamageCooldown = _contactDamageInterval;
            }
        }

        #endregion

#region Gizmos

        private void OnDrawGizmosSelected()
        {

            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _detectionRange);

Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRange);

Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Vector3 origin = transform.position + Vector3.up * 0.5f;

            Vector3 leftBoundary = Quaternion.Euler(0, -_detectionAngle, 0) * transform.forward;
            Vector3 rightBoundary = Quaternion.Euler(0, _detectionAngle, 0) * transform.forward;

            Gizmos.DrawRay(origin, leftBoundary * _detectionRange);
            Gizmos.DrawRay(origin, rightBoundary * _detectionRange);

            int segments = 20;
            Vector3 prevPoint = origin + leftBoundary * _detectionRange;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float currentAngle = Mathf.Lerp(-_detectionAngle, _detectionAngle, t);
                Vector3 dir = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
                Vector3 point = origin + dir * _detectionRange;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }

        #endregion
    }
}
