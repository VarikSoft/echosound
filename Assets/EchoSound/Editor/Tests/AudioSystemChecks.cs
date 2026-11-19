using System;
using UnityEditor;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Editor.Tests
{
    /// <summary>
    /// Verification checks for SoundCue evaluation, AudioManager pooling, cooldown logic,
    /// SurfaceAudioProfile material routing, and AudioMixer decibel conversions.
    /// Accessible via EchoSound/Checks/Validate audio system.
    /// </summary>
    public static class AudioSystemChecks
    {
        [MenuItem("EchoSound/Checks/Validate audio system")]
        public static void ValidateAudioSystem()
        {
            TestSoundCueEvaluation();
            TestSoundCueCooldown();
            TestAudioManagerPooling();
            TestSurfaceAudioProfileMapping();
            TestVolumeAndDecibelConversions();
            TestConfiguredFootstepAssets();
            TestConfiguredDoorAssets();
            Debug.Log("AudioSystem checks: all tests passed successfully.");
        }

        private static void TestSoundCueEvaluation()
        {
            var cue = ScriptableObject.CreateInstance<SoundCue>();
            cue.baseVolume = 0.8f;
            cue.volumeVariation = new Vector2(0.9f, 1.1f);
            cue.basePitch = 1.0f;
            cue.pitchVariation = new Vector2(0.95f, 1.05f);

            // Null / empty clips
            cue.clips = Array.Empty<AudioClip>();
            Require(cue.SelectClip() == null, "Empty SoundCue must return null clip.");

            // Sequential selection test
            var dummy1 = AudioClip.Create("Dummy1", 44100, 1, 44100, false);
            var dummy2 = AudioClip.Create("Dummy2", 44100, 1, 44100, false);
            cue.clips = new[] { dummy1, dummy2 };
            cue.selectionMode = ClipSelectionMode.Sequential;

            Require(cue.SelectClip() == dummy1, "Sequential mode should return first clip.");
            Require(cue.SelectClip() == dummy2, "Sequential mode should return second clip.");
            Require(cue.SelectClip() == dummy1, "Sequential mode should loop back to first clip.");

            // Evaluation bounds check
            for (int i = 0; i < 20; i++)
            {
                var (clip, vol, pitch) = cue.EvaluatePlayback();
                Require(clip != null, "Evaluated clip should not be null.");
                Require(vol >= 0.71f && vol <= 0.89f, $"Volume {vol} should be within [0.72, 0.88] roughly.");
                Require(pitch >= 0.94f && pitch <= 1.06f, $"Pitch {pitch} should be within [0.95, 1.05] roughly.");
            }

            UnityEngine.Object.DestroyImmediate(dummy1);
            UnityEngine.Object.DestroyImmediate(dummy2);
            UnityEngine.Object.DestroyImmediate(cue);
        }

        private static void TestSoundCueCooldown()
        {
            var cue = ScriptableObject.CreateInstance<SoundCue>();
            cue.cooldown = 0.5f;

            Require(cue.CanPlay(1.0f), "Initial play at t=1.0s should be allowed.");
            Require(!cue.CanPlay(1.2f), "Play at t=1.2s should be throttled by 0.5s cooldown.");
            Require(!cue.CanPlay(1.49f), "Play at t=1.49s should be throttled by 0.5s cooldown.");
            Require(cue.CanPlay(1.51f), "Play at t=1.51s should be allowed after cooldown expires.");

            UnityEngine.Object.DestroyImmediate(cue);
        }

        private static void TestAudioManagerPooling()
        {
            var managerGo = new GameObject("TestAudioManager");
            var manager = managerGo.AddComponent<AudioManager>();
            manager.WarmPool(4);

            Require(manager.AvailableCount == 4, $"Expected 4 pooled sources, got {manager.AvailableCount}");
            Require(manager.ActiveCount == 0, $"Expected 0 active sources, got {manager.ActiveCount}");

            var dummyClip = AudioClip.Create("TestClip", 4410, 1, 44100, false);

            var source1 = manager.PlayClip(dummyClip, Vector3.zero);
            Require(source1 != null, "PlayClip should return a valid AudioSource.");
            Require(manager.ActiveCount == 1, $"Active count should be 1, got {manager.ActiveCount}");
            Require(manager.AvailableCount == 3, $"Available count should be 3, got {manager.AvailableCount}");

            manager.StopAll();
            Require(manager.ActiveCount == 0, $"After StopAll, active count should be 0, got {manager.ActiveCount}");
            Require(manager.AvailableCount == 4, $"After StopAll, available count should be 4, got {manager.AvailableCount}");

            manager.ResetState();
            UnityEngine.Object.DestroyImmediate(dummyClip);
            UnityEngine.Object.DestroyImmediate(managerGo);
        }

        private static void TestSurfaceAudioProfileMapping()
        {
            var profile = ScriptableObject.CreateInstance<SurfaceAudioProfile>();

            var defaultWalk = ScriptableObject.CreateInstance<SoundCue>();
            var defaultRun = ScriptableObject.CreateInstance<SoundCue>();
            var metalWalk = ScriptableObject.CreateInstance<SoundCue>();
            var metalImpact = ScriptableObject.CreateInstance<SoundCue>();

            profile.defaultWalkCue = defaultWalk;
            profile.defaultRunCue = defaultRun;

            profile.surfaceSets = new[]
            {
                new SurfaceAudioProfile.SurfaceSoundSet
                {
                    material = AcousticMaterialKind.Metal,
                    walkCue = metalWalk,
                    impactCue = metalImpact
                }
            };

            // Test specific material mapping
            Require(profile.GetFootstepCue(AcousticMaterialKind.Metal, false) == metalWalk,
                "Metal walk should return metalWalk cue.");
            // If run is null on metal, fallback to defaultRun
            Require(profile.GetFootstepCue(AcousticMaterialKind.Metal, true) == defaultRun,
                "Unset metal run should fallback to defaultRun cue.");
            Require(profile.GetImpactCue(AcousticMaterialKind.Metal) == metalImpact,
                "Metal impact should return metalImpact cue.");

            // Test unmapped material fallback
            Require(profile.GetFootstepCue(AcousticMaterialKind.Concrete, false) == defaultWalk,
                "Unmapped Concrete walk should fallback to defaultWalk cue.");
            Require(profile.GetFootstepCue(AcousticMaterialKind.Unmarked, true) == defaultRun,
                "Unmarked run should fallback to defaultRun cue.");

            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(defaultWalk);
            UnityEngine.Object.DestroyImmediate(defaultRun);
            UnityEngine.Object.DestroyImmediate(metalWalk);
            UnityEngine.Object.DestroyImmediate(metalImpact);
        }

        private static void TestVolumeAndDecibelConversions()
        {
            Require(Mathf.Approximately(AudioManager.LinearToDecibels(1.0f), 0f), "Linear 1.0 must be 0 dB.");
            Require(Mathf.Approximately(AudioManager.LinearToDecibels(0.0f), -80f), "Linear 0.0 must be -80 dB.");
            Require(Mathf.Abs(AudioManager.LinearToDecibels(0.5f) - (-6.02f)) < 0.1f, "Linear 0.5 must be approx -6 dB.");

            Require(Mathf.Approximately(AudioManager.DecibelsToLinear(0f), 1.0f), "0 dB must be linear 1.0.");
            Require(Mathf.Approximately(AudioManager.DecibelsToLinear(-80f), 0.0f), "-80 dB must be linear 0.0.");

            var go = new GameObject("TestVolumeMgr");
            var mgr = go.AddComponent<AudioManager>();

            float lastSfxEvent = -1f;
            mgr.OnSFXVolumeChanged += v => lastSfxEvent = v;

            mgr.SetSFXVolume(0.75f);
            Require(Mathf.Approximately(mgr.SFXVolume, 0.75f), "SFXVolume should be 0.75");
            Require(Mathf.Approximately(lastSfxEvent, 0.75f), "OnSFXVolumeChanged should fire with 0.75");

            mgr.SetMuted(true);
            Require(mgr.IsMuted == true, "IsMuted should be true.");

            mgr.ResetState();
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void TestConfiguredFootstepAssets()
        {
            const string profilePath = "Assets/EchoSound/Settings/Audio/PlayerFootstepProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<SurfaceAudioProfile>(profilePath);
            if (profile != null)
            {
                Require(profile.defaultWalkCue != null, "Profile must have defaultWalkCue.");
                Require(profile.defaultRunCue != null, "Profile must have defaultRunCue.");
                Require(profile.surfaceSets != null && profile.surfaceSets.Length >= 4, "Profile must cover at least 4 material sets.");

                // Validate player prefab wiring
                const string playerPrefabPath = "Assets/EchoSound/Prefabs/Player/FirstPersonPlayer.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
                Require(prefab != null, "FirstPersonPlayer prefab must exist.");
                var emitter = prefab.GetComponent<PlayerSoundEmitter>();
                Require(emitter != null, "PlayerSoundEmitter component must be present on FirstPersonPlayer prefab.");
                Require(emitter.surfaceAudio != null, "PlayerSoundEmitter.surfaceAudio must be wired on FirstPersonPlayer prefab.");
            }
        }

        private static void TestConfiguredDoorAssets()
        {
            var openCue = Resources.Load<SoundCue>("Audio/Doors/Door_Open_Default");
            Require(openCue != null, "Resources must contain Audio/Doors/Door_Open_Default.");
            Require(openCue.clips != null && openCue.clips.Length > 0, "Door_Open_Default must contain audio clips.");

            var closeCue = Resources.Load<SoundCue>("Audio/Doors/Door_Close_Default");
            Require(closeCue != null, "Resources must contain Audio/Doors/Door_Close_Default.");
            Require(closeCue.clips != null && closeCue.clips.Length > 0, "Door_Close_Default must contain audio clips.");

            var blockedCue = Resources.Load<SoundCue>("Audio/Doors/Door_Blocked_Default");
            Require(blockedCue != null, "Resources must contain Audio/Doors/Door_Blocked_Default.");
            Require(blockedCue.clips != null && blockedCue.clips.Length > 0, "Door_Blocked_Default must contain audio clips.");

            // Verify Door component auto-wiring
            var doorObj = new GameObject("TestDoor");
            var door = doorObj.AddComponent<EchoSound.Interaction.Door>();
            door.EnsureDefaultCues();

            Require(door.openCue != null, "Door.openCue must auto-populate default cue.");
            Require(door.closeCue != null, "Door.closeCue must auto-populate default cue.");
            Require(door.blockedCue != null, "Door.blockedCue must auto-populate default cue.");

            UnityEngine.Object.DestroyImmediate(doorObj);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"AudioSystem verification failed: {message}");
            }
        }
    }
}
