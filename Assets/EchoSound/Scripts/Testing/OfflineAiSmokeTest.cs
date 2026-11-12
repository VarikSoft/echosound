using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using EchoSound.Companion;
using EchoSound.Interaction;

namespace EchoSound.Testing
{
    /// <summary>Opt-in built-player integration check. Never runs during normal play.</summary>
    public sealed class OfflineAiSmokeTest : MonoBehaviour
    {
        private bool fallback;
        private float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool smoke = Array.IndexOf(args, "-ai-smoke-test") >= 0;
            bool fallback = Array.IndexOf(args, "-ai-smoke-fallback") >= 0;
            if (!smoke && !fallback) return;
            var host = new GameObject("Offline AI smoke test");
            host.AddComponent<OfflineAiSmokeTest>().fallback = fallback;
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            deadline = Time.realtimeSinceStartup + 240;
            StartCoroutine(Check());
        }
        private void Update()
        {
            if (Time.realtimeSinceStartup > deadline) Fail("Timed out.");
        }

        private IEnumerator Check()
        {
            yield return null;
            CompanionBrain brain = FindAnyObjectByType<CompanionBrain>();
            if (brain == null) { Fail("No companion in scene."); yield break; }
            brain.SetFreeDialogue(false, false);
            LocalAiRuntime runtime = LocalAiRuntime.Instance;
            while (runtime.ChatStarting || runtime.SpeechStarting) yield return null;
            if (!runtime.SpeechReady || runtime.ChatReady || runtime.ChatStarting)
            {
                Fail("Unexpected runtime readiness: chat=" + runtime.ChatReady + ", speech=" + runtime.SpeechReady);
                yield break;
            }
            if (!brain.Body.IsPlaced) { Fail("Companion did not reach the NavMesh."); yield break; }
            brain.Command("follow me");
            while (brain.IsBusy) yield return null;
            if (brain.Body.Activity != "following the curator") { Fail("Follow shortcut failed."); yield break; }
            brain.Command("стой");
            while (brain.IsBusy) yield return null;
            if (brain.Body.Activity != "waiting") { Fail("Stop shortcut failed."); yield break; }

            Vector3 start = brain.transform.position;
            Vector3 target = start;
            foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
            {
                if (NavMesh.SamplePosition(start + direction * 4, out NavMeshHit hit, 1, NavMesh.AllAreas)
                    && Vector3.Distance(start, hit.position) > 3 && brain.Body.TryGetRouteLength(hit.position, out float distance) && distance < 8)
                { target = hit.position; break; }
            }
            if (target == start) { Fail("No reachable movement test target."); yield break; }
            var marker = new GameObject("Smoke test destination");
            marker.transform.position = target;
            Transform player = brain.Body.curator;
            brain.Body.curator = marker.transform;
            brain.Command("Podejdź do mnie.");
            while (brain.IsBusy) yield return null;
            brain.Body.curator = player;
            Destroy(marker);
            if (Vector3.Distance(start, brain.transform.position) < 0.5f || Vector3.Distance(target, brain.transform.position) > 2.1f)
            { Fail("Come-here command did not walk to the target."); yield break; }

            string speechError = null;
            string transcript = null;
            var whisper = new WhisperClient { url = runtime.SpeechUrl, languages = new[] { "english" } };
            yield return whisper.Transcribe(new float[16000], 16000, text => transcript = text, error => speechError = error);
            if (speechError != null || WhisperClient.CleanTranscript(transcript).Length != 0)
            { Fail("Speech silence check failed: " + speechError); yield break; }

            string[] args = Environment.GetCommandLineArgs();
            int audioOption = Array.IndexOf(args, "-ai-voice-fixtures");
            if (audioOption >= 0 && audioOption + 1 < args.Length)
            {
                var voice = FindAnyObjectByType<CuratorVoice>();
                if (voice == null) { Fail("No voice input component."); yield break; }
                voice.whisper.language = WhisperClient.SpeechLanguage.Russian;
                string heard = null;
                Action<string> onHeard = value => heard = value;
                brain.OrderReceived += onHeard;
                foreach (string name in new[] { "follow", "stop", "follow2" })
                {
                    float[] samples = ReadWave(Path.Combine(args[audioOption + 1], name + ".wav"), out int rate);
                    heard = null;
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    voice.Submit(samples, rate);
                    while (heard == null && watch.Elapsed.TotalSeconds < 15) yield return null;
                    if (heard == null || !QuickCompanionCommand.TryParse(heard, out _, out _))
                    { brain.OrderReceived -= onHeard; Fail("Short spoken command missed the direct route: " + heard); yield break; }
                    while (brain.IsBusy) yield return null;
                    string expected = name == "stop" ? "waiting" : "following the curator";
                    if (brain.Body.Activity != expected) { brain.OrderReceived -= onHeard; Fail("Spoken command did not execute: " + heard); yield break; }
                    Debug.Log($"VOICE LATENCY PASSED: {name}, release-to-action={watch.Elapsed.TotalSeconds:0.000}s, direct route");
                }
                brain.OrderReceived -= onHeard;
                brain.Command("stop");
                while (brain.IsBusy) yield return null;
            }

            yield return CheckDoors(brain, start, audioOption >= 0 && audioOption + 1 < args.Length ? args[audioOption + 1] : null);
            brain.ResetConversation();
            string clarification = null;
            Action<string> onClarification = text => clarification = text;
            brain.Spoke += onClarification;
            var clarificationTimer = System.Diagnostics.Stopwatch.StartNew();
            brain.Command("Открой дверь только когда я разрешу");
            brain.Spoke -= onClarification;
            if (brain.IsBusy || brain.LastCommandRoute != "clarify" || clarification == null || runtime.ChatReady || runtime.ChatStarting)
            { Fail("Unknown command must clarify immediately without loading the model."); yield break; }
            Debug.Log($"FAST MODE PASSED: clarification={clarificationTimer.Elapsed.TotalSeconds:0.000}s; language model not loaded.");

            runtime.EnsureChatStarted();
            while (runtime.ChatStarting) yield return null;
            if (runtime.ChatReady == fallback) { Fail("Unexpected optional model readiness."); yield break; }

            if (!fallback)
            {
                brain.SetFreeDialogue(true, false);
                string reply = null;
                string problem = null;
                brain.Spoke += text => reply = text;
                brain.Failed += error => problem = error;
                string beforeQuestion = brain.Body.Activity;
                brain.Command("What are you doing?");
                while (brain.IsBusy) yield return null;
                if (problem != null || string.IsNullOrWhiteSpace(reply))
                { Fail("Bundled model conversation failed: " + problem); yield break; }
                if (brain.Body.Activity != beforeQuestion)
                { Fail("A question unexpectedly changed the companion's activity."); yield break; }
                brain.Command("follow me");
                while (brain.IsBusy) yield return null;
                brain.Command("Please remain at your current position until I return.");
                while (brain.IsBusy) yield return null;
                if (problem != null || brain.Body.Activity != "waiting")
                { Fail("Model-selected stop was not executed: " + problem + " / " + brain.Body.Activity); yield break; }
            }
            Debug.Log("OFFLINE AI SMOKE PASSED: " + (fallback ? "missing-model fallback" : "bundled conversation and action") + ", follow, stop, NavMesh movement, speech, clean exit.");
            Application.Quit(0);
        }

