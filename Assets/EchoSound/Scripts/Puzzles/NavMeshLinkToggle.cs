using UnityEngine;
using UnityEngine.AI;

namespace EchoSound.Puzzles
{
    /// <summary>
    /// A NavMesh connection that exists only while its signal is on, for example over a lowered beam or a turned bridge.
    /// The NavMesh is baked once per level, so moving geometry needs this to let agents use the new route.
    /// </summary>
    public sealed class NavMeshLinkToggle : SignalReceiver
    {
        public Transform start;
        public Transform end;
        [Min(0)] public float width = 1.5f;

        private NavMeshLinkInstance link;
        private bool wanted;
        private bool dirty;

        // Applied in Update, not here: a generated room is moved into place after it is created and enabled,
        // and the link is placed in world space.
        protected override void OnSignal(bool on)
        {
            wanted = on;
            dirty = true;
        }

        private void Update()
        {
            if (!dirty) return;
            dirty = false;
            Remove();
            if (!wanted || start == null || end == null) return;

            link = NavMesh.AddLink(new NavMeshLinkData
            {
                startPosition = start.position,
                endPosition = end.position,
                width = width,
                bidirectional = true,
                area = 0,
                costModifier = -1
            });
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Remove();
        }

        private void Remove()
        {
            if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link);
        }
    }
}
