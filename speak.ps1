$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.SpeechSynthesis.SpeechSynthesizer,Windows.Media.SpeechSynthesis,ContentType=WindowsRuntime]
$streamType = [Windows.Media.SpeechSynthesis.SpeechSynthesisStream,Windows.Media.SpeechSynthesis,ContentType=WindowsRuntime]

$text = [System.IO.StreamReader]::new([Console]::OpenStandardInput(), [Text.Encoding]::UTF8).ReadToEnd().Trim()
$synth = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::new()
try {
    $voices = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices
    $voice = $voices | Where-Object { $_.Language -eq 'zh-CN' -and $_.Gender -eq 'Female' } | Select-Object -First 1
    if (-not $voice) { throw 'Install a zh-CN female Windows voice.' }
    $synth.Voice = $voice
    $synth.Options.SpeakingRate = 1.08
    $asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.IsGenericMethodDefinition -and
        $_.GetGenericArguments().Length -eq 1 -and $_.GetParameters().Length -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
    } | Select-Object -First 1
    $operation = $synth.SynthesizeTextToStreamAsync($text)
    $task = $asTask.MakeGenericMethod($streamType).Invoke($null, @($operation))
    $task.GetAwaiter().GetResult() | Out-Null
    $reader = [System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($task.Result)
    try {
        $reader.CopyTo([Console]::OpenStandardOutput())
    } finally {
        $reader.Dispose()
    }
} finally {
    $synth.Dispose()
}
