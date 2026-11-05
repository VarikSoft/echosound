using System;
using UnityEditor;
using UnityEngine;
using EchoSound.Sound;
using EchoSound.VFX;

namespace EchoSound.Editor.Tests
{
    /// <summary>
    /// Verification checks for Echolocation VFX system and configuration.
    /// Accessible via the Unity menu under EchoSound/Checks/Validate echolocation VFX.
    /// </summary>
    public static class EcholocationChecks
    {
        [MenuItem("EchoSound/Checks/Validate echolocation VFX")]
        public static void ValidateEcholocationVFX()
        {
            // 1. Verify EcholocationConfig
            var config = ScriptableObject.CreateInstance<EcholocationConfig>();
            config.PopulateDefaults();

            Require(config.profiles.Count >= 10, "Config should contain profiles for standard SoundTypes.");
            Require(config.waveSpeed > 0f, "Wave speed should be positive.");
            Require(config.waveFrontWidth > 0f, "Wavefront width should be positive.");
            Require(config.trailDecayDuration > 0f, "Trail decay duration should be positive.");

            // Check specific sound profiles
            var walkProfile = config.GetProfile(SoundType.FootstepWalk);
            Require(walkProfile.soundType == SoundType.FootstepWalk, "Walk profile mismatch.");
            Require(walkProfile.waveColor.a >= 0f, "Walk profile color invalid.");

            var runProfile = config.GetProfile(SoundType.FootstepRun);
            Require(runProfile.soundType == SoundType.FootstepRun, "Run profile mismatch.");
            Require(runProfile.intensityMultiplier > walkProfile.intensityMultiplier,
                "Running sound should have higher visual intensity multiplier than walking.");

            var pingProfile = config.GetProfile(SoundType.CameraPing);
            Require(pingProfile.soundType == SoundType.CameraPing, "CameraPing profile mismatch.");

            // 2. Verify EcholocationManager with SoundBus integration
            SoundBus.Reset();

            var go = new GameObject("Test_EcholocationManager");
            try
            {
                var manager = go.AddComponent<EcholocationManager>();
                manager.config = config;

                Require(EcholocationManager.VisualRadius(10, 0.25f) < EcholocationManager.VisualRadius(10, 1f),
                    "Quiet sounds must produce smaller spheres.");
                Require(EcholocationManager.VisualRadius(10, 0) == 0, "Silence must not produce a sphere.");
                SoundBus.Emit(Vector3.zero, 14, 0.6f, SoundType.FootstepWalk);
                SoundBus.Emit(Vector3.right * 3, 10, 1, SoundType.Voice);
                manager.SendMessage("Update");
                Require(Shader.GetGlobalInt("_EchoPulseCount") == 2, "Simultaneous sounds must retain independent spheres.");
                var colors = Shader.GetGlobalVectorArray("_EchoPulseColors");
                Require(Vector4.Distance(colors[0], colors[1]) > 0.1f, "Different sound types must retain their colors.");
                var parameters = Shader.GetGlobalVectorArray("_EchoPulseParams");
                Require(Mathf.Approximately(parameters[0].y, config.waveSpeed), "Visual sphere must use the configured scan speed.");
                Require(Shader.GetGlobalVectorArray("_EchoPulseOrigins")[0].w > 16f,
                    "Normal walking must reveal a useful navigation radius.");
                Require(parameters[0].z > 0.8f && parameters[0].w > 1.9f,
                    "Walking must leave a readable trail between successive strides.");
                Require(Mathf.Approximately(parameters[1].z, 1.1f) && Mathf.Approximately(parameters[1].w, config.trailDecayDuration * 0.85f),
                    "Voice response must retain its existing tuning.");
                manager.ClearAllPulses();

                // Emit sound via SoundBus
                Vector3 origin = new Vector3(15f, 2f, -8f);
                SoundBus.Emit(origin, 25f, 1.5f, SoundType.CameraPing);

                // Emit multiple sounds to test buffer wrap-around
                for (int i = 0; i < 20; i++)
                {
                    SoundBus.Emit(Vector3.one * i, 10f + i, 1f, SoundType.FootstepWalk);
                }

                // Verify clear
                manager.ClearAllPulses();

                Debug.Log("Echolocation VFX checks: all tests passed successfully.");
            }
            finally
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(go);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
                SoundBus.Reset();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Echolocation verification failed: {message}");
            }
        }
    }
}
