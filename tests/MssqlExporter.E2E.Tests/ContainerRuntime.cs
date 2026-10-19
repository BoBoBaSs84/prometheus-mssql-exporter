using System.Diagnostics;

namespace MssqlExporter.E2E.Tests;

/// <summary>
/// Testcontainers supports Docker and Podman; the suite is skipped only when neither is reachable
/// (e.g. a runner without a container runtime).
/// </summary>
internal static class ContainerRuntime
{
    public static bool IsAvailable() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")) || CanRun("docker") || CanRun("podman");

    private static bool CanRun(string binary)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(binary, "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return false;
            }

            // Drain so a chatty `info` cannot block on a full pipe.
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(TimeSpan.FromSeconds(20)))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
