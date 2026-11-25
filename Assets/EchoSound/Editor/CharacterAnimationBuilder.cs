using System;
using System.Collections.Generic;
using EchoSound.Characters;
using UnityEditor;
using UnityEngine;

namespace EchoSound.Editor
{
    /// <summary>
    /// Generates the default humanoid clips. Poses are authored as bone rotations on the Worker rig
    /// and baked to muscle curves, so the clips retarget to any humanoid character.
    /// Runs automatically when the default set is incomplete.
    /// </summary>
    [InitializeOnLoad]
    public static class CharacterAnimationBuilder
    {
        private const string Folder = "Assets/EchoSound/Art/Animations";
        private const string SetPath = Folder + "/DefaultCharacterAnimations.asset";
        private const string RigPath = "Assets/EchoSound/Prefabs/Characters/Worker.prefab";
        private const float FrameRate = 30;
        // Bump when the poses change: outdated clips are rebuilt automatically (GUIDs are kept).
        private const string Version = "3";

        static CharacterAnimationBuilder() => EditorApplication.delayCall += BuildIfMissing;

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(SetPath);
            if (set != null)
            {
                bool complete = true;
                for (int i = 0; i < CharacterAnimationClips.SlotCount; i++)
                    complete &= set.clips[(CharacterAnimationSlot)i] != null;
                if (complete && AssetImporter.GetAtPath(SetPath).userData == Version) return;
            }
            Build();
        }

        [MenuItem("EchoSound/Animation/Rebuild default character animations")]
        public static void Build()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (prefab == null) { Debug.LogError("Character animations: rig not found at " + RigPath); return; }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/EchoSound/Art", "Animations");

