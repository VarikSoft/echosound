using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace EchoSound.Editor
{
    /// <summary>
    /// Creates and configures Assets/EchoSound/Resources/Audio/EchoSoundMixer.mixer
    /// with Master, SFX, Voice, Ambience, and UI groups and exposed volume parameters.
    /// </summary>
    public static class AudioMixerSetup
    {
        private const string MixerPath = "Assets/EchoSound/Resources/Audio/EchoSoundMixer.mixer";

        [InitializeOnLoadMethod]
        private static void AutoInit()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying && !File.Exists(MixerPath))
                {
                    CreateEchoSoundMixer();
                }
            };
        }

        [MenuItem("EchoSound/Audio/Setup EchoSound AudioMixer")]
        public static void CreateEchoSoundMixer()
        {
            if (File.Exists(MixerPath))
            {
                Debug.Log("[AudioMixerSetup] EchoSoundMixer already exists at " + MixerPath);
                return;
            }

            string folder = Path.GetDirectoryName(MixerPath);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            Type mixerControllerType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
            if (mixerControllerType == null)
            {
                Debug.LogWarning("[AudioMixerSetup] AudioMixerController type not found.");
                return;
            }

            MethodInfo createMethod = mixerControllerType.GetMethod(
                "CreateMixerControllerAtPath",
                BindingFlags.Public | BindingFlags.Static);
            if (createMethod == null)
            {
                Debug.LogWarning("[AudioMixerSetup] CreateMixerControllerAtPath method not found.");
                return;
            }

            var controller = createMethod.Invoke(null, new object[] { MixerPath }) as AudioMixer;
            if (controller == null)
            {
                Debug.LogWarning("[AudioMixerSetup] Failed to create AudioMixer at " + MixerPath);
                return;
            }

            PropertyInfo masterProp = mixerControllerType.GetProperty("masterGroup", BindingFlags.Public | BindingFlags.Instance);
            object masterGroup = masterProp != null ? masterProp.GetValue(controller) : null;

            if (masterGroup != null)
            {
                ExposeGroupVolume(mixerControllerType, controller, masterGroup, "MasterVolume");

                object sfxGroup = CreateChildGroup(mixerControllerType, controller, masterGroup, "SFX");
                if (sfxGroup != null) ExposeGroupVolume(mixerControllerType, controller, sfxGroup, "SFXVolume");

                object voiceGroup = CreateChildGroup(mixerControllerType, controller, masterGroup, "Voice");
                if (voiceGroup != null) ExposeGroupVolume(mixerControllerType, controller, voiceGroup, "VoiceVolume");

                object ambienceGroup = CreateChildGroup(mixerControllerType, controller, masterGroup, "Ambience");
                if (ambienceGroup != null) ExposeGroupVolume(mixerControllerType, controller, ambienceGroup, "AmbienceVolume");

                CreateChildGroup(mixerControllerType, controller, masterGroup, "UI");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AudioMixerSetup] Created and configured EchoSoundMixer at " + MixerPath);
        }

        private static object CreateChildGroup(Type mixerControllerType, object controller, object parentGroup, string groupName)
        {
            MethodInfo createGroup = mixerControllerType.GetMethod("CreateNewGroup", new[] { typeof(string), typeof(bool) });
            MethodInfo addChild = mixerControllerType.GetMethod("AddChildToParent", BindingFlags.Public | BindingFlags.Instance);
            if (createGroup == null || addChild == null) return null;

            object group = createGroup.Invoke(controller, new object[] { groupName, false });
            if (group != null)
            {
                addChild.Invoke(controller, new[] { group, parentGroup });
            }
            return group;
        }

        private static void ExposeGroupVolume(Type mixerControllerType, object controller, object group, string exposedName)
        {
            MethodInfo getGuid = group.GetType().GetMethod("GetGUIDForVolume", BindingFlags.Public | BindingFlags.Instance);
            if (getGuid == null) return;

            object volumeGuid = getGuid.Invoke(group, null);
            if (volumeGuid == null) return;

            // Add exposed parameter via SerializedObject so it works reliably across Unity versions
            var serialized = new SerializedObject((UnityEngine.Object)controller);
            SerializedProperty exposedParams = serialized.FindProperty("m_ExposedParameters");
            if (exposedParams == null) return;

            int index = exposedParams.arraySize;
            exposedParams.InsertArrayElementAtIndex(index);
            SerializedProperty elem = exposedParams.GetArrayElementAtIndex(index);

            SerializedProperty nameProp = elem.FindPropertyRelative("name");
            SerializedProperty guidProp = elem.FindPropertyRelative("guid");
            if (nameProp != null) nameProp.stringValue = exposedName;
            if (guidProp != null)
            {
                // Copy GUID bytes/ints via reflection
                Type guidType = volumeGuid.GetType();
                for (int i = 0; i < 4; i++)
                {
                    SerializedProperty gPart = guidProp.FindPropertyRelative($"data[{i}]");
                    if (gPart != null)
                    {
                        PropertyInfo indexer = guidType.GetProperty("Item");
                        if (indexer != null) gPart.intValue = (int)(uint)indexer.GetValue(volumeGuid, new object[] { i });
                    }
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
