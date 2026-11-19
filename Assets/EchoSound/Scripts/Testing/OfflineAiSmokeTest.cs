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
            brain.Command("stój");
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
                voice.whisper.language = WhisperClient.SpeechLanguage.Polish;
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
            brain.Command("Otwórz drzwi dopiero, kiedy pozwolę");
            brain.Spoke -= onClarification;
            if (brain.IsBusy || brain.LastCommandRoute != "clarify" || clarification == null || runtime.ChatReady || runtime.ChatStarting)
            { Fail("Unknown command must clarify immediately without loading the model."); yield break; }
            Debug.Log($"FAST MODE PASSED: clarification={clarificationTimer.Elapsed.TotalSeconds:0.000}s; language model not loaded.");

            runtime.EnsureChatStarted();
            while (runtime.ChatStarting) yield return null;
            if (runtime.ChatReady == fallback) { Fail("Unexpected optional model readiness."); yield break; }

            if (!fallback)
            {
                brain.SetMode(CompanionBrain.CompanionMode.FastLlm, false);
                while (brain.FastStarting) yield return null;
                if (!brain.FastReady) { Fail("Fast LLM did not warm up."); yield break; }
                // Paraphrases outside the direct phrase list, so the language model must interpret them.
                foreach (string phrase in new[] { "Trzymaj się blisko mnie", "Poczekaj tutaj, aż się rozejrzę", "Stick with me for a while" })
                {
                    var intentWatch = System.Diagnostics.Stopwatch.StartNew();
                    brain.Command(phrase);
                    while (brain.IsBusy) yield return null;
                    string expected = phrase.StartsWith("Poczekaj") ? "waiting" : "following the curator";
                    if (brain.LastCommandRoute != "fast-llm" || brain.Body.Activity != expected)
                    { Fail("Fast LLM movement failed: " + phrase); yield break; }
                    Debug.Log($"FAST LLM MOVEMENT PASSED: command-to-action={intentWatch.Elapsed.TotalSeconds:0.000}s");
                }
                brain.Command("Nie zamykaj drzwi");
                if (brain.IsBusy || brain.LastCommandRoute != "clarify") { Fail("Negated fast command was not rejected."); yield break; }
                brain.Command("za mną");
                while (brain.IsBusy) yield return null;
                brain.Command("Trochę się boję");
                while (brain.IsBusy) yield return null;
                if (brain.LastCommandRoute != "game-chat" || brain.Body.Activity != "following the curator")
                { Fail("Small talk should produce a reply without changing the previous action."); yield break; }
                yield return CheckConversationSafety(brain);
                brain.Command("stój");
                while (brain.IsBusy) yield return null;
                Debug.Log("FAST CHAT PASSED: social reply preserves the action already in progress.");
                yield return CheckDoors(brain, Vector3.Distance(brain.transform.position, start) > 2 ? start : target,
                    audioOption >= 0 && audioOption + 1 < args.Length ? args[audioOption + 1] : null, true);
                brain.enabled = false;
                brain.enabled = true;
                while (brain.FastStarting) yield return null;
                if (!brain.FastReady) { Fail("Fast LLM did not recover after re-enabling the companion."); yield break; }
                Debug.Log("FAST LLM LIFECYCLE PASSED: re-enabled companion is ready.");
                brain.ResetConversation();
                brain.SetFreeDialogue(true, false);
                yield return CheckConversationSafety(brain);
                while (brain.FastStarting) yield return null;
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

        private IEnumerator CheckConversationSafety(CompanionBrain brain)
        {
            brain.Command("idź za mną");
            while (brain.IsBusy) yield return null;
            string answer = "";
            void Capture(string text) => answer = text;
            brain.Spoke += Capture;
            try
            {
                brain.Command("Mam na imię Łucja.");
                brain.Command("Mam kota o imieniu Plusz.");
                brain.Command("Do zobaczenia!");
                brain.Command("Jak mam na imię?");
                if (answer != "Masz na imię Łucja.") { Fail("Lost the player's name after other turns."); yield break; }
                brain.Command("What's my cat's name?");
                if (answer != "Your cat is called Plusz.") { Fail("The player's cat was forgotten or assigned to the wrong speaker."); yield break; }
                brain.Command("Czy idziesz za mną?");
                if (answer != "Idę za tobą." || brain.Body.Activity != "following the curator")
                { Fail("Conversation or a farewell interrupted the companion's following order."); yield break; }
                brain.Command("„Stój”");
                while (brain.IsBusy) yield return null;
                if (brain.Body.Activity != "following the curator")
                { Fail("The companion executed a quoted order."); yield break; }
                brain.Command("stop");
                while (brain.IsBusy) yield return null;
                if (brain.Body.Activity != "waiting") { Fail("A real stop was ignored."); yield break; }
                Debug.Log("DIALOGUE SAFETY PASSED: memory, quoted speech, farewell, and real order, mode=" + brain.Mode);
            }
            finally { brain.Spoke -= Capture; }
        }

        private IEnumerator CheckDoors(CompanionBrain brain, Vector3 doorPosition, string fixtures, bool semantic = false)
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
                if (!semantic)
                {
                    string answer = null;
                    void CaptureAnswer(string text) => answer = text;
                    brain.Spoke += CaptureAnswer;
                    brain.Command("Czy drzwi z przodu są zamknięte?");
                    brain.Spoke -= CaptureAnswer;
                    if (brain.LastCommandRoute != "door-status" || answer != "Drzwi są już otwarte."
                        || target.IsOpen || !decoy.IsOpen)
                    {
                        Fail("Door status must report the nearer open door without operating either door.");
                        yield break;
                    }
                    Debug.Log($"DOOR STATUS PASSED: transcript-to-reply={watch.Elapsed.TotalSeconds:0.000}s, route=door-status");
                    watch.Restart();
                }
                brain.Command(semantic ? "Uchyl drzwi z przodu" : "Otwórz drzwi.");
                while (!brain.Body.IsWalking && !target.IsOpen && brain.IsBusy && watch.Elapsed.TotalSeconds < 2) yield return null;
                string expectedRoute = semantic ? "fast-llm" : "direct";
                if (brain.LastCommandRoute != expectedRoute || (!brain.Body.IsWalking && !target.IsOpen))
                { Fail("Door command did not immediately start walking/opening."); yield break; }
                Debug.Log($"DOOR LATENCY PASSED: text-to-action={watch.Elapsed.TotalSeconds:0.000}s, route={expectedRoute}");
                if (!semantic)
                {
                    brain.Command("Is the door ahead open?");
                    if (!brain.IsBusy)
                    {
                        Fail("Door status interrupted the action already in progress.");
                        yield break;
                    }
                }
                while (brain.IsBusy) yield return null;
                if (!target.IsOpen || !decoy.IsOpen) { Fail("Wrong door opened."); yield break; }
                decoy.enabled = false;
                brain.Command(semantic ? "Przymknij drzwi przede mną" : "Zamknij drzwi przede mną.");
                while (brain.IsBusy) yield return null;
                if (target.IsOpen) { Fail("Door did not close."); yield break; }

                if (fixtures != null)
                {
                    CuratorVoice voice = FindAnyObjectByType<CuratorVoice>();
                    voice.whisper.language = WhisperClient.SpeechLanguage.Polish;
                    float[] audio = ReadWave(Path.Combine(fixtures, semantic ? "fast-door.wav" : "door.wav"), out int rate);
                    string heard = null;
                    Action<string> onHeard = text => heard = text;
                    brain.OrderReceived += onHeard;
                    watch.Restart();
                    voice.Submit(audio, rate);
                    while (heard == null && watch.Elapsed.TotalSeconds < 15) yield return null;
                    brain.OrderReceived -= onHeard;
                    while (!target.IsOpen && brain.IsBusy) yield return null;
                    if (heard == null || brain.LastCommandRoute != expectedRoute || !target.IsOpen)
                    { Fail("Spoken door command missed direct execution: " + heard); yield break; }
                    Debug.Log($"DOOR VOICE LATENCY PASSED: release-to-action={watch.Elapsed.TotalSeconds:0.000}s, route={expectedRoute}");
                    while (brain.IsBusy) yield return null;
                }
                brain.Command(semantic ? "Przymknij drzwi z przodu, a potem trzymaj się blisko mnie" : "Zamknij drzwi przede mną, a potem za mną");
                while (brain.IsBusy) yield return null;
                if (target.IsOpen || brain.LastCommandRoute != expectedRoute || brain.Body.Activity != "following the curator")
                { Fail("Direct door plus follow sequence failed."); yield break; }
                brain.Command("stój");
                brain.Command(semantic ? "Uchyl drzwi z przodu, a potem trzymaj się blisko mnie" : "Open the door in front of me and then follow me");
                yield return null;
                brain.Command("stop");
                yield return new WaitForSecondsRealtime(2);
                if (brain.IsBusy || brain.Body.Activity != "waiting")
                { Fail("Stop did not cancel the queued follow after opening a door."); yield break; }
                if (!semantic)
                {
                    yield return CheckDoorEscort(brain, viewer, target, forward.normalized, fixtures);
                }
                Debug.Log("DOOR COMMANDS PASSED: open, close, speech, chained follow, cancellation; route=" + expectedRoute);
            }
            finally
            {
                brain.Body.curator = player;
                foreach (Door item in disabled) if (item != null) item.enabled = true;
                Destroy(host);
            }
            yield return null;
        }

        private IEnumerator CheckDoorEscort(CompanionBrain brain, Transform viewer, Door target, Vector3 forward, string fixtures)
        {
            brain.Command("Zamknij drzwi przede mną.");
            while (brain.IsBusy) yield return null;
            if (target.IsOpen) { Fail("Could not prepare the closed escort door."); yield break; }
            // Keep the fixture's line of sight away from the real player standing at the spawn.
            viewer.position = target.transform.position - forward * 0.9f;
            Physics.SyncTransforms();
            foreach (string phrase in new[] {
                "Idź za mną i otwórz wszystkie drzwi",
                "Follow me. Open all the doors on the way.",
                "Chodź za mną. Otwieraj drzwi po drodze.",
                "Idź za mną. Osłaniaj mnie. I otwieraj wszystkie drzwi po drodze."
            })
                yield return VerifyEscort(brain, target, phrase, null);
            if (fixtures != null)
            {
                foreach (string name in new[] { "escort.wav", "pl-escort.wav", "en-escort.wav" })
                {
                    string wave = Path.Combine(fixtures, name);
                    if (File.Exists(wave)) yield return VerifyEscort(brain, target, null, wave);
                }
            }

            Door second = Door.Create("smoke_bulk_second", target.transform.parent, target.transform.position + viewer.right * 1.3f,
                viewer.rotation, 1, 2.6f, null, false);
            brain.Command("Otwórz wszystkie drzwi");
            while (brain.IsBusy) yield return null;
            if (!target.IsOpen || !second.IsOpen) { Fail("Standalone bulk order did not open both doors."); yield break; }
            Debug.Log("DOOR ESCORT PASSED: open on path, resume follow, stop, and standalone bulk opening.");

            yield return VerifyDismissal(brain, viewer, target, second, "Echo, nie idź za mną i nie otwieraj już drzwi.", null, false);
            yield return VerifyDismissal(brain, viewer, target, second, "Don't follow me anymore and close all the doors.", null, true);
            yield return VerifyDismissal(brain, viewer, target, second, "Przestań za mną chodzić, a potem pozamykaj drzwi.", null, true);
            if (fixtures != null)
            {
                string stopWave = Path.Combine(fixtures, "pl-dismiss.wav");
                string closeWave = Path.Combine(fixtures, "en-dismiss-close.wav");
                if (File.Exists(stopWave)) yield return VerifyDismissal(brain, viewer, target, second, null, stopWave, false);
                if (File.Exists(closeWave)) yield return VerifyDismissal(brain, viewer, target, second, null, closeWave, true);
            }
        }

        private IEnumerator VerifyDismissal(CompanionBrain brain, Transform viewer, Door target, Door second,
            string phrase, string wave, bool closeDoors)
        {
            brain.Command("Otwórz wszystkie drzwi");
            while (brain.IsBusy) yield return null;
            if (!target.IsOpen || !second.IsOpen) { Fail("Dismissal test requires two open doors."); yield break; }
            brain.Command("Idź za mną. Otwórz wszystkie drzwi.");
            if (brain.Body.Activity != "following the curator") { Fail("Dismissal test requires active escort."); yield break; }

            float[] audio = null;
            int rate = 0;
            if (wave != null) audio = ReadWave(wave, out rate);
            string heard = null;
            string reply = null;
            double responseSeconds = -1;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            void CaptureOrder(string text) => heard = text;
            void CaptureResponse(string text)
            {
                if (responseSeconds >= 0) return;
                responseSeconds = watch.Elapsed.TotalSeconds;
                reply = text;
            }
            brain.OrderReceived += CaptureOrder;
            brain.Spoke += CaptureResponse;
            try
            {
                if (audio != null) FindAnyObjectByType<CuratorVoice>().Submit(audio, rate);
                else brain.Command(phrase);
                while ((responseSeconds < 0 || brain.IsBusy) && watch.Elapsed.TotalSeconds < 12) yield return null;
            }
            finally
            {
                brain.OrderReceived -= CaptureOrder;
                brain.Spoke -= CaptureResponse;
            }
            bool bothOpen = target.IsOpen && second.IsOpen;
            bool bothClosed = !target.IsOpen && !second.IsOpen;
            bool doorsMatch = closeDoors ? bothClosed : bothOpen;
            if (heard == null || responseSeconds < 0 || brain.LastCommandRoute != "direct" || brain.IsBusy
                || brain.Body.Activity != "waiting" || !doorsMatch || string.IsNullOrEmpty(reply))
            {
                Fail($"Dismissal did not take effect: {heard ?? phrase ?? wave}, activity={brain.Body.Activity}, doors={target.State}/{second.State}.");
                yield break;
            }
            if (bothOpen)
            {
                var user = viewer.GetComponent<Interactor>() ?? viewer.gameObject.AddComponent<Interactor>();
                if (!target.TryUse(user, InteractionAction.Close, out string problem))
                { Fail("Could not prepare a door for the cancelled escort test: " + problem); yield break; }
            }
            yield return new WaitForSeconds(0.65f);
            if (target.IsOpen || brain.Body.Activity != "waiting")
            { Fail("The old escort continued after dismissal."); yield break; }

            brain.Command("Follow me.");
            while (brain.IsBusy) yield return null;
            if (brain.Body.Activity != "following the curator" || brain.LastCommandRoute != "direct")
            { Fail("A new movement order must still work after dismissal."); yield break; }
            brain.Command("stój");
            while (brain.IsBusy) yield return null;
            Debug.Log($"DISMISSAL PASSED: input={(wave == null ? "text" : Path.GetFileName(wave))}, reply={responseSeconds:0.000}s, transcript={heard}");
        }

        private IEnumerator VerifyEscort(CompanionBrain brain, Door target, string phrase, string wave)
        {
            float[] audio = null;
            int rate = 0;
            if (wave != null) audio = ReadWave(wave, out rate);
            string heard = null;
            double responseSeconds = -1;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            void CaptureOrder(string text) => heard = text;
            void CaptureResponse(string text)
            {
                if (responseSeconds < 0) responseSeconds = watch.Elapsed.TotalSeconds;
            }
            brain.OrderReceived += CaptureOrder;
            brain.Spoke += CaptureResponse;
            try
            {
                if (audio != null) FindAnyObjectByType<CuratorVoice>().Submit(audio, rate);
                else brain.Command(phrase);
                while (responseSeconds < 0 && watch.Elapsed.TotalSeconds < 8) yield return null;
            }
            finally
            {
                brain.OrderReceived -= CaptureOrder;
                brain.Spoke -= CaptureResponse;
            }
            if (heard == null || responseSeconds < 0 || brain.LastCommandRoute != "direct")
            {
                Fail($"Escort did not take the direct route: {heard ?? phrase ?? wave} (route={brain.LastCommandRoute}).");
                yield break;
            }
            while ((!target.IsOpen || target.IsBusy || brain.Body.Activity != "following the curator")
                && watch.Elapsed.TotalSeconds < 8) yield return null;
            if (!target.IsOpen || target.IsBusy || brain.Body.Activity != "following the curator")
            {
                Fail($"Escort should open the door and resume following: {heard}, door={target.State}, activity={brain.Body.Activity}.");
                yield break;
            }
            Debug.Log($"ESCORT VARIANT PASSED: input={(wave == null ? "text" : Path.GetFileName(wave))}, reply={responseSeconds:0.000}s, transcript={heard}");
            brain.Command("stop");
            brain.Command("Close the door in front of me.");
            while (brain.IsBusy) yield return null;
            yield return new WaitForSeconds(0.4f);
            if (target.IsOpen || brain.Body.Activity == "following the curator") Fail("Cancelled escort continued following or reopened a door.");
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
