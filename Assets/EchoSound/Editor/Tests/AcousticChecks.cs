using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EchoSound.Sound;
using EchoSound.VFX;

namespace EchoSound.Editor.Tests
{
    public static class AcousticChecks
    {
        private sealed class Listener : ISoundListener
        {
            public Vector3 ListenerPosition { get; set; }
            public float HearingSensitivity => 1f;
            public int Count;
            public SoundEvent Last;
            public void OnSoundHeard(in SoundEvent sound, float distance) { Count++; Last = sound; }
        }

        [MenuItem("EchoSound/Checks/Validate acoustic propagation")]
        public static void Validate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run acoustic checks outside Play mode.");
            Vector3 origin = new Vector3(1000, 1000, 1000);
            GameObject wall = null, doorSide = null;
            SoundBus.Reset();
            try
            {
                var sound = new SoundEvent(origin, 12f, 1f);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var open = new AcousticField(sound, 12f);
                watch.Stop();
                Debug.Log($"Acoustic field: {watch.Elapsed.TotalMilliseconds:F1} ms (12m, {AcousticField.CellCount} bounded cells).");
                Require(Mathf.Abs(open.PathDistance(origin + Vector3.right * 5) - 5) < 0.01f, "Direct path must equal geometric distance.");
                Require(AcousticField.Pressure(8, 8) < AcousticField.Pressure(4, 4) * 0.51f, "Pressure must decay with distance.");
                Require(AcousticField.Pressure(8, 4) < AcousticField.Pressure(8, 8), "Bent paths must lose extra energy.");

                wall = Box("Acoustic test wall", origin + Vector3.right * 2, new Vector3(0.3f, 30, 30));
                Physics.SyncTransforms();
                var blocked = new AcousticField(sound, 12f);
                Require(blocked.PathDistance(origin + Vector3.right * 4) >= AcousticField.Unreachable, "Sealed wall must block sound.");
                wall.transform.position = origin + Vector3.right * 2.17f;
                wall.transform.localScale = new Vector3(0.05f, 30, 30);
                Physics.SyncTransforms();
                var thinWall = new AcousticField(sound, 12f);
                Require(thinWall.PathDistance(origin + Vector3.right * 4) >= AcousticField.Unreachable,
                    "A thin wall between grid nodes must block connections.");
                wall.transform.position = origin + Vector3.right * 2;
                wall.transform.localScale = new Vector3(0.3f, 30, 4);
                Physics.SyncTransforms();
                var detour = new AcousticField(sound, 12f);
                float bent = detour.PathDistance(origin + Vector3.right * 4);
                Require(bent > 5f && bent < 12f, $"Finite wall must allow a longer route around its edge: {bent}.");
                wall.transform.position = origin + new Vector3(2.17f, 0, -8.3f);
                wall.transform.localScale = new Vector3(0.1f, 60, 15.4f);
                doorSide = Box("Narrow doorway side", origin + new Vector3(2.17f, 0, 8.5f), new Vector3(0.1f, 60, 15));
                Physics.SyncTransforms();
                var doorway = new AcousticField(sound, 24f);
                int middle = AcousticField.Size / 2;
                int acrossDoor = middle + 3 + AcousticField.Size * (middle + AcousticField.Size * middle);
                Require(doorway.Distances[acrossDoor] < 5f, "A 1.6m clear opening must not be closed by inflated voxels.");
                doorSide.SetActive(false);
                wall.SetActive(false);
                Physics.SyncTransforms();
                var reopened = new AcousticField(sound, 12f);
                Require(reopened.PathDistance(origin + Vector3.right * 4) == 4f, "New pulses must see opened doors.");

                var listener = new Listener { ListenerPosition = origin + Vector3.right * 5 };
                SoundBus.RegisterListener(listener);
                SoundBus.Emit(sound);
                SoundBus.Advance(sound.Timestamp + 0.01f);
                Require(listener.Count == 0, "5m arrival must take longer than 10ms.");
                SoundBus.Advance(sound.Timestamp + 0.02f);
                Require(listener.Count == 1, "5m arrival must occur within 20ms.");
                Require(listener.Last.Intensity < sound.Intensity && listener.Last.Timestamp == sound.Timestamp, "Received event must attenuate and preserve emission time.");
                SoundBus.Advance(sound.Timestamp + 1f);
                Require(listener.Count == 1, "One wave must not deliver twice.");
                SoundBus.Emit(origin, 12, 0);
                SoundBus.Advance(Time.time + 1);
                Require(listener.Count == 1, "Silent events must not be heard.");
                SoundBus.Emit(sound);
                SoundBus.UnregisterListener(listener);
                SoundBus.RegisterListener(listener);
                SoundBus.Advance(Time.time + 1);
                Require(listener.Count == 1, "Unregister must cancel pending deliveries.");
                Debug.Log("Acoustic checks: all tests passed.");
            }
            finally
            {
                if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
                if (doorSide != null) UnityEngine.Object.DestroyImmediate(doorSide);
                SoundBus.Reset();
            }
        }

