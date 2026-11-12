using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EchoSound.Interaction
{
    /// <summary>
    /// A sliding door that fills a doorway. While closed it blocks bodies and carves the NavMesh,
    /// so agents plan routes around it. Opening slides the panel sideways into the wall.
    /// </summary>
    public sealed class Door : Interactable
    {
        private static readonly InteractionAction[] actions = { InteractionAction.Open, InteractionAction.Close };

        public Transform panel;
        [Min(0.5f)] public float width = 3;
        [Min(0.5f)] public float height = 2.6f;
        [Min(0.1f)] public float slideSpeed = 2.5f;
        [Tooltip("Distance from the doorway to the spot where an agent stands to use the door.")]
        [Min(0.5f)] public float standDistance = 1.1f;
        [SerializeField] private bool startOpen;

        private NavMeshObstacle obstacle;
        // 0 means closed, 1 means fully open.
        private float openAmount;

        public bool IsOpen { get; private set; }
        public override string Kind => "door";
        public override string State => IsBusy ? (IsOpen ? "opening" : "closing") : (IsOpen ? "open" : "closed");
        public override IReadOnlyList<InteractionAction> Actions => actions;
        public override InteractionAction PrimaryAction => IsOpen ? InteractionAction.Close : InteractionAction.Open;
        public override bool IsBusy => openAmount != (IsOpen ? 1 : 0);

        public override IEnumerable<Vector3> StandPoints
        {
            get
            {
                yield return transform.position + transform.forward * standDistance;
                yield return transform.position - transform.forward * standDistance;
            }
        }

        private void Awake()
        {
            obstacle = GetComponent<NavMeshObstacle>();
            IsOpen = startOpen;
            openAmount = startOpen ? 1 : 0;
            ApplyOpenAmount();
        }

        protected override bool CanPerform(InteractionAction action, out string problem)
        {
            bool open = action == InteractionAction.Open;
            problem = null;
            if (IsOpen == open) problem = open ? "it is already open" : "it is already closed";
            else if (!open && IsDoorwayBlocked()) problem = "something is standing in the doorway";
            return problem == null;
        }

        // Starts sliding; Update moves the panel.
        protected override void Perform(Interactor user, InteractionAction action)
        {
            IsOpen = action == InteractionAction.Open;
        }

        private void Update()
        {
            if (!IsBusy) return;
            openAmount = Mathf.MoveTowards(openAmount, IsOpen ? 1 : 0, slideSpeed / width * Time.deltaTime);
            ApplyOpenAmount();
        }

        private void ApplyOpenAmount()
        {
            if (panel != null) panel.localPosition = new Vector3(openAmount * width, height * 0.5f, 0);
            // Routes through the doorway exist only while the panel is fully out of the way.
            if (obstacle != null) obstacle.enabled = openAmount < 1;
        }

        private bool IsDoorwayBlocked()
        {
            // Slightly smaller than the opening, so walls, floor and lintel around it do not count.
            Vector3 centre = transform.TransformPoint(new Vector3(0, height * 0.5f, 0));
            var halfExtents = new Vector3(width * 0.5f - 0.2f, height * 0.5f - 0.2f, 0.1f);
            foreach (Collider hit in Physics.OverlapBox(centre, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform)) return true;
            }
            return false;
        }

        /// <summary>Builds a door from primitives, so generated levels need no door prefab.</summary>
        public static Door Create(string id, Transform parent, Vector3 position, Quaternion rotation,
            float width, float height, Material material, bool open)
        {
            var root = new GameObject("Door " + id);
            // Stay inactive until configured, so Awake sees the final settings.
            root.SetActive(false);
            root.transform.SetPositionAndRotation(position, rotation);
            root.transform.SetParent(parent, true);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(root.transform, false);
            panel.transform.localScale = new Vector3(width, height, 0.12f);
            if (material != null) panel.GetComponent<Renderer>().sharedMaterial = material;

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0, height * 0.5f, 0);
            obstacle.size = new Vector3(width, height, 0.3f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;

            var door = root.AddComponent<Door>();
            door.id = id;
            door.panel = panel.transform;
            door.width = width;
            door.height = height;
            // Usable from anywhere in front of the opening, including its edges.
            door.useDistance = width * 0.5f + 1;
            door.startOpen = open;
            root.SetActive(true);
            return door;
        }
    }
}
