param([string]$FixtureArgument = 'default')

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

function Write-Marker($Path, $Content) {
    [IO.File]::WriteAllText("$Path.tmp", $Content)
    [IO.File]::Move("$Path.tmp", $Path, $true)
}

Write-Marker 'server.pid' ([string]$PID)
$startup = @{
    argument = $FixtureArgument
    directory = (Get-Location).Path
    environment = $env:MOSSAGENT_MCP_FIXTURE_VALUE
} | ConvertTo-Json -Compress
Write-Marker 'startup.json' $startup

if ($env:MOSSAGENT_MCP_FIXTURE_CHILD -eq '1') {
    Start-Process pwsh -WindowStyle Hidden -WorkingDirectory (Get-Location).Path `
        -ArgumentList '-NoProfile', '-File', 'child.ps1'
}

function Write-Response($RequestId, $Result) {
    $response = @{ jsonrpc = '2.0'; id = $RequestId; result = $Result } |
        ConvertTo-Json -Depth 12 -Compress
    [Console]::Out.WriteLine($response)
    [Console]::Out.Flush()
}

while (($line = [Console]::In.ReadLine()) -ne $null) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $request = $line | ConvertFrom-Json
    if ($request.method -eq 'notifications/cancelled') {
        Write-Marker 'cancelled.json' ($request.params | ConvertTo-Json -Compress)
        Write-Response $request.params.requestId @{
            content = @(@{ type = 'text'; text = 'late cancelled response' })
            isError = $false
        }
        continue
    }
    if ($null -eq $request.id) { continue }

    switch ($request.method) {
        'initialize' {
            if ($env:MOSSAGENT_MCP_FIXTURE_MODE -eq 'hang-initialize') { continue }
            if ($env:MOSSAGENT_MCP_FIXTURE_MODE -eq 'oversized-stdout') {
                [Console]::Out.Write(('x' * (2 * 1024 * 1024 + 1)))
                [Console]::Out.Flush()
                continue
            }
            Write-Response $request.id @{
                protocolVersion = '2025-03-26'
                capabilities = @{ tools = @{} }
                serverInfo = @{ name = 'lifecycle-fixture'; version = '1.0' }
            }
        }
        'tools/list' {
            Write-Response $request.id @{
                tools = @(@{
                    name = 'echo'
                    description = 'Deterministic MCP lifecycle fixture'
                    inputSchema = @{
                        type = 'object'
                        properties = @{ message = @{ type = 'string' } }
                        required = @('message')
                    }
                })
            }
        }
        'tools/call' {
            $message = $request.params.arguments.message
            Write-Marker 'called.json' ($request.params | ConvertTo-Json -Depth 12 -Compress)
            if ($message -eq 'exit') { exit 0 }
            if ($message -eq 'wait') {
                Write-Marker 'waiting.id' ([string]$request.id)
                continue
            }
            if ($message -eq 'error') {
                $response = @{
                    jsonrpc = '2.0'
                    id = $request.id
                    error = @{ code = -32603; message = 'fixture-secret-marker' }
                } | ConvertTo-Json -Depth 12 -Compress
                [Console]::Out.WriteLine($response)
                [Console]::Out.Flush()
                continue
            }
            if ($message -eq 'large-stderr') {
                [Console]::Error.Write(('e' * (3 * 1024 * 1024)))
                [Console]::Error.Flush()
            }
            if ($message -eq 'verbose') { $message = 'x' * 200000 }
            Write-Response $request.id @{
                content = @(@{ type = 'text'; text = $message })
                isError = $false
            }
        }
    }
}
