using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;

namespace EchoSound.Companion
{
    /// <summary>Owns the bundled CPU servers. Nothing is installed or downloaded on a player's PC.</summary>
    public sealed class LocalAiRuntime : MonoBehaviour
    {
        public const string LanguageModel = "Qwen_Qwen3-1.7B-Q4_K_M.gguf";
        public const string SpeechModel = "ggml-base-q5_1.bin";
        public const string VadModel = "ggml-silero-v5.1.2.bin";
        private static LocalAiRuntime instance;
        public static LocalAiRuntime Instance
        {
            get
            {
                if (instance == null)
                {
                    var host = new GameObject("Local AI runtime");
                    instance = host.AddComponent<LocalAiRuntime>();
                    DontDestroyOnLoad(host);
                }
                return instance;
            }
        }

        public bool ChatReady { get; private set; }
        public bool SpeechReady { get; private set; }
        public bool ChatStarting { get; private set; }
        public bool SpeechStarting { get; private set; }
        public string ChatError { get; private set; }
        public string SpeechError { get; private set; }
        public string ChatUrl { get; private set; }
        public string SpeechUrl { get; private set; }
        public string ApiKey { get; private set; }
        private OwnedServer chat;
        private OwnedServer speech;
        private Coroutine chatStartup;

        public static string BundleRoot => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Runtime/AI"))
            : Path.Combine(Application.streamingAssetsPath, "AI");

        private void Awake()
        {
            instance = this;
            ApiKey = Guid.NewGuid().ToString("N");
            SpeechStarting = true;
            StartCoroutine(StartServer(false));
        }

        // Fast gameplay needs speech only. Load the large model on the first free-dialogue request.
        public void EnsureChatStarted()
        {
            if (ChatReady || ChatStarting || ChatError != null) return;
            ChatStarting = true;
            chatStartup = StartCoroutine(StartServer(true));
        }

        public void StopChat()
        {
            if (chatStartup != null) StopCoroutine(chatStartup);
            chatStartup = null;
            chat?.Dispose();
            chat = null;
            ChatStarting = ChatReady = false;
            ChatError = null;
        }

        private IEnumerator StartServer(bool language)
        {
            string error = null;
            OwnedServer server = null;
            string endpoint = null;
            try
            {
                if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
                    throw new NotSupportedException("The bundled AI currently requires Windows x64.");
                string folder = Path.Combine(BundleRoot, "windows-x64", language ? "llama" : "whisper");
                string executable = Path.Combine(folder, language ? "llama-server.exe" : "whisper-server.exe");
                string model = Path.Combine(BundleRoot, "models", language ? LanguageModel : SpeechModel);
                string vad = Path.Combine(BundleRoot, "models", VadModel);
                foreach (string file in language ? new[] { executable, model } : new[] { executable, model, vad })
                    if (!File.Exists(file)) throw new FileNotFoundException("AI bundle is incomplete", file);
                int port = FreePort();
                int threads = Mathf.Clamp(SystemInfo.processorCount / 2, 1, 4);
                endpoint = "http://127.0.0.1:" + port;
                string args = language
                    ? $"-m {Quote(model)} --host 127.0.0.1 --port {port} -ngl 0 -c 4096 -np 1 -t {threads} -tb {threads} --jinja --reasoning-budget 0 --api-key {ApiKey}"
                    : $"-m {Quote(model)} --host 127.0.0.1 --port {port} -ng -t {threads} --vad -vm {Quote(vad)} -vp 300 -nlp";
                server = new OwnedServer(executable, args);
                if (language) { chat = server; ChatUrl = endpoint + "/v1/chat/completions"; }
                else { speech = server; SpeechUrl = endpoint + "/inference"; }
            }
            catch (Exception exception) { error = exception.Message; }

            float deadline = Time.realtimeSinceStartup + 120;
            while (error == null && Time.realtimeSinceStartup < deadline)
            {
                if (server.HasExited) { error = server.Diagnostic; break; }
                using (var request = UnityWebRequest.Get(endpoint + (language ? "/health" : "/")))
                {
                    request.timeout = 2;
                    if (language) request.SetRequestHeader("Authorization", "Bearer " + ApiKey);
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success && !server.HasExited)
                    {
                        if (language) { ChatReady = true; ChatStarting = false; }
                        else { SpeechReady = true; SpeechStarting = false; }
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Fail(language, error ?? "Startup timed out. " + server?.Diagnostic);
        }

        private void Update()
        {
            if (ChatReady && chat.HasExited) Fail(true, chat.Diagnostic);
            if (SpeechReady && speech.HasExited) Fail(false, speech.Diagnostic);
        }

        private void Fail(bool language, string diagnostic)
        {
            UnityEngine.Debug.LogWarning("[Local AI] " + (language ? "Language: " : "Speech: ") + diagnostic);
            if (language)
            {
                ChatStarting = ChatReady = false;
                ChatError = "Free conversation is unavailable. You can still use follow, stop and come here.";
                chat?.Dispose();
                chat = null;
            }
            else
            {
                SpeechStarting = SpeechReady = false;
                SpeechError = "Voice recognition is unavailable. Press Enter to type an order.";
                speech?.Dispose();
                speech = null;
            }
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
            finally { listener.Stop(); }
        }

        private static string Quote(string path) => "\"" + path + "\"";
        private void OnApplicationQuit() => Shutdown();
        private void OnDestroy() { Shutdown(); if (instance == this) instance = null; }
        private void Shutdown()
        {
            StopAllCoroutines();
            chat?.Dispose(); chat = null;
            speech?.Dispose(); speech = null;
            ChatReady = SpeechReady = false;
        }

        private sealed class OwnedServer : IDisposable
        {
            private readonly Process process;
            private readonly ChildProcessJob job;
            private readonly object gate = new object();
            private string diagnostic = "Server exited before it was ready.";
            public bool HasExited => process.HasExited;
            public string Diagnostic { get { lock (gate) return diagnostic; } }
            public OwnedServer(string executable, string arguments)
            {
                job = new ChildProcessJob();
                process = new Process
                {
                    StartInfo = new ProcessStartInfo(executable, arguments)
                    {
                        WorkingDirectory = Path.GetDirectoryName(executable),
                        UseShellExecute = false, CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true, RedirectStandardError = true
                    }
                };
                process.OutputDataReceived += Record;
                process.ErrorDataReceived += Record;
                try
                {
                    process.Start();
                    job.Add(process);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                catch { Dispose(); throw; }
            }
            private void Record(object sender, DataReceivedEventArgs args)
            {
                if (string.IsNullOrWhiteSpace(args.Data)) return;
                lock (gate)
                {
                    diagnostic += "\n" + args.Data;
                    if (diagnostic.Length > 4096) diagnostic = diagnostic.Substring(diagnostic.Length - 4096);
                }
            }
            public void Dispose()
            {
                try { if (!process.HasExited) process.Kill(); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                job.Dispose();
                process.Dispose();
            }
        }
    }
}
