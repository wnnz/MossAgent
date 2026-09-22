$ErrorActionPreference = 'Stop'

while (($line = [Console]::In.ReadLine()) -ne $null) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    $request = $line | ConvertFrom-Json
    if ($null -eq $request.id) {
        continue
    }

    switch ($request.method) {
        'initialize' {
            $result = @{
                protocolVersion = '2025-03-26'
                capabilities = @{ tools = @{} }
                serverInfo = @{ name = 'fixture'; version = '1.0' }
            }
        }
        'tools/list' {
            $result = @{
                tools = @(
                    @{
                        name = 'echo'
                        description = 'Echo a message'
                        inputSchema = @{
                            type = 'object'
                            properties = @{ message = @{ type = 'string' } }
                            required = @('message')
                        }
                        annotations = @{ readOnlyHint = $true }
                    }
                )
            }
        }
        'tools/call' {
            $result = @{
                content = @(@{ type = 'text'; text = $request.params.arguments.message })
                isError = $false
            }
        }
        default {
            $response = @{
                jsonrpc = '2.0'
                id = $request.id
                error = @{ code = -32601; message = 'Method not found' }
            } | ConvertTo-Json -Depth 12 -Compress
            [Console]::Out.WriteLine($response)
            [Console]::Out.Flush()
            continue
        }
    }

    $response = @{
        jsonrpc = '2.0'
        id = $request.id
        result = $result
    } | ConvertTo-Json -Depth 12 -Compress
    [Console]::Out.WriteLine($response)
    [Console]::Out.Flush()
}

