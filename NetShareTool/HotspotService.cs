using System.Diagnostics;

namespace NetShareTool;

public sealed class HotspotService
{
    public async Task<HotspotStartResult> StartMobileHotspotAsync()
    {
        var script = """
            $ErrorActionPreference = 'Stop'
            Add-Type -AssemblyName System.Runtime.WindowsRuntime
            [Windows.Networking.Connectivity.NetworkInformation,Windows.Networking.Connectivity,ContentType=WindowsRuntime] | Out-Null
            [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime] | Out-Null

            $profile = [Windows.Networking.Connectivity.NetworkInformation]::GetInternetConnectionProfile()
            if ($null -eq $profile) {
                throw '没有找到当前 Internet 连接配置。'
            }

            $manager = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager]::CreateFromConnectionProfile($profile)
            if ($manager.TetheringOperationalState -eq 1) {
                Write-Output '移动热点已经开启。'
                exit 0
            }

            $async = $manager.StartTetheringAsync()
            while ($async.Status -eq 0) {
                Start-Sleep -Milliseconds 100
            }

            $result = $async.GetResults()
            Write-Output "StartTethering result: $($result.Status)"
            if ($result.Status -ne 0) {
                exit 2
            }
            """;

        var result = await RunPowerShellAsync(script);
        if (result.ExitCode == 0)
        {
            return new HotspotStartResult(true, result.Output);
        }

        AppLogger.Info($"移动热点接口启动失败，尝试打开设置页。输出：{result.Output} 错误：{result.Error}");
        OpenMobileHotspotSettings();
        return new HotspotStartResult(false, $"{result.Output}{Environment.NewLine}{result.Error}".Trim());
    }

    public void OpenMobileHotspotSettings()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "ms-settings:network-mobilehotspot",
            UseShellExecute = true
        });
    }

    private static async Task<ProcessResult> RunPowerShellAsync(string script)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command {Quote(script)}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ProcessResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "; ") + "\"";
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}

public sealed record HotspotStartResult(bool Started, string Message);
