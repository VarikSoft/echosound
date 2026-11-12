using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Editor
{
    /// <summary>
    /// Automatically builds SoundCue assets from imported footstep audio clips,
    /// configures a SurfaceAudioProfile for Metal, Concrete, Wood, and Soft materials,
    /// and assigns the profile to the FirstPersonPlayer prefab.
    /// </summary>
    public static class FootstepProfileBuilder
    {
        private const string AudioSettingsDir = "Assets/EchoSound/Settings/Audio";
        private const string CuesDir = "Assets/EchoSound/Settings/Audio/SoundCues/Footsteps";
        private const string ProfilePath = AudioSettingsDir + "/PlayerFootstepProfile.asset";
        private const string PlayerPrefabPath = "Assets/EchoSound/Prefabs/Player/FirstPersonPlayer.prefab";

        [InitializeOnLoadMethod]
        private static void AutoInit()
        {
            EditorApplication.delayCall += () =>
            {
                var existing = AssetDatabase.LoadAssetAtPath<SurfaceAudioProfile>(ProfilePath);
                if (existing == null || existing.defaultWalkCue == null)
                {
                    BuildAndWireFootsteps();
                }
            };
        }

        [MenuItem("EchoSound/Audio/Setup Footsteps and Wire Player")]
        public static void BuildAndWireFootsteps()
        {
            EnsureDirectory("Assets/EchoSound/Settings");
            EnsureDirectory(AudioSettingsDir);
            EnsureDirectory("Assets/EchoSound/Settings/Audio/SoundCues");
            EnsureDirectory(CuesDir);

            // 1. Load Clips from Assets/Footsteps - Essentials
            // Metal
            var metalWalkClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_Metal/Footsteps_Metal_Walk");
            var metalRunClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_Metal/Footsteps_Metal_Run");
            var metalJumpClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_Metal/Footsteps_Metal_Jump");

            // Concrete (uses Rock footsteps)
            var concreteWalkClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_Rock/Footsteps_Rock_Walk");
            var concreteRunClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_Rock/Footsteps_Rock_Run");
            var concreteJumpClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_Rock/Footsteps_Rock_Jump");

            // Wood
            var woodWalkClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Walk");
            var woodRunClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Run");
            var woodJumpClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Jump");

            // Soft (uses DirtyGround & Grass)
            var softWalkClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_DirtyGround/Footsteps_DirtyGround_Walk");
            var softRunClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_DirtyGround/Footsteps_DirtyGround_Run");
            var softJumpClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_Grass/Footsteps_Grass_Jump");
            var softLandClips = LoadClips("Assets/Footsteps - Essentials/Footsteps_DirtyGround/Footsteps_DirtyGround_Land");

            // 2. Build SoundCue assets
            // Metal Cues
            var metalWalkCue = GetOrCreateCue(CuesDir + "/Footstep_Metal_Walk.asset", metalWalkClips, 0.8f, 0.94f, 1.06f, 0.8f);
            var metalRunCue = GetOrCreateCue(CuesDir + "/Footstep_Metal_Run.asset", metalRunClips, 0.92f, 0.94f, 1.06f, 0.8f);
            var metalJumpCue = GetOrCreateCue(CuesDir + "/Footstep_Metal_Jump.asset", metalJumpClips, 0.95f, 0.94f, 1.06f, 0.85f);
            var metalLandCue = GetOrCreateCue(CuesDir + "/Footstep_Metal_Land.asset", metalJumpClips, 1.0f, 0.92f, 1.04f, 0.85f);
            var metalImpactCue = GetOrCreateCue(CuesDir + "/Impact_Metal.asset", metalJumpClips, 1.0f, 0.90f, 1.08f, 0.95f, 0.15f);

            // Concrete Cues
            var concreteWalkCue = GetOrCreateCue(CuesDir + "/Footstep_Concrete_Walk.asset", concreteWalkClips, 0.82f, 0.94f, 1.06f, 0.8f);
            var concreteRunCue = GetOrCreateCue(CuesDir + "/Footstep_Concrete_Run.asset", concreteRunClips, 0.95f, 0.94f, 1.06f, 0.8f);
            var concreteJumpCue = GetOrCreateCue(CuesDir + "/Footstep_Concrete_Jump.asset", concreteJumpClips, 0.95f, 0.94f, 1.06f, 0.85f);
            var concreteLandCue = GetOrCreateCue(CuesDir + "/Footstep_Concrete_Land.asset", concreteJumpClips, 1.0f, 0.92f, 1.04f, 0.85f);
            var concreteImpactCue = GetOrCreateCue(CuesDir + "/Impact_Concrete.asset", concreteJumpClips, 1.0f, 0.90f, 1.08f, 0.95f, 0.15f);

            // Wood Cues
            var woodWalkCue = GetOrCreateCue(CuesDir + "/Footstep_Wood_Walk.asset", woodWalkClips, 0.82f, 0.94f, 1.06f, 0.8f);
            var woodRunCue = GetOrCreateCue(CuesDir + "/Footstep_Wood_Run.asset", woodRunClips, 0.95f, 0.94f, 1.06f, 0.8f);
            var woodJumpCue = GetOrCreateCue(CuesDir + "/Footstep_Wood_Jump.asset", woodJumpClips, 0.95f, 0.94f, 1.06f, 0.85f);
            var woodLandCue = GetOrCreateCue(CuesDir + "/Footstep_Wood_Land.asset", woodJumpClips, 1.0f, 0.92f, 1.04f, 0.85f);
            var woodImpactCue = GetOrCreateCue(CuesDir + "/Impact_Wood.asset", woodJumpClips, 1.0f, 0.90f, 1.08f, 0.95f, 0.15f);

            // Soft Cues
            var softWalkCue = GetOrCreateCue(CuesDir + "/Footstep_Soft_Walk.asset", softWalkClips, 0.75f, 0.94f, 1.06f, 0.8f);
            var softRunCue = GetOrCreateCue(CuesDir + "/Footstep_Soft_Run.asset", softRunClips, 0.88f, 0.94f, 1.06f, 0.8f);
            var softJumpCue = GetOrCreateCue(CuesDir + "/Footstep_Soft_Jump.asset", softJumpClips, 0.9f, 0.94f, 1.06f, 0.85f);
            var softLandCue = GetOrCreateCue(CuesDir + "/Footstep_Soft_Land.asset", softLandClips, 0.95f, 0.92f, 1.04f, 0.85f);
            var softImpactCue = GetOrCreateCue(CuesDir + "/Impact_Soft.asset", softLandClips, 0.95f, 0.90f, 1.08f, 0.95f, 0.15f);

            // 3. Configure SurfaceAudioProfile
            var profile = AssetDatabase.LoadAssetAtPath<SurfaceAudioProfile>(ProfilePath);
            bool isNewProfile = (profile == null);
            if (isNewProfile)
            {
                profile = ScriptableObject.CreateInstance<SurfaceAudioProfile>();
            }

            profile.surfaceSets = new SurfaceAudioProfile.SurfaceSoundSet[]
            {
                new SurfaceAudioProfile.SurfaceSoundSet
                {
                    material = AcousticMaterialKind.Metal,
                    walkCue = metalWalkCue,
                    runCue = metalRunCue,
                    jumpCue = metalJumpCue,
                    landCue = metalLandCue,
                    impactCue = metalImpactCue
                },
                new SurfaceAudioProfile.SurfaceSoundSet
                {
                    material = AcousticMaterialKind.Concrete,
                    walkCue = concreteWalkCue,
                    runCue = concreteRunCue,
                    jumpCue = concreteJumpCue,
                    landCue = concreteLandCue,
                    impactCue = concreteImpactCue
                },
                new SurfaceAudioProfile.SurfaceSoundSet
                {
                    material = AcousticMaterialKind.Wood,
                    walkCue = woodWalkCue,
                    runCue = woodRunCue,
                    jumpCue = woodJumpCue,
                    landCue = woodLandCue,
                    impactCue = woodImpactCue
                },
                new SurfaceAudioProfile.SurfaceSoundSet
                {
                    material = AcousticMaterialKind.Soft,
                    walkCue = softWalkCue,
                    runCue = softRunCue,
                    jumpCue = softJumpCue,
                    landCue = softLandCue,
                    impactCue = softImpactCue
                }
            };

            // Fallback / Defaults
            profile.defaultWalkCue = concreteWalkCue;
            profile.defaultRunCue = concreteRunCue;
            profile.defaultJumpCue = concreteJumpCue;
            profile.defaultLandCue = softLandCue;
            profile.defaultImpactCue = concreteImpactCue;

            if (isNewProfile)
            {
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            else
            {
                EditorUtility.SetDirty(profile);
            }

            // 4. Assign profile to FirstPersonPlayer prefab
            WirePlayerPrefab(profile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FootstepProfileBuilder] Successfully built footstep sound cues and wired SurfaceAudioProfile to Player prefab at {ProfilePath}");
        }

        private static void WirePlayerPrefab(SurfaceAudioProfile profile)
        {
            if (!File.Exists(PlayerPrefabPath))
            {
                Debug.LogWarning($"[FootstepProfileBuilder] Player prefab not found at {PlayerPrefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var emitter = root.GetComponent<PlayerSoundEmitter>();
                if (emitter == null)
                {
                    emitter = root.AddComponent<PlayerSoundEmitter>();
                }

                emitter.surfaceAudio = profile;
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[FootstepProfileBuilder] PlayerSoundEmitter.surfaceAudio wired to " + profile.name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AudioClip[] LoadClips(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"[FootstepProfileBuilder] Missing clip folder: {folderPath}");
                return Array.Empty<AudioClip>();
            }

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath });
            var clips = new List<AudioClip>(guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clips.Add(clip);
            }
            return clips.ToArray();
        }

        private static SoundCue GetOrCreateCue(
            string assetPath,
            AudioClip[] clips,
            float baseVolume,
            float minPitch,
            float maxPitch,
            float spatialBlend,
            float cooldown = 0.12f)
        {
            var cue = AssetDatabase.LoadAssetAtPath<SoundCue>(assetPath);
            bool isNew = (cue == null);
            if (isNew)
            {
                cue = ScriptableObject.CreateInstance<SoundCue>();
            }

            cue.clips = clips;
            cue.selectionMode = ClipSelectionMode.RandomNoRepeat;
            cue.baseVolume = baseVolume;
            cue.volumeVariation = new Vector2(0.92f, 1.05f);
            cue.basePitch = 1.0f;
            cue.pitchVariation = new Vector2(minPitch, maxPitch);
            cue.spatialBlend = spatialBlend;
            cue.rolloffMode = AudioRolloffMode.Logarithmic;
            cue.minDistance = 1.5f;
            cue.maxDistance = 25f;
            cue.cooldown = cooldown;
            cue.emitToSoundBus = false;

            if (isNew)
            {
                AssetDatabase.CreateAsset(cue, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(cue);
            }
            return cue;
        }

        private static void EnsureDirectory(string relativePath)
        {
            if (!AssetDatabase.IsValidFolder(relativePath))
            {
                string parent = Path.GetDirectoryName(relativePath).Replace('\\', '/');
                string leaf = Path.GetFileName(relativePath);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureDirectory(parent);
                }
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
