using System.Diagnostics;
using System.Text;

namespace MossAgent.Browser.Tests;

internal sealed class PublishScriptSandbox : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "MossAgent-publish-tests-" + Guid.NewGuid().ToString("N"));
    public string Artifacts => Path.Combine(Root, "artifacts", "publish");

    public PublishScriptSandbox()
    {
        Directory.CreateDirectory(Path.Combine(Root, "scripts"));
        Directory.CreateDirectory(Path.Combine(Root, "src", "MossAgent.App"));
        Directory.CreateDirectory(Artifacts);
        File.WriteAllText(Path.Combine(Root, "MossAgent.slnx"), string.Empty);
        File.WriteAllText(Path.Combine(Root, "src", "MossAgent.App", "MossAgent.App.csproj"), string.Empty);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository root not found.");
        File.Copy(Path.Combine(directory.FullName, "scripts", "publish.ps1"), Path.Combine(Root, "scripts", "publish.ps1"));
    }

    public string OutputDirectory => Path.Combine(Artifacts, "win-x64");
    public string ArchivePath => Path.Combine(Artifacts, "MossAgent-0.1.0-win-x64.zip");

    public void Seed()
    {
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, "MossAgent.App.exe"), "previous executable");
        File.WriteAllText(ArchivePath, "previous archive");
    }

    public async Task<(int ExitCode, string Output)> RunAsync(
        string scenario, CancellationToken cancellationToken, string runtime = "win-x64", string version = "0.1.0")
    {
        var runner = Path.Combine(Root, "runner.ps1");
        await File.WriteAllTextAsync(runner, RunnerScript, cancellationToken);
        var start = new ProcessStartInfo("pwsh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
            WorkingDirectory = Root
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", runner })
            start.ArgumentList.Add(argument);
        start.Environment["MOSS_PUBLISH_SCENARIO"] = scenario;
        start.Environment["MOSS_PUBLISH_TEST_RUNTIME"] = runtime;
        start.Environment["MOSS_PUBLISH_TEST_VERSION"] = version;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell failed to start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try { await process.WaitForExitAsync(cancellationToken); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        return (process.ExitCode, await output + await error);
    }

    public void Dispose()
    {
        if (Directory.Exists(Artifacts) && (File.GetAttributes(Artifacts) & FileAttributes.ReparsePoint) != 0)
            Directory.Delete(Artifacts);
        Directory.Delete(Root, recursive: true);
    }

    private const string RunnerScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        function global:dotnet {
            Add-Content -LiteralPath (Join-Path $PSScriptRoot 'calls.txt') -Value ($args -join '|')
            $global:LASTEXITCODE = 0
            if ($env:MOSS_PUBLISH_SCENARIO -eq 'restore-failure') { $global:LASTEXITCODE = 1; return }
            if ($args[0] -ne 'publish') { return }
            $index = [Array]::IndexOf([object[]]$args, '-o')
            $output = $args[$index + 1]
            if ($env:MOSS_PUBLISH_SCENARIO -ne 'missing-executable') {
                Set-Content -LiteralPath (Join-Path $output 'MossAgent.App.exe') -Value 'new executable' -NoNewline
            }
            if ($env:MOSS_PUBLISH_SCENARIO -eq 'multiple-assets') {
                Set-Content -LiteralPath (Join-Path $output 'native.dll') -Value 'native asset' -NoNewline
                $driver = Join-Path $output '.playwright/package'
                New-Item -ItemType Directory -Path $driver -Force | Out-Null
                Set-Content -LiteralPath (Join-Path $driver 'cli.js') -Value 'driver asset' -NoNewline
            }
            if ($env:MOSS_PUBLISH_SCENARIO -eq 'publish-failure') { $global:LASTEXITCODE = 1 }
        }
        function global:Move-Item {
            [CmdletBinding()]
            param([string]$LiteralPath, [string]$Destination)
            if ($env:MOSS_PUBLISH_SCENARIO -eq 'install-archive-failure' -and (Split-Path -Leaf $LiteralPath) -like '.staging-*.zip') {
                throw 'Synthetic archive promotion failure'
            }
            Microsoft.PowerShell.Management\Move-Item @PSBoundParameters
        }
        if ($env:MOSS_PUBLISH_SCENARIO -eq 'junction-artifacts') {
            $target = Join-Path $PSScriptRoot 'outside'
            New-Item -ItemType Directory -Path $target | Out-Null
            Set-Content -LiteralPath (Join-Path $target 'marker.txt') -Value 'untouched' -NoNewline
            $artifacts = Join-Path $PSScriptRoot 'artifacts/publish'
            Remove-Item -LiteralPath $artifacts
            New-Item -ItemType Junction -Path $artifacts -Target $target | Out-Null
        }
        $parameters = @{
            SkipTests = $true
            Runtime = $env:MOSS_PUBLISH_TEST_RUNTIME
            Version = $env:MOSS_PUBLISH_TEST_VERSION
        }
        try { & (Join-Path $PSScriptRoot 'scripts/publish.ps1') @parameters }
        catch { Write-Output $_.Exception.Message; exit 1 }
        """;
}
