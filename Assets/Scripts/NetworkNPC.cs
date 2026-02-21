using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace Network
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    // enemy NPC that roams around and chases players when it sees them
    public class NetworkNPC : NetworkBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float _detectionRange = 12f;
        [SerializeField] private float _detectionAngle = 60f;    // half angle of the vision cone
        [SerializeField] private LayerMask _wallLayerMask = ~0;

        [Header("Chase Settings")]
        [SerializeField] private float _chaseSpeed = 10f;
        [SerializeField] private float _attackRange = 1.5f;
        [SerializeField] private int _attackDamage = 100;

        [Header("Contact Damage")]
        [SerializeField] private int _contactDamage = 25;
        [SerializeField] private float _contactDamageInterval = 1f;

        [Header("Audio")]
        [SerializeField] private AudioClip _npcSound;
        private AudioSource _audioSource;

        [Header("Roam Settings")]
        [SerializeField] private float _roamSpeed = 2f;
        [SerializeField] private float _roamRadius = 10f;
        [SerializeField] private float _roamWaitTime = 2f;

        private NavMeshAgent _agent;
        private Animator _animator;
        private NetworkPlayer _currentTarget;
        private string _lastAnimation;
        private float _chaseLoseMultiplier = 1.5f;   // how much further than detection range before giving up
        private int _detectionTick;
        [SerializeField] private int _detectionInterval = 3;    // only check for players every N ticks
        private Vector3 _lastSetDestination;
        [SerializeField] private float _destinationUpdateDistance = 0.8f;  // don't spam SetDestination every tick
        private float _contactDamageCooldown;

        // networked so all clients see the same state
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
            
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f; // Make it completely 3D
                _audioSource.rolloffMode = AudioRolloffMode.Linear; // Predictable volume drop
                _audioSource.minDistance = 2f;  // Full volume when this close
                _audioSource.maxDistance = 15f; // Zero volume when further than this
                _audioSource.loop = true;
            }
            
            if (Com.MyCompany.MyGame.SoundManager.Instance != null)
            {
                _audioSource.volume = Com.MyCompany.MyGame.SoundManager.Instance.NpcVolume;
                Com.MyCompany.MyGame.SoundManager.OnNpcVolumeChanged += HandleVolumeChange;
            }

            // need triggers so the NPC can overlap with players for contact damage
            foreach (var col in GetComponentsInChildren<Collider>())
                col.isTrigger = true;

            if (HasStateAuthority)
            {
                // only the server runs the AI, clients just see the visual result
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

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Com.MyCompany.MyGame.SoundManager.Instance != null)
            {
                Com.MyCompany.MyGame.SoundManager.OnNpcVolumeChanged -= HandleVolumeChange;
            }
            base.Despawned(runner, hasState);
        }

        private void HandleVolumeChange(float newVolume)
        {
            if (_audioSource != null)
            {
                _audioSource.volume = newVolume;
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

                    // dont check for players every single tick, too expensive
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

        // Render runs on every frame (not just physics ticks) so animations look smooth
        public override void Render()
        {
            if (_animator == null) return;

            switch (CurrentState)
            {
                case NPCState.Chasing:
                    PlayAnimation("NPC_Run");
                    PlayLoopingSound(_npcSound);
                    break;

                case NPCState.Roaming:
                    // only walk if the agent is actually moving
                    if (_agent != null && _agent.enabled && _agent.velocity.magnitude > 0.1f)
                        PlayAnimation("NPC_Walk");
                    else
                        PlayAnimation("NPC_Idle");
                        
                    PlayLoopingSound(_npcSound);
                    break;

                case NPCState.Stunned:
                    PlayAnimation("NPC_Stunned");
                    if (_audioSource != null && _audioSource.isPlaying) _audioSource.Stop();
                    break;

                default:
                    PlayAnimation("NPC_Idle");
                    PlayLoopingSound(_npcSound);
                    break;
            }
        }

        private void PlayLoopingSound(AudioClip clip)
        {
            if (_audioSource == null || clip == null) return;
            if (_audioSource.clip != clip || !_audioSource.isPlaying)
            {
                _audioSource.clip = clip;
                _audioSource.Play();
            }
        }

        // avoid calling animator.Play if we're already in this state, prevents flickering
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

            // if we're waiting at a point, stay idle till timer runs out
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

            // reached the destination, wait a bit then pick a new one
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
            {
                RoamWaitTimer = TickTimer.CreateFromSeconds(Runner, _roamWaitTime);
                CurrentState = NPCState.Idle;
            }
        }

        private void SetNewRoamDestination()
        {
            // try a few random positions and use the first valid navmesh point
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
            // if all 10 attempts fail we just stay put, not a big deal
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
                // skip dead or finished players
                if (player.CurrentHealth <= 0) continue;
                if (player.HasFinished) continue;

                Vector3 dirToPlayer = player.transform.position - transform.position;
                float distance = dirToPlayer.magnitude;

                if (distance > _detectionRange) continue;

                // flatten the angle check to ignore height differences
                dirToPlayer.y = 0;
                float angle = Vector3.Angle(transform.forward, dirToPlayer.normalized);

                if (angle > _detectionAngle) continue;

                // line of sight check, make sure nothing is blocking the view
                Vector3 rayOrigin = transform.position + Vector3.up * 1f;
                Vector3 rayTarget = player.transform.position + Vector3.up * 1f;
                Vector3 rayDir = (rayTarget - rayOrigin).normalized;
                float rayDist = Vector3.Distance(rayOrigin, rayTarget);

                if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, rayDist, _wallLayerMask))
                {
                    if (hit.transform.root != player.transform.root)
                    {
                        continue; // something blocked the raycast, player isnt visible
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
                ReturnToRoaming(); // target died or disconnected
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);

            // give up chasing if target got too far away
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

            // only update the destination if the target moved enough, avoids spamming navmesh
            float sqrMoved = (_currentTarget.transform.position - _lastSetDestination).sqrMagnitude;
            float threshold = _destinationUpdateDistance * _destinationUpdateDistance;
            if (sqrMoved > threshold)
            {
                _lastSetDestination = _currentTarget.transform.position;
                _agent.SetDestination(_currentTarget.transform.position);
            }

            // smoothly rotate towards the target
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

            if (_audioSource != null && _npcSound != null)
            {
                _audioSource.PlayOneShot(_npcSound);
            }
        }

        private void HandleStunState()
        {
            if (_agent != null && _agent.enabled) _agent.isStopped = true;

            // resume roaming once the stun timer expires
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
            if (CurrentState == NPCState.Stunned) return; // stunned NPCs dont deal damage

            // cooldown so it doesnt deal damage every single frame
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

                if (_audioSource != null && _npcSound != null)
                {
                    _audioSource.PlayOneShot(_npcSound);
                }
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            // yellow sphere = detection range
            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _detectionRange);

            // red sphere = attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRange);

            // green lines = the vision cone edges
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Vector3 origin = transform.position + Vector3.up * 0.5f;

            Vector3 leftBoundary = Quaternion.Euler(0, -_detectionAngle, 0) * transform.forward;
            Vector3 rightBoundary = Quaternion.Euler(0, _detectionAngle, 0) * transform.forward;

            Gizmos.DrawRay(origin, leftBoundary * _detectionRange);
            Gizmos.DrawRay(origin, rightBoundary * _detectionRange);

            // connect the arc of the cone with line segments
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
