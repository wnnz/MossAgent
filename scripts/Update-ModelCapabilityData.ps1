param([string]$OutputPath = (Join-Path $PSScriptRoot '../src/MossAgent.Application/Data/model-capabilities.json'))

$ErrorActionPreference = 'Stop'
$source = 'https://models.dev/api.json'
$response = Invoke-WebRequest $source -TimeoutSec 30
$bytes = [Text.Encoding]::UTF8.GetBytes($response.Content)
if ($bytes.Length -gt 20MB) { throw 'Model metadata exceeds the 20 MiB maintenance limit.' }
$catalog = $response.Content | ConvertFrom-Json
$models = foreach ($provider in @('openai', 'anthropic', 'google', 'deepseek', 'moonshotai', 'zai', 'xai', 'alibaba', 'minimax')) {
    foreach ($property in ($catalog.$provider.models.PSObject.Properties | Sort-Object Name)) {
        $model = $property.Value
        if (!$model.tool_call -or $model.modalities.output -notcontains 'text') { continue }
        if ($provider -eq 'alibaba' -and $model.id -notmatch '^(qwen|qwq|qvq)') { continue }
        if ($model.id -match 'realtime|live-preview|computer-use|deep-research') { continue }
        if ($model.limit.context -lt 1024 -or $model.limit.context -gt 4000000 -or $model.limit.output -lt 256) { continue }
        $efforts = @($model.reasoning_options | Where-Object type -eq 'effort' | ForEach-Object values)
        if ($efforts | Where-Object { $_ -cnotmatch '^[a-z][a-z0-9_-]{0,63}$' }) { throw 'Invalid reasoning effort in metadata.' }
        [ordered]@{
            provider = $provider
            modelId = $model.id
            contextLength = [int]$model.limit.context
            maximumOutputTokens = [int]$model.limit.output
            supportsImages = $model.modalities.input -contains 'image'
            supportsFiles = $model.modalities.input -contains 'pdf'
            supportsReasoning = [bool]$model.reasoning
            reasoningEfforts = $efforts
        } | ConvertTo-Json -Depth 4 -Compress
    }
}
if (@($models).Count -lt 30) { throw 'Incomplete model capability catalog; keep the previous snapshot.' }
$hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
$date = [DateTimeOffset]::UtcNow.ToString('O')
$json = "{`n  `"schemaVersion`": 1,`n  `"source`": `"$source`",`n  `"sourceSha256`": `"$hash`",`n  `"retrievedAt`": `"$date`",`n  `"models`": [`n    " + ($models -join ",`n    ") + "`n  ]`n}`n"
$target = [IO.Path]::GetFullPath($OutputPath)
$temporary = "$target.$([Guid]::NewGuid().ToString('N')).tmp"
try {
    [IO.File]::WriteAllText($temporary, $json, [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temporary, $target, $true)
} finally {
    if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
}
Write-Output "Saved $(@($models).Count) model capabilities to $target"