        private IEnumerator CheckDoors(CompanionBrain brain, Vector3 doorPosition, string fixtures)
        {
            // Keep world geometry and navigation, but isolate target selection from generated doors.
            var originalDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            var disabled = new System.Collections.Generic.List<Door>();
            foreach (Door item in originalDoors) if (item.enabled) { disabled.Add(item); item.enabled = false; }
            Transform player = brain.Body.curator;
            var host = new GameObject("Door command smoke fixtures");
            try
            {
                var viewer = new GameObject("Door test curator").transform;
                viewer.SetParent(host.transform);
                viewer.position = brain.transform.position;
                Vector3 forward = doorPosition - viewer.position;
                forward.y = 0;
                viewer.rotation = Quaternion.LookRotation(forward);
                brain.Body.curator = viewer;
                Door target = Door.Create("smoke_closed", host.transform, doorPosition, viewer.rotation, 1, 2.6f, null, false);
                Door decoy = Door.Create("smoke_already_open", host.transform,
                    viewer.position + forward.normalized * 0.7f + viewer.right * 0.3f, viewer.rotation, 1, 2.6f, null, true);
                yield return null;
                Physics.SyncTransforms();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                brain.Command("Открой дверь.");
                while (!brain.Body.IsWalking && !target.IsOpen && brain.IsBusy && watch.Elapsed.TotalSeconds < 2) yield return null;
                if (brain.LastCommandRoute != "direct" || (!brain.Body.IsWalking && !target.IsOpen))
                { Fail("Door command did not immediately start walking/opening."); yield break; }
                Debug.Log($"DOOR LATENCY PASSED: text-to-action={watch.Elapsed.TotalSeconds:0.000}s, direct route");
                while (brain.IsBusy) yield return null;
                if (!target.IsOpen || !decoy.IsOpen) { Fail("Wrong door opened."); yield break; }
                decoy.enabled = false;
                brain.Command("Закрой дверь передо мной.");
                while (brain.IsBusy) yield return null;
                if (target.IsOpen) { Fail("Door did not close."); yield break; }

                if (fixtures != null)
                {
                    CuratorVoice voice = FindAnyObjectByType<CuratorVoice>();
                    voice.whisper.language = WhisperClient.SpeechLanguage.Russian;
                    float[] audio = ReadWave(Path.Combine(fixtures, "door.wav"), out int rate);
                    string heard = null;
                    Action<string> onHeard = text => heard = text;
                    brain.OrderReceived += onHeard;
                    watch.Restart();
                    voice.Submit(audio, rate);
                    while (heard == null && watch.Elapsed.TotalSeconds < 15) yield return null;
                    brain.OrderReceived -= onHeard;
                    while (!target.IsOpen && brain.IsBusy) yield return null;
                    if (heard == null || brain.LastCommandRoute != "direct" || !target.IsOpen)
                    { Fail("Spoken door command missed direct execution: " + heard); yield break; }
                    Debug.Log($"DOOR VOICE LATENCY PASSED: release-to-action={watch.Elapsed.TotalSeconds:0.000}s, direct route");
                    while (brain.IsBusy) yield return null;
                }
                brain.Command("Закрой дверь передо мной а потом за мной");
                while (brain.IsBusy) yield return null;
                if (target.IsOpen || brain.LastCommandRoute != "direct" || brain.Body.Activity != "following the curator")
                { Fail("Direct door plus follow sequence failed."); yield break; }
                brain.Command("стой");
                brain.Command("Открой дверь передо мной а потом за мной");
                yield return null;
                brain.Command("стой");
                yield return new WaitForSecondsRealtime(2);
                if (brain.IsBusy || brain.Body.Activity != "waiting")
                { Fail("Stop did not cancel the queued follow after opening a door."); yield break; }
                Debug.Log("DOOR COMMANDS PASSED: open, close, speech, chained follow, cancellation, no model required.");
            }
            finally
            {
                brain.Body.curator = player;
                foreach (Door item in disabled) if (item != null) item.enabled = true;
                Destroy(host);
            }
            yield return null;
        }

        private void Fail(string message)
        {
            Debug.LogError("OFFLINE AI SMOKE FAILED: " + message);
            StopAllCoroutines();
            Application.Quit(1);
        }

        private static float[] ReadWave(string path, out int rate)
        {
            rate = 0;
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException("Expected WAV.");
                reader.ReadInt32();
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidDataException("Expected WAVE.");
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string chunk = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    int size = reader.ReadInt32();
                    long next = reader.BaseStream.Position + size + (size & 1);
                    if (chunk == "fmt ")
                    {
                        if (reader.ReadInt16() != 1 || reader.ReadInt16() != 1) throw new InvalidDataException("Expected mono PCM.");
                        rate = reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt16();
                        if (reader.ReadInt16() != 16) throw new InvalidDataException("Expected PCM16.");
                    }
                    if (chunk == "data" && rate > 0)
                    {
                        var result = new float[size / 2];
                        for (int i = 0; i < result.Length; i++) result[i] = reader.ReadInt16() / 32768f;
                        return result;
                    }
                    reader.BaseStream.Position = next;
                }
            }
            throw new InvalidDataException("Missing WAV data.");
        }
    }
}