        // Entry point for Unity -batchmode -executeMethod; output stays in ignored Logs.
        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Validate();
                VoiceChecks.Validate();
                SoundBusChecks.ValidateSoundBus();
                EcholocationChecks.ValidateEcholocationVFX();
                CapturePreview();
                CaptureGeneratedPreview();
                foreach (string name in new[] { "EchoSound/Echolocation", "EchoSound/EcholocationSurface" })
                {
                    Shader shader = Shader.Find(name);
                    Require(shader != null && shader.isSupported, name + " is unsupported.");
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        Require(message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, message.message);
                }
                Debug.Log("ACOUSTIC_BATCH_SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static GameObject Box(string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            return go;
        }

        private static void CapturePreview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Box("Floor", new Vector3(0, -0.25f, 5), new Vector3(14, 0.5f, 22));
            Box("Left wall", new Vector3(-6, 2, 5), new Vector3(0.4f, 4, 22));
            Box("Right wall", new Vector3(6, 2, 5), new Vector3(0.4f, 4, 22));
            Box("Back wall left", new Vector3(-3.7f, 2, 12), new Vector3(4.6f, 4, 0.4f));
            Box("Back wall right", new Vector3(3.7f, 2, 12), new Vector3(4.6f, 4, 0.4f));
            Box("Door lintel", new Vector3(0, 3.6f, 12), new Vector3(2.8f, 0.8f, 0.4f));
            Box("Pillar", new Vector3(2, 1.8f, 5), new Vector3(0.8f, 3.6f, 0.8f));
            Box("Low block", new Vector3(-2.5f, 0.7f, 4), new Vector3(1.7f, 1.4f, 1.7f));
            var camera = new GameObject("Preview camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.7f, -4);
            camera.transform.rotation = Quaternion.Euler(8, 0, 0);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.gameObject.AddComponent<EcholocationEffect>();
            var manager = new GameObject("Echo manager").AddComponent<EcholocationManager>();
            Physics.SyncTransforms();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            SoundBus.Emit(new Vector3(0, 0.12f, -1), 20, 1.2f, SoundType.FootstepWalk);
            SoundBus.Emit(new Vector3(3, 1.6f, 2), 18, 1f, SoundType.Voice);
            watch.Stop();
            Debug.Log($"Acoustic room field: {watch.Elapsed.TotalMilliseconds:F1} ms.");
            manager.SendMessage("Update");
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            Shader.SetGlobalFloat("_EchoTime", Time.realtimeSinceStartup + 0.55f);
            camera.Render();
            RenderTexture.active = target;
            var capture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            capture.Apply();
            Directory.CreateDirectory("Logs/AcousticChecks");
            File.WriteAllBytes("Logs/AcousticChecks/echo-preview.png", capture.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static void CaptureGeneratedPreview()
        {
            EditorSceneManager.OpenScene("Assets/EchoSound/Scenes/GenerationTest.unity");
            var generator = UnityEngine.Object.FindFirstObjectByType<Generation.LevelGenerator>();
            generator.Generate(generator.seed);
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonPlayer>();
            Vector3 spawn = generator.Current.Spawn;
            var door = generator.Current.Rooms[0].GetComponentInChildren<Rooms.RoomDoor>();
            var camera = player.view;
            camera.transform.position = spawn + Vector3.up * 1.7f;
            camera.transform.LookAt(door.transform.position + Vector3.up * 1.2f);
            if (camera.GetComponent<EcholocationEffect>() == null) camera.gameObject.AddComponent<EcholocationEffect>();
            var manager = new GameObject("Generated preview echo").AddComponent<EcholocationManager>();
            Physics.SyncTransforms();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            SoundBus.Emit(spawn + Vector3.up * 0.1f, 14, 0.6f, SoundType.FootstepWalk, player.gameObject);
            watch.Stop();
            Debug.Log($"Generated level acoustic field: {watch.Elapsed.TotalMilliseconds:F1} ms.");
            manager.SendMessage("Update");
            Shader.SetGlobalFloat("_EchoTime", Time.realtimeSinceStartup + 0.65f);
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var capture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            capture.Apply();
            File.WriteAllBytes("Logs/AcousticChecks/generated-preview.png", capture.EncodeToPNG());
            float pulseStart = Shader.GetGlobalVectorArray("_EchoPulseParams")[0].x;
            float[] ages = { 0f, 0.18f, 0.35f, 0.65f, 1.4f, 5f };
            float initialBrightness = 0f, revealedBrightness = 0f;
            for (int frameIndex = 0; frameIndex < ages.Length; frameIndex++)
            {
                Shader.SetGlobalFloat("_EchoTime", pulseStart + ages[frameIndex]);
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                capture.Apply();
                File.WriteAllBytes($"Logs/AcousticChecks/arrival-{frameIndex}.png", capture.EncodeToPNG());
                if (frameIndex == 2)
                    Require(capture.GetPixel(470, 330).maxColorComponent < 0.03f,
                        "The far doorway must stay dark before the sphere arrives (350ms).");
                if (frameIndex == 3)
                    Require(capture.GetPixel(470, 330).maxColorComponent > 0.1f,
                        "The far doorway must be revealed after the sphere arrives (650ms).");
                Color32[] pixels = capture.GetPixels32();
                double sum = 0;
                int coloredPixels = 0;
                foreach (Color32 pixel in pixels)
                {
                    if (Math.Abs(pixel.r - pixel.b) > 20) coloredPixels++;
                    sum += (pixel.r + pixel.g + pixel.b) / 765.0;
                }
                float brightness = (float)(sum / pixels.Length);
                if (frameIndex == 0) initialBrightness = brightness;
                if (frameIndex == 3) revealedBrightness = brightness;
                if (frameIndex == 3) Require(coloredPixels > 1000, "Source colors must appear in the rendered scan.");
                if (frameIndex == 5)
                {
                    Require(revealedBrightness > initialBrightness + 0.05f, "Arriving sound must visibly reveal original scene materials.");
                    Require(brightness < initialBrightness + 0.01f, "Surface memory must fade back into darkness.");
                }
            }
            // Render the same surface under pure red, pure blue, and overlapping spheres.
            var mixOrigins = Shader.GetGlobalVectorArray("_EchoPulseOrigins");
            var mixParams = Shader.GetGlobalVectorArray("_EchoPulseParams");
            var mixColors = Shader.GetGlobalVectorArray("_EchoPulseColors");
            Shader.SetGlobalFloat("_EchoTime", pulseStart + 0.65f);
            Color SampleScan()
            {
                Shader.SetGlobalVectorArray("_EchoPulseColors", mixColors);
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                capture.Apply();
                return capture.GetPixel(470, 330);
            }
            Shader.SetGlobalInt("_EchoPulseCount", 1);
            mixColors[0] = new Vector4(1,0,0,0.65f);
            Color red = SampleScan();
            mixColors[0] = new Vector4(0,0,1,0.65f);
            Color blue = SampleScan();
            mixOrigins[1] = mixOrigins[0];
            mixParams[1] = mixParams[0];
            mixColors[0] = new Vector4(1,0,0,0.65f);
            mixColors[1] = new Vector4(0,0,1,0.65f);
            Shader.SetGlobalVectorArray("_EchoPulseOrigins", mixOrigins);
            Shader.SetGlobalVectorArray("_EchoPulseParams", mixParams);
            Shader.SetGlobalInt("_EchoPulseCount", 2);
            Color mixed = SampleScan();
            Require(red.r > 0.1f && blue.b > 0.1f && mixed.r > blue.r + 0.05f && mixed.b > red.b + 0.05f,
                "Overlapping red and blue spheres must mix on the GPU.");
            File.WriteAllBytes("Logs/AcousticChecks/color-mixing.png", capture.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Acoustic check: " + message);
        }
    }
}