            var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                var rig = new Rig(instance);
                set.clips.idle = Bake(rig, "Idle", 4f, true, t => Idle(rig, t));
                set.clips.walk = Bake(rig, "Walk", 0.55f, true, p => Stride(rig, p, false));
                set.clips.run = Bake(rig, "Run", 0.5f, true, p => Stride(rig, p, true));
                set.clips.jump = Bake(rig, "Jump", 0.5f, false, t => Jump(rig, t));
                set.clips.fall = Bake(rig, "Fall", 0.8f, true, t => Fall(rig, t));
                set.clips.land = Bake(rig, "Land", 0.4f, false, t => Land(rig, t));
                rig.Dispose();
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            var importer = AssetImporter.GetAtPath(SetPath);
            importer.userData = Version;
            importer.SaveAndReimport();
            Debug.Log("Character animations: 6 default clips baked into " + Folder);
        }

        // Pose functions receive normalised time t in [0, 1]. Arcade style: readable silhouettes,
        // snappy timing, exaggerated weight shift; feet are kept on the ground by Rig.Ground().
        private static void Idle(Rig rig, float t)
        {
            float a = t * Mathf.PI * 2, shift = Mathf.Sin(a), breath = Mathf.Sin(a * 2);
            rig.Shift(new Vector3(0.025f * shift, 0, 0));
            rig.Spine(2 + 1.5f * breath, 2 * Mathf.Sin(a + 1), -2.5f * shift);
            rig.Rot("Head", -1 - breath, 4 * Mathf.Sin(a + 1.5f), 2 * shift);
            rig.Arm(true, 70 + 2.5f * breath, 3 + 2 * Mathf.Sin(a + 0.6f), 14 + 3 * breath);
            rig.Arm(false, 70 + 2.5f * breath, 3 - 2 * Mathf.Sin(a + 0.6f), 14 + 3 * breath);
            // The unweighted leg relaxes; legs counter the sway so feet stay planted.
            float spread = Rig.CounterSway(0.025f * shift);
            float kneeL = 3 + 9 * Mathf.Max(0, shift), kneeR = 3 + 9 * Mathf.Max(0, -shift);
            rig.Leg(true, -kneeL * 0.4f, kneeL, -kneeL * 0.6f, spread);
            rig.Leg(false, -kneeR * 0.4f, kneeR, -kneeR * 0.6f, -spread);
            rig.Ground();
        }

        private static void Stride(Rig rig, float t, bool run)
        {
            float p = t * Mathf.PI * 2, sw = Mathf.Sin(p), cs = Mathf.Cos(p);
            float hipYaw = run ? 9 : 6, chestYaw = run ? 9 : 5, lean = run ? 15 : 4;
            float sway = run ? 0.012f : 0.03f;

            rig.Shift(new Vector3(sway * cs, 0, 0));
            rig.Rot("Hips", 0, hipYaw * sw, 0);
            // Shoulders counter-rotate against the pelvis; the head keeps looking ahead.
            rig.Spine(lean + (run ? 3 : 1.5f) * Mathf.Cos(p * 2), -(hipYaw + chestYaw) * sw, -(run ? 2 : 4) * cs);
            rig.Rot("Head", -lean * 0.6f + (run ? 2 : 1) * Mathf.Cos(p * 2), chestYaw * sw, 0);

            float spread = Rig.CounterSway(sway * cs);
            StrideLeg(rig, true, p, run, spread);
            StrideLeg(rig, false, p + Mathf.PI, run, -spread);

            // Arms trail the legs slightly and bend more on the forward swing.
            float arm = run ? 42 : 24, elbowBase = run ? 80 : 14, elbowExtra = run ? 20 : 22;
            float la = Shape(Mathf.Sin(p - 0.3f), 0.8f);
            rig.Arm(true, run ? 66 : 70, la * arm, elbowBase + elbowExtra * Mathf.Max(0, -la));
            rig.Arm(false, run ? 66 : 70, -la * arm, elbowBase + elbowExtra * Mathf.Max(0, la));

            rig.Ground();
            if (run) rig.Shift(new Vector3(0, 0.04f * sw * sw, 0)); // flight phase
        }

        // phase: the leg is furthest forward (heel strike) at PI/2 and furthest back (toe-off) at 3PI/2.
        private static void StrideLeg(Rig rig, bool left, float phase, bool run, float spread)
        {
            float forward = Shape(Mathf.Sin(phase), 0.8f);
            float swing = Mathf.Max(0, Mathf.Cos(phase));
            float thigh = forward > 0 ? (run ? 50 : 28) : (run ? 32 : 22);
            float knee = 4 + (run ? 115 : 55) * Mathf.Pow(swing, 1.3f)
                         + (run ? 18 : 10) * Bump(phase, Mathf.PI * 0.5f + 0.6f, 0.9f);
            float heel = Bump(phase, Mathf.PI * 0.5f, 0.7f);
            float toeOff = Bump(phase, Mathf.PI * 1.5f - 0.15f, 0.7f);
            float foot = -18 * heel + (run ? 38 : 26) * toeOff - 0.25f * knee * swing;
            rig.Leg(left, -forward * thigh, knee, foot, spread);
            rig.Toes(left, -(run ? 35 : 28) * toeOff);
        }

        private static void Jump(Rig rig, float t)
        {
            float e = EaseOutBack(Mathf.Clamp01(t / 0.6f));
            rig.Spine(-8 * e, 4 * e, 0);
            rig.Rot("Head", -6 * e);
            rig.Arm(true, 72 - 58 * e, -38 * e, 25);
            rig.Arm(false, 72 - 46 * e, -14 * e, 38);
            rig.Leg(true, -58 * e, 100 * e, 25 * e, 0);
            rig.Leg(false, 14 * e, 40 * e, 32 * e, 0);
        }

        private static void Fall(Rig rig, float t)
        {
            float a = t * Mathf.PI * 2, w = Mathf.Sin(a), w2 = Mathf.Cos(a);
            rig.Spine(6, 4 * w, 3 * w2);
            rig.Rot("Head", -12);
            rig.Arm(true, 14 + 14 * w, -15 + 16 * w2, 30 + 12 * w);
            rig.Arm(false, 14 - 14 * w, -15 - 16 * w2, 30 - 12 * w);
            rig.Leg(true, -32 + 14 * w, 52 + 18 * w2, 22, 4);
            rig.Leg(false, -12 - 14 * w, 40 - 18 * w2, 28, 4);
        }

        private static void Land(Rig rig, float t)
        {
            // Hard squash in the first fifth, then a springy recovery that slightly overshoots.
            const float impact = 0.18f;
            float s = t < impact ? 1 - Mathf.Pow(1 - t / impact, 2) : 1 - EaseOutBack((t - impact) / (1 - impact));
            float c = Mathf.Max(0, s);
            rig.Spine(28 * s, 0, 0);
            rig.Rot("Head", -22 * s);
            rig.Arm(true, 72 - 34 * c, -32 * c, 18 + 45 * c);
            rig.Arm(false, 72 - 28 * c, -24 * c, 18 + 40 * c);
            rig.Leg(true, -62 * c, 115 * c, -52 * c, 7 * c);
            rig.Leg(false, -62 * c, 115 * c, -52 * c, 7 * c);
            rig.Ground();
        }

        private static float Shape(float x, float k) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), k);

        /// <summary>Smooth 0..1 bump around <paramref name="center"/> (radians, wraps around 2PI).</summary>
        private static float Bump(float phase, float center, float width)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(center * Mathf.Rad2Deg, phase * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            return d >= width ? 0 : Mathf.Pow(Mathf.Cos(d / width * Mathf.PI * 0.5f), 2);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
        }

        private static AnimationClip Bake(Rig rig, string name, float duration, bool loop, Action<float> pose)
        {
            int frames = Mathf.Max(2, Mathf.RoundToInt(duration * FrameRate));
            var muscles = HumanTrait.MuscleName;
            var curves = new Dictionary<string, AnimationCurve>();
            var humanPose = new HumanPose();

            for (int f = 0; f <= frames; f++)
            {
                float t = (float)f / frames, time = t * duration;
                rig.Reset();
                pose(t);
                rig.Sample(ref humanPose);
                Key(curves, "RootT.x", time, humanPose.bodyPosition.x);
                Key(curves, "RootT.y", time, humanPose.bodyPosition.y);
                Key(curves, "RootT.z", time, humanPose.bodyPosition.z);
                Key(curves, "RootQ.x", time, humanPose.bodyRotation.x);
                Key(curves, "RootQ.y", time, humanPose.bodyRotation.y);
                Key(curves, "RootQ.z", time, humanPose.bodyRotation.z);
                Key(curves, "RootQ.w", time, humanPose.bodyRotation.w);
                for (int m = 0; m < muscles.Length; m++)
                {
                    // The rig has no fingers; finger curves use a different naming scheme anyway.
                    if (IsFinger(muscles[m])) continue;
                    Key(curves, muscles[m], time, humanPose.muscles[m]);
                }
            }

            var clip = new AnimationClip { name = name, frameRate = FrameRate };
            foreach (var pair in curves)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), pair.Key), pair.Value);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            // Bake root movement into the pose: the character's transform is moved by gameplay code.
            settings.loopBlendOrientation = settings.loopBlendPositionY = settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string path = Folder + "/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
            EditorUtility.CopySerialized(clip, existing); // keeps the GUID, so references survive rebuilds
            UnityEngine.Object.DestroyImmediate(clip);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static bool IsFinger(string muscle) =>
            muscle.Contains("Thumb") || muscle.Contains("Index") || muscle.Contains("Middle")
            || muscle.Contains("Ring") || muscle.Contains("Little");

        private static void Key(Dictionary<string, AnimationCurve> curves, string name, float time, float value)
        {
            if (!curves.TryGetValue(name, out var curve)) curves[name] = curve = new AnimationCurve();
            curve.AddKey(time, value);
        }

        /// <summary>
        /// Worker rig helper. Bones have identity rest rotations: +Z forward, +Y up, left arm along -X.
        /// Angles are in degrees; negative thigh = leg forward, positive knee = bend.
        /// </summary>
        private sealed class Rig : IDisposable
        {
            private readonly Transform root;
            private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
            private readonly Dictionary<Transform, (Quaternion rotation, Vector3 position)> rest =
                new Dictionary<Transform, (Quaternion, Vector3)>();
            private readonly HumanPoseHandler handler;
            private readonly float groundHeight;
            private static readonly string[] FootJoints = { "Foot.L", "Foot.R", "Toes.L", "Toes.R" };

            public Rig(GameObject instance)
            {
                root = instance.transform;
                foreach (var bone in instance.GetComponentsInChildren<Transform>())
                {
                    bones[bone.name] = bone;
                    rest[bone] = (bone.localRotation, bone.localPosition);
                }
                handler = new HumanPoseHandler(instance.GetComponent<Animator>().avatar, root);
                groundHeight = LowestFoot();
            }

            public void Reset()
            {
                foreach (var pair in rest) { pair.Key.localRotation = pair.Value.rotation; pair.Key.localPosition = pair.Value.position; }
            }

            public void Sample(ref HumanPose pose) => handler.GetHumanPose(ref pose);
            public void Dispose() => handler.Dispose();

            public void Rot(string bone, float x, float y = 0, float z = 0) =>
                bones[bone].localRotation = rest[bones[bone]].rotation * Quaternion.Euler(x, y, z);

            public void Spine(float lean, float twist = 0, float side = 0)
            {
                Rot("Spine", lean * 0.6f, twist * 0.4f, side * 0.6f);
                Rot("Chest", lean * 0.4f, twist * 0.6f, side * 0.4f);
            }

            /// <param name="spread">Positive moves the foot outward.</param>
            public void Leg(bool left, float thigh, float knee, float foot, float spread)
            {
                string s = left ? ".L" : ".R";
                float roll = left ? -spread : spread;
                Rot("UpperLeg" + s, thigh, 0, roll);
                Rot("LowerLeg" + s, knee);
                Rot("Foot" + s, foot, 0, -roll);
            }

            public void Toes(bool left, float angle) => Rot("Toes" + (left ? ".L" : ".R"), angle);

            /// <summary>Spread for the left leg (negate for the right) that keeps the feet in place while the hips move sideways by dx.</summary>
            public static float CounterSway(float dx) => Mathf.Asin(Mathf.Clamp(dx / LegLength, -1, 1)) * Mathf.Rad2Deg;
            private const float LegLength = 0.85f;

            /// <param name="down">Lowers the arm from the T-pose.</param>
            /// <param name="swing">Negative swings forward.</param>
            /// <param name="elbow">Positive bends the forearm forward.</param>
            public void Arm(bool left, float down, float swing, float elbow)
            {
                float side = left ? 1 : -1;
                string s = left ? ".L" : ".R";
                var upper = bones["UpperArm" + s];
                upper.localRotation = rest[upper].rotation
                    * Quaternion.AngleAxis(swing, Vector3.right) * Quaternion.AngleAxis(down * side, Vector3.forward);
                var lower = bones["LowerArm" + s];
                lower.localRotation = rest[lower].rotation * Quaternion.AngleAxis(elbow * side, Vector3.up);
            }

            public void Shift(Vector3 offset) => bones["Hips"].localPosition += offset;

            /// <summary>Moves the hips vertically so the lowest foot joint touches the rest-pose ground.</summary>
            public void Ground() => Shift(new Vector3(0, groundHeight - LowestFoot(), 0));

            private float LowestFoot()
            {
                float y = float.MaxValue;
                foreach (var name in FootJoints) y = Mathf.Min(y, bones[name].position.y);
                return y;
            }
        }
    }
}
