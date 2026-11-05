using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace EchoSound.Companion
{
    /// <summary>
    /// The companion's body: walks on the NavMesh and follows the curator.
    /// Knows nothing about the LLM; tools call these methods.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CompanionAgent : MonoBehaviour
    {
        [Tooltip("The player who gives orders.")]
        public Transform curator;
        [Min(0.5f)] public float followDistance = 2.5f;
        [Tooltip("Seconds without progress before a walk is reported as stuck.")]
        [Min(0.5f)] public float stuckTimeout = 3f;

        private NavMeshAgent agent;
        private bool following;
        private float nextFollowUpdate;

        /// <summary>What the companion is doing, in words, for the LLM's perception.</summary>
        public string Activity { get; set; } = "waiting";
        public bool IsWalking { get; private set; }
        public bool IsPlaced => agent != null && agent.enabled && agent.isOnNavMesh;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            // Levels and their NavMesh are built at runtime; Place() enables the agent afterwards.
            agent.enabled = false;
        }

        /// <summary>Puts the companion on the NavMesh near a point. Call after the NavMesh is built.</summary>
        public bool Place(Vector3 position)
        {
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) return false;
            if (agent.enabled)
            {
                agent.Warp(hit.position);
            }
            else
            {
                // Enabling the agent away from the NavMesh logs an error, so move first.
                transform.position = hit.position;
                agent.enabled = true;
            }
            Halt();
            return true;
        }

        public void Follow()
        {
            following = true;
            nextFollowUpdate = 0;
            Activity = "following the curator";
        }

        public void Halt(string activity = "waiting")
        {
            following = false;
            IsWalking = false;
            if (IsPlaced) agent.ResetPath();
            Activity = activity;
        }

        /// <summary>Length of the walking route to a point; false if the point cannot be reached.</summary>
        public bool TryGetRouteLength(Vector3 point, out float length)
        {
            length = 0;
            NavMeshPath path = FindRoute(point);
            if (path == null) return false;

            Vector3[] corners = path.corners;
            for (int index = 1; index < corners.Length; index++)
            {
                length += Vector3.Distance(corners[index - 1], corners[index]);
            }
            return true;
        }

        /// <summary>Walks to a point. If the walk cannot finish, onFailed receives a short reason.</summary>
        public IEnumerator WalkTo(Vector3 point, float arriveDistance, Action<string> onFailed)
        {
            following = false;
            NavMeshPath path = FindRoute(point);
            if (path == null)
            {
                onFailed("there is no walkable route there");
                yield break;
            }

            IsWalking = true;
            agent.stoppingDistance = 0.1f;
            agent.SetPath(path);
            // Progress is measured by movement: remainingDistance can be unknown on long routes.
            Vector3 checkpoint = transform.position;
            float lastProgressTime = Time.time;

            while (!HasArrived(point, arriveDistance))
            {
                if (!IsPlaced)
                {
                    IsWalking = false;
                    onFailed("lost the walkable floor");
                    yield break;
                }

                if ((transform.position - checkpoint).sqrMagnitude > 0.04f)
                {
                    checkpoint = transform.position;
                    lastProgressTime = Time.time;
                }
                else if (Time.time - lastProgressTime > stuckTimeout)
                {
                    // A door closing on the way makes the route partial, and the agent stops short.
                    bool blocked = agent.pathStatus != NavMeshPathStatus.PathComplete;
                    Halt();
                    onFailed(blocked ? "the way got blocked, maybe by a closed door" : "got stuck on the way");
                    yield break;
                }
                yield return null;
            }

            agent.ResetPath();
            IsWalking = false;
        }

        private bool HasArrived(Vector3 point, float arriveDistance)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0;
            if (offset.magnitude <= arriveDistance) return true;
            // The end of a partial route is not the destination.
            return !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathComplete
                && agent.remainingDistance <= arriveDistance;
        }

        public void Face(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(direction);
        }

        private NavMeshPath FindRoute(Vector3 point)
        {
            if (!IsPlaced || !NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return null;

            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return null;
            return path;
        }

        private void Update()
        {
            if (!following || curator == null || !IsPlaced || Time.time < nextFollowUpdate) return;

            // Re-planning a few times per second is enough to keep up with a walking player.
            nextFollowUpdate = Time.time + 0.25f;
            agent.stoppingDistance = followDistance;
            agent.SetDestination(curator.position);
        }
    }
}
