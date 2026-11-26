using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Editor
{
    /// <summary>
    /// Builds SoundCue assets for doors (open, close, blocked/locked)
    /// from the imported audio packs into Resources and Settings.
    /// </summary>
    public static class DoorAudioProfileBuilder
    {
        private const string ResourcesDoorsDir = "Assets/EchoSound/Resources/Audio/Doors";
        private const string SettingsDoorsDir = "Assets/EchoSound/Settings/Audio/SoundCues/Doors";

        private const string DefaultOpenPath = ResourcesDoorsDir + "/Door_Open_Default.asset";
        private const string DefaultClosePath = ResourcesDoorsDir + "/Door_Close_Default.asset";
        private const string DefaultBlockedPath = ResourcesDoorsDir + "/Door_Blocked_Default.asset";

        [InitializeOnLoadMethod]
        private static void AutoInit()
        {
            EditorApplication.delayCall += () =>
            {
                var existing = AssetDatabase.LoadAssetAtPath<SoundCue>(DefaultOpenPath);
                if (existing == null || existing.clips == null || existing.clips.Length == 0)
                {
                    BuildDoorSoundCues();
                }
            };
        }

        [MenuItem("EchoSound/Audio/Setup Door Sound Cues")]
        public static void BuildDoorSoundCues()
        {
            EnsureDirectory("Assets/EchoSound/Resources");
            EnsureDirectory("Assets/EchoSound/Resources/Audio");
            EnsureDirectory(ResourcesDoorsDir);

            EnsureDirectory("Assets/EchoSound/Settings/Audio/SoundCues");
            EnsureDirectory(SettingsDoorsDir);

            // 1. Gather clips from Doors Small Sound Pack & Basic RPG Sounds
            var doorPackDir = "Assets/Doors Small Sound Pack";
            var rpgAudioDir = "Assets/Multiple Solutions/Basic RPG Sounds/Audio";

            // Metal open & close
            var metalOpenClips = LoadSpecificClips(doorPackDir, new[] { "Drs_MetalDoor_Open_01" });
            var metalCloseClips = LoadSpecificClips(doorPackDir, new[] { "Drs_MetalDoor_Close_01" });
            var metalBlockedClips = LoadSpecificClips(rpgAudioDir, new[] { "Door Lock 1", "Door Lock 2", "Door Lock 3" });

            // Wood open & close
            var woodOpenClips = LoadSpecificClips(doorPackDir, new[] { "Drs_Wood_Door_Open_01", "Drs_Wood_Door_Open_02" });
            var woodCloseClips = LoadSpecificClips(doorPackDir, new[] { "Drs_Wood_Door_Close_01", "Drs_Wood_Door_Close_02" });
            var woodBlockedClips = LoadSpecificClips(rpgAudioDir, new[] { "Door Dungeon Lock 1", "Door Dungeon Lock 2", "Door Lock 4" });

            // 2. Create Material-specific Cues in Resources/Audio/Doors
            CreateOrUpdateCue(ResourcesDoorsDir + "/Door_Metal_Open.asset", metalOpenClips, 0.92f, 0.98f, 1.02f, 1.0f, 0.15f);
            CreateOrUpdateCue(ResourcesDoorsDir + "/Door_Metal_Close.asset", metalCloseClips, 0.95f, 0.98f, 1.02f, 1.0f, 0.15f);
            CreateOrUpdateCue(ResourcesDoorsDir + "/Door_Metal_Blocked.asset", metalBlockedClips, 0.85f, 0.98f, 1.02f, 1.0f, 0.15f);

            CreateOrUpdateCue(ResourcesDoorsDir + "/Door_Wood_Open.asset", woodOpenClips, 0.90f, 0.98f, 1.02f, 1.0f, 0.15f);
            CreateOrUpdateCue(ResourcesDoorsDir + "/Door_Wood_Close.asset", woodCloseClips, 0.95f, 0.98f, 1.02f, 1.0f, 0.15f);
            CreateOrUpdateCue(ResourcesDoorsDir + "/Door_Wood_Blocked.asset", woodBlockedClips, 0.85f, 0.98f, 1.02f, 1.0f, 0.15f);

            // Defaults fallback to Metal
            CreateOrUpdateCue(DefaultOpenPath, metalOpenClips, 0.92f, 0.98f, 1.02f, 1.0f, 0.15f);
            CreateOrUpdateCue(DefaultClosePath, metalCloseClips, 0.95f, 0.98f, 1.02f, 1.0f, 0.15f);
            CreateOrUpdateCue(DefaultBlockedPath, metalBlockedClips, 0.85f, 0.98f, 1.02f, 1.0f, 0.15f);

            // 3. Mirror in Settings/Audio/SoundCues/Doors
            CreateOrUpdateCue(SettingsDoorsDir + "/Door_Metal_Open.asset", metalOpenClips, 0.92f, 0.98f, 1.02f, 1.0f, 0.15f);
            CreateOrUpdateCue(SettingsDoorsDir + "/Door_Metal_Close.asset", metalCloseClips, 0.95f, 0.98f, 1.02f, 1.0f, 0.15f);
            CreateOrUpdateCue(SettingsDoorsDir + "/Door_Wood_Open.asset", woodOpenClips, 0.90f, 0.98f, 1.02f, 1.0f, 0.15f);
            CreateOrUpdateCue(SettingsDoorsDir + "/Door_Wood_Close.asset", woodCloseClips, 0.95f, 0.98f, 1.02f, 1.0f, 0.15f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[DoorAudioProfileBuilder] Successfully built and registered door SoundCues in Resources/Audio/Doors and Settings/Audio/SoundCues/Doors.");
        }

        private static AudioClip[] LoadSpecificClips(string folderPath, string[] fileNamesWithoutExt)
        {
            var list = new List<AudioClip>();
            foreach (string name in fileNamesWithoutExt)
            {
                string path = $"{folderPath}/{name}.wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) list.Add(clip);
            }
            return list.ToArray();
        }

        private static SoundCue CreateOrUpdateCue(
            string assetPath,
            AudioClip[] clips,
            float baseVolume,
            float minPitch,
            float maxPitch,
            float spatialBlend = 1.0f,
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
            cue.volumeVariation = new Vector2(0.95f, 1.05f);
            cue.basePitch = 1.0f;
            cue.pitchVariation = new Vector2(minPitch, maxPitch);
            cue.spatialBlend = spatialBlend;
            cue.rolloffMode = AudioRolloffMode.Logarithmic;
            cue.minDistance = 2.0f;
            cue.maxDistance = 28f;
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
