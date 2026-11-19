param([string]$OutputFolder = (Join-Path (Split-Path $PSScriptRoot -Parent) '.utmp/companion-tests'))
$ErrorActionPreference = 'Stop'
$folder = [IO.Path]::GetFullPath($OutputFolder)
New-Item -ItemType Directory -Force $folder | Out-Null
Add-Type -AssemblyName System.Speech
$probe = New-Object System.Speech.Synthesis.SpeechSynthesizer
try { $voices = @($probe.GetInstalledVoices() | ForEach-Object { $_.VoiceInfo }) }
finally { $probe.Dispose() }
$phrases = [ordered]@{
    'pl-stop'='Stój.'
    'pl-follow'='Idź za mną.'
    'pl-come'='Podejdź do mnie.'
    'pl-door'='Proszę, otwórz drzwi przede mną.'
    'pl-left'='Otwórz drzwi po lewej.'
    'pl-right'='Otwórz drzwi po prawej.'
    'pl-close'='Zamknij drzwi przede mną.'
    'pl-escort'='Idź za mną i otwórz wszystkie drzwi.'
    'pl-dismiss'='Echo, nie idź za mną i nie otwieraj już drzwi.'
    'pl-follow-cover'='Idź za mną i osłaniaj mnie.'
    'pl-negative'='Nie otwieraj drzwi.'
    'pl-condition'='Jeśli powiem, otwórz drzwi.'
    'pl-firstperson'='Zaraz otworzę drzwi.'
    'pl-uneasy'='Trochę się boję.'
    'pl-thanks'='Dziękuję.'
    'pl-name'='Mam na imię Łucja.'
    'pl-memory'='Jak mam na imię?'
    'pl-social'='Cześć, jak się czujesz?'
    'pl-question'='Czy drzwi przede mną są otwarte?'
    'pl-long'='Możesz poczekać tutaj, aż się rozejrzę i sprawdzę, dokąd iść dalej?'
    'en-stop'='Stop.'
    'en-follow'='Follow me.'
    'en-come'='Come here.'
    'en-door'='Open the door in front of me.'
    'en-escort'='Follow me and open all the doors on the way.'
    'en-dismiss-close'='Stop following me and close all the doors.'
    'en-follow-cover'='Follow me and cover me.'
    'en-social'="Hey, how are you doing?"
    'en-negative'="Don't open the door."
    'en-reported'='I heard someone say stop.'
    'en-thanks'='Thank you.'
}
foreach ($item in $phrases.GetEnumerator()) {
    $culture = if ($item.Key.StartsWith('pl-')) { 'pl' } else { 'en' }
    $voice = $voices | Where-Object { $_.Culture.TwoLetterISOLanguageName -eq $culture } | Select-Object -First 1
    if (!$voice) { Write-Warning "No installed $culture voice. Fixture $($item.Key) is unavailable."; continue }
    $speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
    try {
        $speaker.SelectVoice($voice.Name)
        $format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
        $speaker.SetOutputToWaveFile((Join-Path $folder ($item.Key + '.wav')), $format)
        $speaker.Speak($item.Value)
    } finally { $speaker.Dispose() }
}
# A second, male English voice. Windows ships only one Polish voice (Microsoft Paulina).
$male = $voices | Where-Object { $_.Name -eq 'Microsoft David' } | Select-Object -First 1
if ($male) {
    foreach ($source in @('en-follow','en-come','en-negative','en-reported')) {
        $name = $source + '-male'
        $phrases[$name] = $phrases[$source]
        $speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
        try {
            $speaker.SelectVoice($male.Name)
            $speaker.SetOutputToWaveFile((Join-Path $folder ($name + '.wav')), $format)
            $speaker.Speak($phrases[$name])
        } finally { $speaker.Dispose() }
    }
}
$voices | Select-Object Name,@{n='culture';e={$_.Culture.Name}} | ConvertTo-Json | Set-Content (Join-Path $folder 'voices.json') -Encoding utf8
$phrases | ConvertTo-Json | Set-Content (Join-Path $folder 'phrases.json') -Encoding utf8
Write-Output "Generated synthetic PCM16 fixtures in $folder"
