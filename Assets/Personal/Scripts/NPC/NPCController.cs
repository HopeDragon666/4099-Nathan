using EditorAttributes;
using UnityEngine;
using UnityEngine.AI;

namespace NPC
{
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public class NPCController : MonoBehaviour
    {
        enum MovementMode
        {
            Radius,
            PathNodes
        }

        enum NPCState
        {
            Idle,
            Wandering
        }

        [SerializeField, Title("Movement Mode")] MovementMode movementMode = MovementMode.Radius;

        [SerializeField] bool startInIdle = true;

        [SerializeField, Title("State Timings")] Vector2 idleIntervalRange = new Vector2(1.5f, 4f);

        [SerializeField, Min(0.5f)] float maxMoveDuration = 12f;

        [SerializeField, ShowField(nameof(movementMode), MovementMode.Radius), Title("Radius System")]
        Transform radiusCenter;

        [SerializeField, ShowField(nameof(movementMode), MovementMode.Radius), Min(0.5f)] float wanderRadius = 8f;

        [SerializeField, ShowField(nameof(movementMode), MovementMode.Radius), Min(1)] int randomPointAttempts = 10;

        [SerializeField, Title("NavMesh Sampling"), Min(0.1f)] float navMeshSampleDistance = 2f;

        [SerializeField, ShowField(nameof(movementMode), MovementMode.PathNodes), Title("Path Node System")]
        Transform[] pathNodes;

        [SerializeField, ShowField(nameof(movementMode), MovementMode.PathNodes)] bool avoidImmediateNodeRepeat = true;

        [SerializeField, Title("Debug")] bool printWarnings = true;

        [SerializeField] bool drawGizmos = true;

        NavMeshAgent navMeshAgent;
        NPCState _state;
        float _stateTimer;
        float _moveTimer;
        int _lastNodeIndex = -1;
        Vector3 _spawnPosition;
        Vector3 _currentDestination;
        bool _isDestinationActive;
        bool _hasWarnedAboutNavMesh;

        void Awake()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            _spawnPosition = transform.position;

            if (navMeshAgent == null)
            {
                print("[Error] NPCController: Missing required NavMeshAgent component.");
                enabled = false;
                return;
            }

            ClampRanges();
        }

        void OnEnable()
        {
            if (navMeshAgent == null)
            {
                return;
            }

            // Starting in idle avoids invalid SetDestination calls when the agent has not reached NavMesh yet.
            if (!navMeshAgent.isOnNavMesh)
            {
                EnterIdleState();
                return;
            }

            if (startInIdle)
            {
                EnterIdleState();
                return;
            }

            TryStartWanderState();
        }

        void Update()
        {
            if (navMeshAgent == null)
            {
                return;
            }

            if (!navMeshAgent.isOnNavMesh)
            {
                // Warn once and skip updates until this object is on a baked NavMesh.
                if (!_hasWarnedAboutNavMesh && printWarnings)
                {
                    print("[Warning] NPCController: Agent is not on a NavMesh. Check bake and spawn position.");
                    _hasWarnedAboutNavMesh = true;
                }

                return;
            }

            _hasWarnedAboutNavMesh = false;

            if (_state == NPCState.Idle)
            {
                TickIdleState();
                return;
            }

            TickWanderState();
        }

        void TickIdleState()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
            {
                return;
            }

