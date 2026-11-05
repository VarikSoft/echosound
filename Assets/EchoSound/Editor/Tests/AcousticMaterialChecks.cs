using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EchoSound.Sound;
using EchoSound.VFX;
using EchoSound.UI;

namespace EchoSound.Editor.Tests
{
    public static class AcousticMaterialChecks
    {
        private sealed class Listener : ISoundListener
        {
            public Vector3 ListenerPosition { get; set; }
            public float HearingSensitivity => 1;
            public int Count;
            public SoundEvent Last;
            public void OnSoundHeard(in SoundEvent sound, float distance) { Count++; Last = sound; }
        }

        [MenuItem("EchoSound/Checks/Validate acoustic materials")]
        public static void Validate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run checks outside Play mode.");
            Vector3 origin = Vector3.one * 1000;
            var metal = ScriptableObject.CreateInstance<AcousticMaterial>();
            var soft = ScriptableObject.CreateInstance<AcousticMaterial>();
            metal.reflection = 0.95f; metal.reflectionDamping = 0.005f; metal.contactLoudness = 1.3f;
            soft.reflection = 0.12f; soft.reflectionDamping = 0.3f; soft.contactLoudness = 0.2f;
            GameObject wall = Box("Material test wall", origin + Vector3.right * 3, new Vector3(0.2f, 30, 30));
            SoundBus.Reset();
            try
            {
                Physics.SyncTransforms();
                var sound = new SoundEvent(origin, 12, 1);
                var field = new AcousticField(sound, 12);
                Require(new AcousticReflections(sound, field, Physics.DefaultRaycastLayers).Count == 0, "Untagged geometry must keep legacy behaviour.");
                var surface = wall.AddComponent<AcousticSurface>();
                surface.profile = metal;
                var reflections = new AcousticReflections(sound, field, Physics.DefaultRaycastLayers);
                Vector3 target = origin + Vector3.forward * 2;
                Require(reflections.TryGetEcho(target, out float path, out float metalPressure), "Metal wall must reflect.");
                Require(path > 6 && path < 6.3f, "First bounce must follow the image-source path.");
                Require(!reflections.TryGetEcho(origin + Vector3.right * 5, out _, out _), "Reflection must not cross the wall.");
                surface.profile = soft;
                var absorbed = new AcousticReflections(sound, field, Physics.DefaultRaycastLayers);
                Require(absorbed.TryGetEcho(target, out _, out float softPressure) && softPressure < metalPressure * 0.1f,
                    "Soft walls must lose substantially more reflected pressure.");
                Require(AcousticSurface.ContactGain(wall.GetComponent<Collider>()) == 0.2f, "Contact sounds must use the profile.");
                Require(soft.ReflectionGain(10) < soft.ReflectionGain(2), "Damping must grow with reflected path length.");

                surface.profile = metal;
                var listener = new Listener { ListenerPosition = target };
                SoundBus.RegisterListener(listener);
                SoundBus.Emit(sound);
                SoundBus.Advance(sound.Timestamp + 0.008f);
                Require(listener.Count == 1 && !listener.Last.IsReflection, "Direct sound must arrive before its echo.");
                SoundBus.Advance(sound.Timestamp + 0.025f);
                Require(listener.Count == 2 && listener.Last.IsReflection, "Metal echo must arrive separately, at physical travel time.");
                Require(listener.Last.Timestamp == sound.Timestamp && listener.Last.Intensity < sound.Intensity, "Echo must preserve source context and lose energy.");
                SoundBus.Advance(sound.Timestamp + 1);
                Require(listener.Count == 2, "Do not redeliver an echo.");
                SoundBus.UnregisterListener(listener);

                wall.transform.position = origin + Vector3.down * 2;
                wall.transform.localScale = new Vector3(30, 0.2f, 30);
                Physics.SyncTransforms();
                field = new AcousticField(sound, 12);
                reflections = new AcousticReflections(sound, field, Physics.DefaultRaycastLayers);
                Require(reflections.TryGetEcho(target, out path, out metalPressure) && path > 4, "Floor must reflect using the same model.");
                surface.profile = soft;
                absorbed = new AcousticReflections(sound, field, Physics.DefaultRaycastLayers);
                Require(absorbed.TryGetEcho(target, out _, out softPressure) && softPressure < metalPressure * 0.15f, "Soft flooring must absorb reflections.");
                Debug.Log("Acoustic material checks: wall/floor reflections, damping, contacts and delayed delivery passed.");
            }
            finally
            {
                SoundBus.Reset();
                UnityEngine.Object.DestroyImmediate(wall);
                UnityEngine.Object.DestroyImmediate(metal);
                UnityEngine.Object.DestroyImmediate(soft);
            }
        }

        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Validate();
                AcousticChecks.Validate();
                SoundBusChecks.ValidateSoundBus();
                VoiceChecks.Validate();
                EcholocationChecks.ValidateEcholocationVFX();
                ValidateRendering();
                ValidateRoomAssignments();
                foreach (string name in new[] { "EchoSound/Echolocation", "EchoSound/EcholocationSurface", "Hidden/EchoSound/AcousticResponse" })
                {
                    Shader shader = Shader.Find(name);
                    Require(shader != null && shader.isSupported, "Shader unsupported: " + name);
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        Require(message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, message.message);
                }
                Debug.Log("ACOUSTIC_MATERIAL_CHECKS_PASSED");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        private static void ValidateRendering()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SoundBus.Reset();
            var metal = ScriptableObject.CreateInstance<AcousticMaterial>();
            metal.reflection = 0.95f; metal.echoPersistence = 1;
            metal.kind = AcousticMaterialKind.Metal; metal.displayName = "Metal"; metal.playerHint = "Strong echoes";
            var soft = ScriptableObject.CreateInstance<AcousticMaterial>();
            soft.reflection = 0.15f; soft.echoPersistence = 0.2f;
            soft.kind = AcousticMaterialKind.Soft;
            var left = Box("Reflective sample", new Vector3(-2, 1.5f, 6), new Vector3(3, 3, 0.2f)).AddComponent<AcousticSurface>();
            var right = Box("Absorbing sample", new Vector3(2, 1.5f, 6), new Vector3(3, 3, 0.2f)).AddComponent<AcousticSurface>();
            left.profile = metal; right.profile = soft;
            var camera = new GameObject("Material preview camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.5f, 0);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.gameObject.AddComponent<EcholocationEffect>().bloomStrength = 0;
            var manager = new GameObject("Material preview echo").AddComponent<EcholocationManager>();
            manager.config.waveSpeed = 10;
            manager.config.trailDecayDuration = 2;
            manager.config.surfaceTint = 1;
            var target = new RenderTexture(640, 360, 24);
            var capture = new Texture2D(640, 360, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            try
            {
                SoundBus.Emit(camera.transform.position, 20, 1);
                manager.SendMessage("Update");
                float start = Shader.GetGlobalVectorArray("_EchoPulseParams")[0].x;
                Vector3 leftPixel = camera.WorldToScreenPoint(left.transform.position);
                Vector3 rightPixel = camera.WorldToScreenPoint(right.transform.position);
                Color Sample(Vector3 pixel) => capture.GetPixel((int)pixel.x, (int)pixel.y);
                void Render(float age, string filename)
                {
                    Shader.SetGlobalFloat("_EchoTime", start + age);
                    camera.Render();
                    RenderTexture.active = target;
                    capture.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                    capture.Apply();
                    Directory.CreateDirectory("Logs/MaterialChecks");
                    File.WriteAllBytes("Logs/MaterialChecks/" + filename, capture.EncodeToPNG());
                }
                Render(0.9f, "early.png");
                Require(Sample(leftPixel).maxColorComponent > Sample(rightPixel).maxColorComponent * 2 + 0.02f,
                    "Reflective wall must visibly exceed the soft wall under the same pulse.");
                Render(1.6f, "late.png");
                Require(Sample(leftPixel).maxColorComponent > 0.02f && Sample(rightPixel).maxColorComponent < 0.015f,
                    "Soft surface memory must expire earlier.");
                var readout = camera.gameObject.AddComponent<AcousticMaterialReadout>();
                readout.readDistance = 10;
                camera.transform.LookAt(left.transform.position);
                Physics.SyncTransforms();
                readout.Refresh(start + 0.1f);
                Require(readout.CurrentLabel == "", "Material name must stay hidden before pulse arrival.");
                readout.Refresh(start + 0.9f);
                Require(readout.CurrentLabel.Contains("Metal"), "Revealed aimed material must identify itself.");
                readout.Refresh(start + 10);
                Require(readout.CurrentLabel == "", "Material name must disappear after the echo fades.");
                var occluder = Box("Readout occluder", Vector3.Lerp(camera.transform.position, left.transform.position, 0.5f), Vector3.one);
                Physics.SyncTransforms();
                readout.Refresh(start + 0.9f);
                Require(readout.CurrentLabel == "", "The material readout must not see through an unmarked object.");
                UnityEngine.Object.DestroyImmediate(occluder);
                camera.transform.rotation = Quaternion.identity;
                left.enabled = right.enabled = false;
                Render(0.9f, "neutral.png");
                Require(Mathf.Abs(Sample(leftPixel).maxColorComponent - Sample(rightPixel).maxColorComponent) < 0.03f,
                    "Removing profiles must restore neutral rendering.");
                left.enabled = right.enabled = true;
                manager.ClearAllPulses();
                readout.Refresh();
                Require(readout.CurrentLabel == "", "No pulse must mean no material readout.");
                Render(0, "dark.png");
                foreach (Color32 pixel in capture.GetPixels32())
                    Require(pixel.r <= 1 && pixel.g <= 1 && pixel.b <= 1, "Material markings must not glow in darkness.");
                Debug.Log("Acoustic material render checks passed.");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
                UnityEngine.Object.DestroyImmediate(left.gameObject);
                UnityEngine.Object.DestroyImmediate(right.gameObject);
                UnityEngine.Object.DestroyImmediate(metal);
                UnityEngine.Object.DestroyImmediate(soft);
                UnityEngine.Object.DestroyImmediate(capture);
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
                SoundBus.Reset();
            }
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name; box.transform.position = position; box.transform.localScale = size;
            return box;
        }
        private static void ValidateRoomAssignments()
        {
            foreach (string path in Directory.GetFiles("Assets/EchoSound/Prefabs/Rooms", "*.prefab"))
            {
                var room = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool special = room.name == "Room_SecretVault" || room.name == "Room_Ambush";
                int count = room.GetComponentsInChildren<AcousticSurface>(true).Length;
                Require(special ? count > 0 : count == 0, "Unexpected acoustic material assignment: " + room.name);
            }
            Debug.Log("Selective acoustic checks: only SecretVault and Ambush have material overrides.");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
