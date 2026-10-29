using System;
using UnityEditor;
using UnityEngine;
using EchoSound.Sound;

namespace EchoSound.Editor.Tests
{
    /// <summary>
    /// Verification checks for SoundBus and sound distribution system.
    /// Accessible via the Unity menu under EchoSound/Checks/Validate sound bus.
    /// </summary>
    public static class SoundBusChecks
    {
        private class TestListener : ISoundListener
        {
            public Vector3 ListenerPosition { get; set; }
            public float HearingSensitivity { get; set; } = 1f;

            public int HeardCount;
            public SoundEvent LastHeardEvent;
            public float LastDistance;

            public void OnSoundHeard(in SoundEvent soundEvent, float distance)
            {
                HeardCount++;
                LastHeardEvent = soundEvent;
                LastDistance = distance;
            }
        }

        [MenuItem("EchoSound/Checks/Validate sound bus")]
        public static void ValidateSoundBus()
        {
            SoundBus.Reset();

            // 1. Verify global delegate broadcast
            int globalReceivedCount = 0;
            SoundEvent lastGlobalSound = default;
            Action<SoundEvent> globalHandler = s =>
            {
                globalReceivedCount++;
                lastGlobalSound = s;
            };

            SoundBus.OnSoundEmitted += globalHandler;

            var testPos = new Vector3(10f, 0f, 5f);
            SoundBus.Emit(testPos, 15f, 0.8f, SoundType.FootstepRun);

            Require(globalReceivedCount == 1, "Global delegate did not receive sound event.");
            Require(lastGlobalSound.Type == SoundType.FootstepRun, "Global sound event type mismatch.");
            Require(lastGlobalSound.Radius == 15f, "Global sound radius mismatch.");
            Require(lastGlobalSound.Intensity == 0.8f, "Global sound intensity mismatch.");
            Require(lastGlobalSound.Position == testPos, "Global sound position mismatch.");

            // 2. Verify spatial listener range checks
            var listenerNearby = new TestListener { ListenerPosition = new Vector3(12f, 0f, 5f) }; // 2m away
            var listenerFar = new TestListener { ListenerPosition = new Vector3(100f, 0f, 5f) };    // 90m away

            SoundBus.RegisterListener(listenerNearby);
            SoundBus.RegisterListener(listenerFar);
            Require(SoundBus.ListenerCount == 2, "SoundBus listener count mismatch.");

            SoundBus.Emit(testPos, 10f, 1f, SoundType.CameraPing);

            Require(listenerNearby.HeardCount == 1, "Nearby listener should have heard the sound.");
            Require(Mathf.Approximately(listenerNearby.LastDistance, 2f), "Nearby listener distance mismatch.");
            Require(listenerNearby.LastHeardEvent.Type == SoundType.CameraPing, "Nearby listener heard wrong sound type.");
            Require(listenerFar.HeardCount == 0, "Far listener should not hear sound out of range.");

            // 3. Verify hearing sensitivity
            var sensitiveListener = new TestListener
            {
                ListenerPosition = new Vector3(30f, 0f, 5f), // 20m away
                HearingSensitivity = 2.5f                     // 10m radius * 2.5 = 25m effective radius
            };
            SoundBus.RegisterListener(sensitiveListener);

            SoundBus.Emit(testPos, 10f, 1f, SoundType.Voice);
            Require(sensitiveListener.HeardCount == 1, "Sensitive listener should hear sound amplified by sensitivity.");

            // 4. Verify unregistration
            SoundBus.UnregisterListener(listenerNearby);
            Require(SoundBus.ListenerCount == 2, "Unregistered listener count mismatch.");

            SoundBus.Emit(testPos, 10f, 1f, SoundType.FootstepWalk);
            Require(listenerNearby.HeardCount == 1, "Unregistered listener should not receive subsequent sounds.");

            // 5. Attenuation helper test
            var sound = new SoundEvent(Vector3.zero, 10f, 1f, SoundType.Impact);
            Require(sound.IsInRange(new Vector3(5, 0, 0)), "Target at 5m should be in range of 10m sound.");
            Require(!sound.IsInRange(new Vector3(15, 0, 0)), "Target at 15m should not be in range of 10m sound.");
            Require(Mathf.Approximately(sound.GetLinearAttenuation(new Vector3(5, 0, 0)), 0.5f), "Linear attenuation at half distance should be 0.5.");
            Require(sound.GetLinearAttenuation(new Vector3(12, 0, 0)) == 0f, "Linear attenuation outside radius should be 0.");

            // Clean up
            SoundBus.Reset();
            Require(SoundBus.ListenerCount == 0, "SoundBus Reset should clear all listeners.");

            Debug.Log("SoundBus checks: all tests passed successfully.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"SoundBus verification failed: {message}");
            }
        }
    }
}
