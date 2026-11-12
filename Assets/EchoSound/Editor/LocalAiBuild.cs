using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using EchoSound.Companion;

namespace EchoSound.Editor
{
    /// <summary>Release builds must carry verified weights, native libraries and notices.</summary>
    public sealed class LocalAiBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
                throw new BuildFailedException("The offline AI bundle currently supports Windows x64 only.");
            ValidateBundle();
        }

        [MenuItem("EchoSound/Checks/Validate offline AI bundle")]
        public static void ValidateBundle()
        {
            string root = LocalAiRuntime.BundleRoot;
            string manifest = Path.Combine(root, "bundle-manifest.json");
            if (!File.Exists(manifest))
                throw new BuildFailedException("Missing AI bundle. Run Tools/Prepare-LocalAI.ps1 once on the build machine.");
            var data = JObject.Parse(File.ReadAllText(manifest));
            if ((int?)data["schema"] != 1 || (string)data["platform"] != "windows-x64" || !(data["files"] is JArray files) || files.Count == 0)
                throw new BuildFailedException("Invalid AI bundle manifest.");
            foreach (JToken file in files)
            {
                string path = Resolve(root, (string)file["path"]);
                if (!File.Exists(path) || new FileInfo(path).Length != (long)file["bytes"])
                    throw new BuildFailedException("Missing or incomplete AI file: " + path);
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(path))
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (actual != (string)file["sha256"]) throw new BuildFailedException("AI checksum mismatch: " + path);
                }
            }
            foreach (string required in new[] { "windows-x64/llama/llama-server.exe", "windows-x64/whisper/whisper-server.exe",
                "models/" + LocalAiRuntime.LanguageModel, "models/" + LocalAiRuntime.SpeechModel, "models/" + LocalAiRuntime.VadModel,
                "licenses/NOTICES.txt", "licenses/Qwen3.txt", "licenses/llama.cpp.txt", "licenses/whisper.cpp.txt",
                "licenses/Whisper-model.txt", "licenses/Silero-VAD.txt" })
            {
                bool found = false;
                foreach (JToken file in files) if ((string)file["path"] == required) found = true;
                if (!found) throw new BuildFailedException("AI manifest omits " + required);
            }
            foreach (string engine in new[] { "llama", "whisper" })
                foreach (string dll in new[] { "msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll", "vcomp140.dll" })
                {
                    string required = "windows-x64/" + engine + "/" + dll;
                    bool found = false;
                    foreach (JToken file in files) if ((string)file["path"] == required) found = true;
                    if (!found) throw new BuildFailedException("Missing app-local Visual C++ runtime: " + required);
                }
            Debug.Log("Offline AI bundle verified (" + files.Count + " files).");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            string output = report.summary.outputPath;
            string destination = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_Data", "StreamingAssets", "AI");
            string root = LocalAiRuntime.BundleRoot;
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(root, "bundle-manifest.json")));
            foreach (JToken file in manifest["files"])
            {
                string relative = (string)file["path"];
                string target = Resolve(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Resolve(root, relative), target, true);
            }
            File.Copy(Path.Combine(root, "bundle-manifest.json"), Path.Combine(destination, "bundle-manifest.json"), true);
        }

        private static string Resolve(string root, string relative)
        {
            if (string.IsNullOrEmpty(relative)) throw new BuildFailedException("Empty AI file path.");
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new BuildFailedException("Invalid AI path: " + relative);
            return path;
        }

        [MenuItem("EchoSound/Build/Windows offline companion")]
        public static void BuildWindows()
        {
            const string scene = "Assets/EchoSound/Scenes/CompanionTest.unity";
            ValidateBundle();
            if (!File.Exists(scene)) CompanionSceneSetup.CreateScene();
            if (!File.Exists(scene)) throw new BuildFailedException("Companion scene creation was cancelled.");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene }, locationPathName = "Builds/Windows/EchoSound.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Windows build failed.");
        }
    }
}