            TryStartWanderState();
        }

        void TickWanderState()
        {
            _moveTimer -= Time.deltaTime;

            // Destination checks are intentionally simple for low overhead and robust behavior.
            if (HasReachedDestination())
            {
                EnterIdleState();
                return;
            }

            // If movement takes too long (blocked/unreachable), fall back to idle and retry later.
            if (_moveTimer <= 0f)
            {
                if (printWarnings)
                {
                    print("[Warning] NPCController: Move timeout reached. Returning to idle before next destination pick.");
                }

                EnterIdleState();
            }
        }

        void TryStartWanderState()
        {
            if (!navMeshAgent.isOnNavMesh)
            {
                EnterIdleState();
                return;
            }

            if (!TryGetNextDestination(out Vector3 destination))
            {
                if (printWarnings)
                {
                    print("[Warning] NPCController: Could not find a valid destination. Retrying after idle interval.");
                }

                EnterIdleState();
                return;
            }

            _state = NPCState.Wandering;
            _moveTimer = maxMoveDuration;
            _currentDestination = destination;
            _isDestinationActive = true;

            navMeshAgent.isStopped = false;
            if (!navMeshAgent.SetDestination(destination))
            {
                if (printWarnings)
                {
                    print("[Warning] NPCController: SetDestination failed. Retrying after idle interval.");
                }

                EnterIdleState();
            }
        }

        void EnterIdleState()
        {
            _state = NPCState.Idle;
            _stateTimer = Random.Range(idleIntervalRange.x, idleIntervalRange.y);

            _isDestinationActive = false;

            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
            {
                return;
            }

            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
        }

        bool TryGetNextDestination(out Vector3 destination)
        {
            if (movementMode == MovementMode.PathNodes)
            {
                return TryGetRandomNodeDestination(out destination);
            }

            return TryGetRandomRadiusDestination(out destination);
        }

        bool TryGetRandomRadiusDestination(out Vector3 destination)
        {
            Vector3 center = radiusCenter != null ? radiusCenter.position : _spawnPosition;

            for (int attempt = 0; attempt < randomPointAttempts; attempt++)
            {
                // Sample XZ around center and project onto NavMesh for valid, cheap random roaming.
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    destination = hit.position;
                    return true;
                }
            }

            if (NavMesh.SamplePosition(center, out NavMeshHit fallbackHit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                destination = fallbackHit.position;
                return true;
            }

            destination = Vector3.zero;
            return false;
        }

        bool TryGetRandomNodeDestination(out Vector3 destination)
        {
            if (pathNodes == null || pathNodes.Length == 0)
            {
                destination = Vector3.zero;
                return false;
            }

            int maxAttempts = Mathf.Max(pathNodes.Length * 2, 4);

            // Random probing keeps this cheap while still filtering null/invalid/navmesh-missing nodes.
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int index = Random.Range(0, pathNodes.Length);

                if (avoidImmediateNodeRepeat && pathNodes.Length > 1 && index == _lastNodeIndex)
                {
                    continue;
                }

                Transform node = pathNodes[index];
                if (node == null)
                {
                    continue;
                }

                if (!NavMesh.SamplePosition(node.position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    continue;
                }

                _lastNodeIndex = index;
                destination = hit.position;
                return true;
            }

            // Fallback pass allows same-node selection when options are limited.
            for (int index = 0; index < pathNodes.Length; index++)
            {
                Transform node = pathNodes[index];
                if (node == null)
                {
                    continue;
                }

                if (!NavMesh.SamplePosition(node.position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    continue;
                }

                _lastNodeIndex = index;
                destination = hit.position;
                return true;
            }

            destination = Vector3.zero;
            return false;
        }

        bool HasReachedDestination()
        {
            if (!_isDestinationActive)
            {
                return false;
            }

            if (navMeshAgent.pathPending)
            {
                return false;
            }

            if (navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance + 0.05f)
            {
                return false;
            }

            if (navMeshAgent.hasPath && navMeshAgent.velocity.sqrMagnitude > 0.01f)
            {
                return false;
            }

            return true;
        }

        void ClampRanges()
        {
            idleIntervalRange.x = Mathf.Max(0.1f, idleIntervalRange.x);
            idleIntervalRange.y = Mathf.Max(idleIntervalRange.x, idleIntervalRange.y);
            maxMoveDuration = Mathf.Max(0.5f, maxMoveDuration);
            wanderRadius = Mathf.Max(0.5f, wanderRadius);
            randomPointAttempts = Mathf.Max(1, randomPointAttempts);
            navMeshSampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            ClampRanges();
        }
#endif

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            Vector3 center = radiusCenter != null ? radiusCenter.position : transform.position;

            if (movementMode == MovementMode.Radius)
            {
                Gizmos.color = new Color(0f, 0.9f, 1f, 0.4f);
                Gizmos.DrawWireSphere(center, wanderRadius);
            }

            if (_isDestinationActive)
            {
                Gizmos.color = new Color(1f, 0.9f, 0f, 0.7f);
                Gizmos.DrawLine(transform.position, _currentDestination);
                Gizmos.DrawSphere(_currentDestination, 0.2f);
            }
        }
    }
}