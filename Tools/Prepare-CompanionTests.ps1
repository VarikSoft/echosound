param([string]$OutputFolder = (Join-Path (Split-Path $PSScriptRoot -Parent) '.utmp/companion-tests'))
$ErrorActionPreference = 'Stop'
$folder = [IO.Path]::GetFullPath($OutputFolder)
New-Item -ItemType Directory -Force $folder | Out-Null
Add-Type -AssemblyName System.Speech
$probe = New-Object System.Speech.Synthesis.SpeechSynthesizer
try { $voices = @($probe.GetInstalledVoices() | ForEach-Object { $_.VoiceInfo }) }
finally { $probe.Dispose() }
$phrases = [ordered]@{
    'ru-stop'='Стой.'
    'ru-follow'='Иди за мной.'
    'ru-come'='Ко мне.'
    'ru-door'='Пожалуйста, откройте дверь передо мной.'
    'ru-left'='Открой дверь слева.'
    'ru-right'='Открой дверь справа.'
    'ru-close'='Закрой дверь передо мной.'
    'ru-escort'='Иди за мной и открой все двери.'
    'ru-escort-casual'='Короче, иди за мной. И открывай все двери передо мной.'
    'ru-escort-ahead'='Иди за мной. Открывай все двери передо мной.'
    'ru-dismiss-escort'='Напарник, не иди за мной и не открывай больше двери.'
    'ru-dismiss-close'='Не ходи за мной. Закрой все двери вокруг.'
    'ru-dismiss-unclear'='Можешь больше не ходить за мной, а пройтись и закрыть двери?'
    'ru-follow-context'='Иди за мной. Короче, следи, чтобы враги меня не убили. Может, откроем дверь? Не знаю.'
    'ru-follow-cover'='Следуй за мной и прикрой меня. Может, откроем двери?'
    'ru-negative'='Не открывай дверь.'
    'ru-condition'='Если я скажу, открой дверь.'
    'ru-reported'='Мне послышалось слово стой.'
    'ru-firstperson'='Я сейчас открою дверь.'
    'ru-uneasy'='Мне тут немного жутко.'
    'ru-thanks'='Спасибо, что ты рядом.'
    'ru-name'='Меня зовут Лёня.'
    'ru-memory'='Как меня зовут?'
    'ru-social'='А как у тебя дела?'
    'ru-question'='Дверь передо мной открыта?'
    'ru-long'='Можешь подождать здесь, пока я немного осмотрюсь и попробую разобраться, куда нам дальше идти?'
    'en-stop'='Stop.'
    'en-follow'='Follow me.'
    'en-come'='Come here.'
    'en-door'='Open the door in front of me.'
    'en-social'="Hey, how are you doing?"
    'en-negative'="Don't open the door."
    'en-thanks'='Thank you.'
}
foreach ($item in $phrases.GetEnumerator()) {
    $culture = if ($item.Key.StartsWith('ru-')) { 'ru' } else { 'en' }
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
$male = $voices | Where-Object { $_.Name -eq 'Microsoft Pavel' } | Select-Object -First 1
if ($male) {
    foreach ($source in @('ru-follow','ru-come','ru-negative','ru-reported')) {
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
