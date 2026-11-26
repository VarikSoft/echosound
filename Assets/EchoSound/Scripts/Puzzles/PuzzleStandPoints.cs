using System.Collections.Generic;
using UnityEngine;

namespace EchoSound.Puzzles
{
    internal static class PuzzleStandPoints
    {
        /// <summary>
        /// Four floor points around an object; the companion picks the nearest reachable one.
        /// Points are dropped to the floor, because the object itself may sit on a pedestal.
        /// </summary>
        public static IEnumerable<Vector3> Around(Transform item, float distance)
        {
            Vector3 forward = Vector3.ProjectOnPlane(item.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(item.right, Vector3.up).normalized;
            yield return ToFloor(item.position + forward * distance);
            yield return ToFloor(item.position - forward * distance);
            yield return ToFloor(item.position + right * distance);
            yield return ToFloor(item.position - right * distance);
        }

        private static Vector3 ToFloor(Vector3 point)
        {
            return Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : point;
        }
    }
}
