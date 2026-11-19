param([string]$OutputFolder)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (!$OutputFolder) { $OutputFolder = Join-Path $projectRoot '.utmp/panic-mimic-tests' }
$folder = [IO.Path]::GetFullPath($OutputFolder)
New-Item -ItemType Directory -Force $folder | Out-Null
$utterance = 'БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик....'
Add-Type -AssemblyName System.Speech
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $voice = $speaker.GetInstalledVoices() | ForEach-Object { $_.VoiceInfo } | Where-Object { $_.Culture.TwoLetterISOLanguageName -eq 'ru' } | Select-Object -First 1
    if (!$voice) { throw 'A Russian Windows voice is required for the synthetic recording.' }
    $speaker.SelectVoice($voice.Name)
    $format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
    $speaker.SetOutputToWaveFile((Join-Path $folder 'panic.wav'), $format)
    $speaker.Speak($utterance)
    @{synthetic=$true;voice=$voice.Name;text=$utterance;sampleRate=16000} | ConvertTo-Json | Set-Content (Join-Path $folder 'audio.json') -Encoding utf8
}
finally { $speaker.Dispose() }
$cases = [System.Collections.Generic.List[object]]::new()
foreach ($mode in @('FastLlm','FreeDialogue')) {
    foreach ($known in @($false,$true)) {
        foreach ($audio in @($false,$true)) {
            foreach ($repeat in 1..3) {
                $prefix = "$mode-known$known-audio$audio-$repeat"
                if ($known) {
                    $cases.Add(@{id="$prefix-intro";group='setup';text='Меня зовут Саша.';mode=$mode;reset=$true;expected='preserve';exact='Понял, Саша.'})
                }
                $case = @{id="$prefix-target";group='compound';text=$utterance;mode=$mode;reset=(!$known);following=$true;expected='preserve';language='Russian'}
                if ($known) { $case.contains = 'Саша' }
                if ($audio) { $case.audio = 'panic.wav' }
                $cases.Add($case)
                $expectedName = if ($known) { 'Тебя зовут Саша.' } else { 'Ты ещё не называл своё имя.' }
                $cases.Add(@{id="$prefix-control";group='name-control';text='Как меня зовут?';mode=$mode;reset=$false;expected='preserve';exact=$expectedName})
            }
        }
    }
}
@{cases=@($cases.ToArray())} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $folder 'suite.json') -Encoding utf8
Write-Output "Prepared $($cases.Count) checks: 24 verbatim utterances plus introductions/name controls. Voice=$($voice.Name)"
