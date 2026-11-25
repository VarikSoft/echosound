using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace EchoSound.Characters
{
    /// <summary>
    /// Plays idle / walk / run / jump / fall / land from any <see cref="ILocomotionSource"/>.
    /// Clips come from the shared set; any filled override slot replaces just that clip.
    /// </summary>
    public sealed class CharacterAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Header("Animations")]
        [SerializeField] private CharacterAnimationSet defaults;
        [Tooltip("Filled slots replace the default clip; empty slots keep it.")]
        [SerializeField] private CharacterAnimationClips overrides = new CharacterAnimationClips();

        [Header("Locomotion")]
        [Tooltip("Horizontal speed at which the walk clip plays at normal rate.")]
        [Min(0.1f)] public float walkSpeed = 4.5f;
        [Tooltip("Horizontal speed at which the run clip plays at normal rate.")]
        [Min(0.2f)] public float runSpeed = 7.5f;
        [Min(0.01f)] public float blendTime = 0.15f;

        [Header("Direction")]
        [Tooltip("How far the legs turn towards a sideways move while the torso keeps facing forward.")]
        [Range(0, 90)] public float maxLegYaw = 70f;
        [Min(0.01f)] public float legTurnTime = 0.1f;

        private const float AirDelay = 0.1f;
        private const float LandAfterAirTime = 0.25f;
        private const int Count = CharacterAnimationClips.SlotCount;

        private ILocomotionSource source;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[Count];
        private readonly AnimationClip[] clips = new AnimationClip[Count];
        private readonly float[] weights = new float[Count];
        private readonly float[] targets = new float[Count];
        private bool wasGrounded = true;
        private float airTime;
        private float landTimer;
        private float stridePhase;
        private float legYaw;
        private bool movingBackward;
        private Transform hips, spine, chest;

        public AnimationClip GetClip(CharacterAnimationSlot slot) => clips[(int)slot];

        /// <summary>Replaces one slot at runtime; null restores the default clip.</summary>
        public void SetOverride(CharacterAnimationSlot slot, AnimationClip clip)
        {
            overrides[slot] = clip;
            if (graph.IsValid()) Rebuild();
        }

        public void ResetMotion()
        {
            wasGrounded = true;
            airTime = 0;
            landTimer = 0;
            stridePhase = 0;
            legYaw = 0;
            movingBackward = false;
            for (int i = 0; i < Count; i++) weights[i] = i == (int)CharacterAnimationSlot.Idle ? 1 : 0;
        }

        private void OnEnable()
        {
            source = GetComponentInParent<ILocomotionSource>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("CharacterAnimator needs an Animator.", this);
                enabled = false;
                return;
            }
            if (animator.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            }
            Rebuild();
            ResetMotion();
        }

        private void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        private void OnValidate()
        {
            // Lets slots be swapped in the inspector during play mode.
            if (Application.isPlaying && graph.IsValid()) Rebuild();
        }

        private void Rebuild()
        {
            if (graph.IsValid()) graph.Destroy();
            graph = PlayableGraph.Create(name + " Animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, Count);
            AnimationPlayableOutput.Create(graph, "Character", animator).SetSourcePlayable(mixer);

            for (int i = 0; i < Count; i++)
            {
                var slot = (CharacterAnimationSlot)i;
                clips[i] = overrides[slot] != null ? overrides[slot] : defaults != null ? defaults.clips[slot] : null;
                if (clips[i] == null) continue;
                playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                // Clip playables enable foot IK by default, unlike Animator states; clips without
                // IK goal curves would pull both feet up to the body centre.
                playables[i].SetApplyFootIK(false);
                // Walk and run are driven by the stride phase rather than by graph time.
                if (slot == CharacterAnimationSlot.Walk || slot == CharacterAnimationSlot.Run) playables[i].SetSpeed(0);
                graph.Connect(playables[i], 0, mixer, i);
            }
            graph.Play();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0 || !graph.IsValid()) return;

            Vector3 velocity = source != null ? source.Velocity : Vector3.zero;
            bool grounded = source == null || source.IsGrounded;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;

            if (grounded)
            {
                if (!wasGrounded && airTime > LandAfterAirTime && Has(CharacterAnimationSlot.Land))
                {
                    landTimer = clips[(int)CharacterAnimationSlot.Land].length;
                    Restart(CharacterAnimationSlot.Land);
                }
                airTime = 0;
            }
            else
            {
                if (wasGrounded) Restart(CharacterAnimationSlot.Jump);
                airTime += dt;
            }
            wasGrounded = grounded;
            landTimer -= dt;

            System.Array.Clear(targets, 0, Count);
            if (airTime > AirDelay)
                Target(velocity.y > 0.1f ? CharacterAnimationSlot.Jump : CharacterAnimationSlot.Fall, 1);
            else if (landTimer > 0)
                Target(CharacterAnimationSlot.Land, 1);
            else
                TargetLocomotion(speed);

            float step = dt / blendTime, sum = 0;
            for (int i = 0; i < Count; i++) sum += weights[i] = Mathf.MoveTowards(weights[i], targets[i], step);
            for (int i = 0; i < Count; i++) mixer.SetInputWeight(i, sum > 0 ? weights[i] / sum : 0);

            float direction = UpdateLegYaw(velocity, speed, dt);
            UpdateStride(speed, direction, dt);
        }

        // Turns the legs towards the move direction; backwards moves keep the legs forward and play the stride in reverse.
        private float UpdateLegYaw(Vector3 velocity, float speed, float dt)
        {
            float target = 0, direction = 1;
            if (speed > 0.2f)
            {
                Vector3 local = transform.InverseTransformDirection(velocity);
                float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                // Hysteresis stops pure strafing from flickering between forward and backward.
                movingBackward = Mathf.Abs(angle) > (movingBackward ? 80 : 100);
                if (movingBackward) { angle = Mathf.DeltaAngle(180, angle); direction = -1; }
                target = Mathf.Clamp(angle, -maxLegYaw, maxLegYaw);
            }
            legYaw = Mathf.Lerp(legYaw, target, 1 - Mathf.Exp(-dt / legTurnTime));
            return direction;
        }

        // Walk and run share one normalised phase, so blending between them never breaks the step.
        private void UpdateStride(float speed, float direction, float dt)
        {
            float walkLength = Length(CharacterAnimationSlot.Walk), runLength = Length(CharacterAnimationSlot.Run);
            float run = Mathf.Clamp01((speed - walkSpeed) / Mathf.Max(0.01f, runSpeed - walkSpeed));
            float walkRate = Mathf.Clamp(speed / walkSpeed, 0.5f, 2f) / walkLength;
            float runRate = Mathf.Clamp(speed / runSpeed, 0.5f, 2f) / runLength;
            stridePhase = Mathf.Repeat(stridePhase + direction * Mathf.Lerp(walkRate, runRate, run) * dt, 1);
            SetPhase(CharacterAnimationSlot.Walk, walkLength);
            SetPhase(CharacterAnimationSlot.Run, runLength);
        }

        private void LateUpdate()
        {
            if (hips == null || spine == null || Mathf.Abs(legYaw) < 0.01f) return;
            // Applied after the Animator: hips (and legs) turn, the spine and chest turn back.
            Vector3 up = transform.up;
            hips.rotation = Quaternion.AngleAxis(legYaw, up) * hips.rotation;
            if (chest != null)
            {
                spine.rotation = Quaternion.AngleAxis(-legYaw * 0.5f, up) * spine.rotation;
                chest.rotation = Quaternion.AngleAxis(-legYaw * 0.5f, up) * chest.rotation;
            }
            else spine.rotation = Quaternion.AngleAxis(-legYaw, up) * spine.rotation;
        }

        private void TargetLocomotion(float speed)
        {
            if (speed <= walkSpeed)
            {
                float t = speed / walkSpeed;
                Target(CharacterAnimationSlot.Idle, 1 - t);
                Target(CharacterAnimationSlot.Walk, t);
            }
            else
            {
                float t = Mathf.Clamp01((speed - walkSpeed) / Mathf.Max(0.01f, runSpeed - walkSpeed));
                Target(CharacterAnimationSlot.Walk, 1 - t);
                Target(CharacterAnimationSlot.Run, t);
            }
        }

        // A missing clip falls back to idle so the character never snaps to its bind pose.
        private void Target(CharacterAnimationSlot slot, float weight)
        {
            if (!Has(slot)) slot = CharacterAnimationSlot.Idle;
            targets[(int)slot] += weight;
        }

        private bool Has(CharacterAnimationSlot slot) => clips[(int)slot] != null;

        private void Restart(CharacterAnimationSlot slot)
        {
            if (!Has(slot)) return;
            playables[(int)slot].SetTime(0);
            playables[(int)slot].SetTime(0);
        }

        private float Length(CharacterAnimationSlot slot) => Has(slot) ? Mathf.Max(0.01f, clips[(int)slot].length) : 1;

        private void SetPhase(CharacterAnimationSlot slot, float length)
        {
            if (Has(slot)) playables[(int)slot].SetTime(stridePhase * length);
        }
    }
}
