using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using EchoSound.Companion;
using EchoSound.Interaction;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

namespace EchoSound.Testing
{
    /// <summary>Opt-in built-player acceptance suite. Inputs and assertions are external JSON fixtures.</summary>
    public sealed class CompanionAcceptanceTest : MonoBehaviour
    {
        [Serializable]
        public sealed class Case
        {
            public string id, group, text, audio, language = "Polish", mode = "FastLlm";
            public string expected = "preserve", contains, exact, expectedRoute;
            public bool reset, quiet, replaceWithStop, disableVoice, following, cancelWithStop, differentFromPrevious;
            public int doors = -1;
        }

        [Serializable] public sealed class Suite { public Case[] cases; }
        [Serializable]
        public sealed class Result
        {
            public string id, group, mode, input, transcript, reply, route, before, after, error;
            public bool audio, correct, underOneSecond, measurable;
            public double replySeconds = -1, actionSeconds = -1, firstTextSeconds = -1, finishedSeconds;
            public int doors;
        }

        [Serializable]
        public sealed class Report
        {
            public string processor, gpu, operatingSystem, utc, fixturePath;
            public int processors, memoryMb;
            public double startupSeconds;
            public List<Result> results = new List<Result>();
        }

        private CompanionBrain brain;
        private CuratorVoice voice;
        private Door front, left, right;
        private Transform curator;
        private Vector3 home;
        private string outputPath;
        private string fixturePath;
        private float globalDeadline;
        private readonly Report report = new Report();
        private string[] arguments;
        private string previousReply;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-companion-acceptance");
            if (at < 0 || at + 2 >= args.Length) return;
            var test = new GameObject("Companion acceptance tests").AddComponent<CompanionAcceptanceTest>();
            test.fixturePath = args[at + 1];
            test.outputPath = args[at + 2];
            test.arguments = args;
        }

        private void Update()
        {
            if (globalDeadline != 0 && Time.realtimeSinceStartup > globalDeadline)
            {
                Debug.LogError("ACCEPTANCE TIMED OUT");
                Save();
                Application.Quit(1);
            }
        }

        private IEnumerator Start()
        {
            Application.targetFrameRate = 120;
            globalDeadline = Time.realtimeSinceStartup + 900;
            var startup = Stopwatch.StartNew();
            yield return null;
            brain = FindAnyObjectByType<CompanionBrain>();
            voice = FindAnyObjectByType<CuratorVoice>();
            if (brain == null || voice == null) { Application.Quit(2); yield break; }
            brain.SetMode(CompanionBrain.CompanionMode.FastLlm, false);
            while (brain.FastStarting || LocalAiRuntime.Instance.SpeechStarting) yield return null;
            report.startupSeconds = startup.Elapsed.TotalSeconds;
            if (!brain.FastReady || !LocalAiRuntime.Instance.SpeechReady) { Application.Quit(3); yield break; }
            report.processor = SystemInfo.processorType;
            report.processors = SystemInfo.processorCount;
            report.memoryMb = SystemInfo.systemMemorySize;
            report.gpu = SystemInfo.graphicsDeviceName;
            report.operatingSystem = SystemInfo.operatingSystem;
            report.utc = DateTime.UtcNow.ToString("O");
            report.fixturePath = fixturePath;
            Suite suite = JsonUtility.FromJson<Suite>(File.ReadAllText(fixturePath));
            if (!PrepareDoors()) { Application.Quit(4); yield break; }
            yield return null;
            string mode = null;
            foreach (Case item in suite.cases)
            {
                if (mode != item.mode)
                {
                    mode = item.mode;
                    brain.SetMode((CompanionBrain.CompanionMode)Enum.Parse(typeof(CompanionBrain.CompanionMode), mode), false);
                    while (brain.FastStarting) yield return null;
                }
                if (item.reset)
                {
                    brain.Command("stop");
                    brain.ResetConversation();
                    brain.Body.Place(home);
                    SetDoor(front, false); SetDoor(left, false); SetDoor(right, false);
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                if (item.id == "escort-social") yield return new WaitForSecondsRealtime(3);
                if (item.following) brain.Command("follow me");
                yield return Run(item);
            }
            brain.Command("stop");
            Save();
            Debug.Log("COMPANION ACCEPTANCE COMPLETE: " + report.results.Count);
            Application.Quit(0);
        }

        private bool PrepareDoors()
        {
            home = brain.transform.position;
            Vector3 forward = Vector3.zero;
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                Vector3 candidate = home + direction * 3;
                if (NavMesh.SamplePosition(candidate, out var hit, 0.5f, NavMesh.AllAreas)
                    && brain.Body.TryGetRouteLength(hit.position, out float length) && length < 5)
                { forward = direction; break; }
            }
            if (forward == Vector3.zero) return false;
            foreach (Door door in FindObjectsByType<Door>()) door.gameObject.SetActive(false);
            var host = new GameObject("Acceptance door fixtures");
            curator = new GameObject("Acceptance player").transform;
            curator.SetParent(host.transform);
            curator.position = home - forward * 0.3f;
            curator.rotation = Quaternion.LookRotation(forward);
            brain.Body.curator = curator;
            front = Door.Create("accept_front", host.transform, home + forward * 2, curator.rotation, 1, 2.6f, null, false);
            left = Door.Create("accept_left", host.transform, home - curator.right * 1.2f, curator.rotation, 1, 2.6f, null, false);
            right = Door.Create("accept_right", host.transform, home + curator.right * 1.2f, curator.rotation, 1, 2.6f, null, false);
            return true;
        }

