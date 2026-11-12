param([switch]$VerifyOnly, [string]$VisualStudioRoot)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$bundle = Join-Path $project 'Runtime/AI'
$cache = Join-Path $project '.utmp/ai-downloads'
$manifestPath = Join-Path $bundle 'bundle-manifest.json'

function Assert-Hash($Path, $Hash) {
    if (!(Test-Path -LiteralPath $Path) -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Hash) {
        throw "Missing or corrupt AI file: $Path"
    }
}

if ($VerifyOnly) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    foreach ($file in $manifest.files) { Assert-Hash (Join-Path $bundle $file.path) $file.sha256 }
    Write-Output "Verified $($manifest.files.Count) bundled AI files."
    exit
}

if (!(Get-Command hf -ErrorAction SilentlyContinue)) { throw 'Build prerequisite: install the Hugging Face hf CLI. Players do not need it.' }
New-Item -ItemType Directory -Force $cache, "$bundle/models", "$bundle/windows-x64/llama", "$bundle/windows-x64/whisper", "$bundle/licenses" | Out-Null

function Get-Archive($Name, $Url, $Hash) {
    $archive = Join-Path $cache "$Name.zip"
    if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $Hash) {
        Invoke-WebRequest $Url -OutFile $archive
    }
    Assert-Hash $archive $Hash
    Expand-Archive -LiteralPath $archive -DestinationPath "$cache/$Name" -Force
}

Get-Archive 'llama' 'https://github.com/ggml-org/llama.cpp/releases/download/b12291/llama-b12291-bin-win-cpu-x64.zip' '6b712208c51fa71108351f9dd4d31c7d6b9f1e3646199494698f0677008b71cb'
Get-Archive 'whisper' 'https://github.com/ggml-org/whisper.cpp/releases/download/v1.8.6/whisper-bin-x64.zip' 'b07ea0b1b4115a38e1a7b07debf581f0b77d999925f8acb8f39d322b0ba0a822'

$files = [System.Collections.Generic.List[string]]::new()
function Copy-RuntimeFile($Source, $Relative) {
    Copy-Item -LiteralPath $Source -Destination (Join-Path $bundle $Relative) -Force
    $files.Add($Relative)
}
foreach ($file in Get-ChildItem "$cache/llama" -File) {
    if ($file.Name -match '^(ggml.*\.dll|libomp\.dll|llama\.dll|llama-common\.dll|llama-server-impl\.dll|mtmd\.dll|llama-server\.exe|LICENSE.*)$') {
        Copy-RuntimeFile $file.FullName "windows-x64/llama/$($file.Name)"
    }
}
foreach ($file in Get-ChildItem "$cache/whisper/Release" -File) {
    if ($file.Name -match '^(ggml.*\.dll|whisper\.dll|whisper-server\.exe)$') {
        Copy-RuntimeFile $file.FullName "windows-x64/whisper/$($file.Name)"
    }
}

# App-local Visual C++ libraries: players must not have to run vc_redist either.
if (!$VisualStudioRoot) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $VisualStudioRoot = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    }
}
if (!$VisualStudioRoot) { throw 'Build prerequisite: Visual Studio C++ redistributable files, or pass -VisualStudioRoot.' }
$redistVersion = (Get-Content (Join-Path $VisualStudioRoot 'VC/Auxiliary/Build/Microsoft.VCRedistVersion.default.txt') -Raw).Trim()
$redist = Join-Path $VisualStudioRoot "VC/Redist/MSVC/$redistVersion/x64"
$crt = Get-ChildItem -LiteralPath $redist -Directory -Filter '*.CRT' | Select-Object -First 1
$openmp = Get-ChildItem -LiteralPath $redist -Directory -Filter '*.OpenMP' | Select-Object -First 1
if (!$crt -or !$openmp) { throw "Incomplete x64 Visual C++ redistributable directory: $redist" }
foreach ($engine in @('llama','whisper')) {
    foreach ($file in Get-ChildItem -LiteralPath $crt.FullName -Filter '*.dll') {
        Copy-RuntimeFile $file.FullName "windows-x64/$engine/$($file.Name)"
    }
    Copy-RuntimeFile (Join-Path $openmp.FullName 'vcomp140.dll') "windows-x64/$engine/vcomp140.dll"
}

$models = @(
    @{ repo='bartowski/Qwen_Qwen3-1.7B-GGUF'; revision='dcb19155b962dbb6389f4691a982043a8e651022'; name='Qwen_Qwen3-1.7B-Q4_K_M.gguf' },
    @{ repo='ggerganov/whisper.cpp'; revision='5359861c739e955e79d9a303bcbc70fb988958b1'; name='ggml-base-q5_1.bin' },
    @{ repo='ggml-org/whisper-vad'; revision='9ffd54a1e1ee413ddf265af9913beaf518d1639b'; name='ggml-silero-v5.1.2.bin' }
)
foreach ($model in $models) {
    hf download $model.repo $model.name --revision $model.revision --local-dir "$bundle/models"
    if ($LASTEXITCODE -ne 0) { throw "Model download failed: $($model.name)" }
    # The repository LFS hash is the expected SHA-256, independent of our local download.
    $metadata = Invoke-RestMethod "https://huggingface.co/api/models/$($model.repo)/tree/$($model.revision)"
    $entry = $metadata | Where-Object path -eq $model.name
    if (!$entry.lfs.oid) { throw "No upstream checksum for $($model.name)" }
    Assert-Hash "$bundle/models/$($model.name)" $entry.lfs.oid
    $files.Add("models/$($model.name)")
}

$licenses = @{
    'llama.cpp.txt'='https://raw.githubusercontent.com/ggml-org/llama.cpp/b12291/LICENSE';
    'whisper.cpp.txt'='https://raw.githubusercontent.com/ggml-org/whisper.cpp/v1.8.6/LICENSE';
    'Qwen3.txt'='https://huggingface.co/Qwen/Qwen3-1.7B-GGUF/resolve/90862c4b9d2787eaed51d12237eafdfe7c5f6077/LICENSE';
    'Whisper-model.txt'='https://raw.githubusercontent.com/openai/whisper/v20250625/LICENSE';
    'Silero-VAD.txt'='https://raw.githubusercontent.com/snakers4/silero-vad/v5.1.2/LICENSE'
}
foreach ($name in $licenses.Keys) {
    Invoke-WebRequest $licenses[$name] -OutFile "$bundle/licenses/$name"
    $files.Add("licenses/$name")
}
Copy-RuntimeFile "$PSScriptRoot/LocalAI-NOTICES.txt" 'licenses/NOTICES.txt'
$entries = foreach ($relative in $files) {
    $item = Get-Item -LiteralPath (Join-Path $bundle $relative)
    @{ path=$relative; bytes=$item.Length; sha256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
}
@{ schema=1; platform='windows-x64'; llama='b12291'; whisper='v1.8.6'; visualCpp=$redistVersion; models=$models; files=@($entries) } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Output "Prepared offline AI bundle: $($files.Count) files. Build the game in Unity; ship the whole build folder."