        private static void SetDoor(Door door, bool open)
        {
            if (door.IsOpen == open) return;
            var user = door.GetComponent<Interactor>() ?? door.gameObject.AddComponent<Interactor>();
            door.TryUse(user, open ? InteractionAction.Open : InteractionAction.Close, out _);
        }

        private int DoorBits() => (front.IsOpen ? 1 : 0) | (left.IsOpen ? 2 : 0) | (right.IsOpen ? 4 : 0);

        private bool ActionStarted(string expected, int doors)
        {
            if (expected == "follow") return brain.Body.Activity == "following the curator";
            if (expected == "stop") return brain.Body.Activity == "waiting";
            if (expected == "come") return brain.Body.IsWalking || brain.Body.Activity == "waiting next to the curator";
            return doors >= 0 && (DoorBits() == doors || brain.Body.IsWalking);
        }

        private IEnumerator Run(Case item)
        {
            voice.whisper.language = (WhisperClient.SpeechLanguage)Enum.Parse(typeof(WhisperClient.SpeechLanguage), item.language);
            // Words without a language, such as "stop", are answered in the case's language.
            CompanionLanguage.Preferred = item.language == "English" ? CompanionLanguage.Language.English : CompanionLanguage.Language.Polish;
            int initialDoors = DoorBits();
            var row = new Result { id = item.id, group = item.group, mode = item.mode, input = item.text,
                audio = !string.IsNullOrEmpty(item.audio), before = brain.Body.Activity };
            var timer = Stopwatch.StartNew();
            void Heard(string text) => row.transcript = text;
            void Reply(string text)
            {
                if (row.replySeconds < 0) row.replySeconds = timer.Elapsed.TotalSeconds;
                row.reply = row.reply == null ? text : row.reply + " | " + text;
            }
            void Error(string text) => row.error = text;
            void Progress(string text) { if (row.firstTextSeconds < 0 && !string.IsNullOrWhiteSpace(text)) row.firstTextSeconds = timer.Elapsed.TotalSeconds; }
            brain.OrderReceived += Heard; brain.Spoke += Reply; voice.Failed += Error; brain.Failed += Error;
            brain.ReplyProgress += Progress;
            try
            {
                float[] samples = null;
                int sampleRate = 16000;
                if (row.audio && item.audio != "invalid") samples = ReadWave(Path.Combine(Path.GetDirectoryName(fixturePath), item.audio), out sampleRate);
                timer.Restart();
                if (item.audio == "invalid") voice.Submit(null, 0);
                else if (row.audio) voice.Submit(samples, sampleRate);
                else brain.Command(item.text);
                if (item.cancelWithStop)
                {
                    yield return new WaitForSecondsRealtime(0.05f);
                    brain.Command("stop");
                }
                if (item.disableVoice) voice.enabled = false;
                if (item.replaceWithStop)
                {
                    // Replace in-flight speech with a new audio order. Old text must never execute later.
                    yield return new WaitForSecondsRealtime(0.01f);
                    float[] stop = ReadWave(Path.Combine(Path.GetDirectoryName(fixturePath), "pl-stop.wav"), out int rate);
                    voice.Submit(stop, rate);
                }
                while (timer.Elapsed.TotalSeconds < (item.quiet || item.disableVoice ? 1 : 12))
                {
                    if (row.actionSeconds < 0 && row.transcript != null && item.expected != "preserve"
                        && ActionStarted(item.expected, item.doors)) row.actionSeconds = timer.Elapsed.TotalSeconds;
                    if (!item.quiet && !item.disableVoice && (row.reply != null || row.error != null) && !brain.IsBusy) break;
                    yield return null;
                }
                // Observe a cancelled operation long enough to detect a stale callback or queued action.
                if (item.replaceWithStop || item.disableVoice || item.cancelWithStop) yield return new WaitForSecondsRealtime(0.6f);
                row.route = brain.LastCommandRoute;
                row.after = brain.Body.Activity;
                row.doors = DoorBits();
                row.finishedSeconds = timer.Elapsed.TotalSeconds;
                bool expectedAction = item.expected == "preserve"
                    ? row.before == row.after && initialDoors == row.doors
                    : ActionStarted(item.expected, item.doors);
                row.correct = expectedAction && (item.audio == "invalid" ? row.error != null && row.transcript == null
                    : item.quiet || item.disableVoice ? row.transcript == null && row.reply == null
                    : !string.IsNullOrWhiteSpace(row.reply));
                if (item.doors >= 0) row.correct &= item.doors == row.doors;
                if (item.exact != null) row.correct &= row.reply == item.exact;
                if (item.expectedRoute != null) row.correct &= row.route == item.expectedRoute;
                if (item.differentFromPrevious) row.correct &= row.reply != previousReply;
                if (item.contains != null) row.correct &= row.reply != null && row.reply.IndexOf(item.contains, StringComparison.OrdinalIgnoreCase) >= 0;
                if (item.group.StartsWith("conversation")) row.correct &= row.route != "chat-clarify" && row.error == null;
                previousReply = row.reply;
                row.measurable = !item.quiet && !item.disableVoice && item.audio != "invalid";
                double responseTime = item.expected == "preserve" ? row.replySeconds : row.actionSeconds;
                row.underOneSecond = !row.measurable || responseTime >= 0 && responseTime <= 1;
            }
            finally
            {
                brain.OrderReceived -= Heard; brain.Spoke -= Reply; voice.Failed -= Error; brain.Failed -= Error;
                brain.ReplyProgress -= Progress;
                voice.enabled = true;
            }
            report.results.Add(row);
            Save();
            Debug.Log($"ACCEPTANCE {row.id}: correct={row.correct} <=1s={row.underOneSecond} reply={row.replySeconds:0.000}s action={row.actionSeconds:0.000}s");
        }

        private void Save() => File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));

        private static float[] ReadWave(string path, out int rate)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException(path);
            reader.BaseStream.Position = 12;
            rate = 0;
            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                string chunk = Encoding.ASCII.GetString(reader.ReadBytes(4));
                int length = reader.ReadInt32();
                long end = reader.BaseStream.Position + length + (length & 1);
                if (chunk == "fmt ")
                {
                    if (reader.ReadInt16() != 1 || reader.ReadInt16() != 1) throw new InvalidDataException(path);
                    rate = reader.ReadInt32(); reader.BaseStream.Position += 6;
                    if (reader.ReadInt16() != 16) throw new InvalidDataException(path);
                }
                else if (chunk == "data" && rate > 0)
                {
                    var samples = new float[length / 2];
                    for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
                    return samples;
                }
                reader.BaseStream.Position = end;
            }
            throw new InvalidDataException(path);
        }
    }
}
