#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace HaDeskLink;

/// <summary>
/// Manages Windows autostart using Task Scheduler (runs with highest privileges, no UAC prompt).
/// Falls back to registry Run key if Task Scheduler is unavailable.
/// </summary>
public static class Autostart
{
    private const string TaskName = "HA_DeskLink";
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HA_DeskLink";

    public static bool IsEnabled()
    {
        // Check Task Scheduler first
        if (IsTaskSchedulerEnabled())
            return true;
        // Fallback: check registry
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) != null;
    }

    public static void Enable()
    {
        var exePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory + "HA_DeskLink.exe";

        // Try Task Scheduler first (runs with highest privileges = no UAC prompt on autostart)
        if (TryCreateScheduledTask(exePath))
        {
            // Remove registry entry if it exists (avoid duplicate startup)
            RemoveRegistryEntry();
            CreateStartMenuShortcut(exePath);
            return;
        }

        // Fallback: registry Run key (will trigger UAC prompt on autostart)
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        key?.SetValue(AppName, $"\"{exePath}\"");
        CreateStartMenuShortcut(exePath);
    }

    public static void Disable()
    {
        RemoveScheduledTask();
        RemoveRegistryEntry();
    }

    private static bool TryCreateScheduledTask(string exePath)
    {
        try
        {
            // Delete existing task first (to update if path changed)
            RunSchtasks($"/delete /tn \"{TaskName}\" /f", ignoreError: true);

            // Create task: run at logon, with highest privileges, no UAC prompt
            var result = RunSchtasks($"/create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\"\" " +
                $"/sc onlogon /rl highest /f");

            return result;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTaskSchedulerEnabled()
    {
        try
        {
            var result = RunSchtasks($"/query /tn \"{TaskName}\"", ignoreError: true);
            return result;
        }
        catch
        {
            return false;
        }
    }

    private static void RemoveScheduledTask()
    {
        try
        {
            RunSchtasks($"/delete /tn \"{TaskName}\" /f", ignoreError: true);
        }
        catch { }
    }

    private static void RemoveRegistryEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.DeleteValue(AppName, false);
        }
        catch { }
    }

    private static bool RunSchtasks(string arguments, bool ignoreError = false)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(10000);
            return proc.ExitCode == 0 || ignoreError;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Create a Start Menu shortcut using PowerShell (no COM dependency).</summary>
    private static void CreateStartMenuShortcut(string exePath)
    {
        try
        {
            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
            var lnkPath = Path.Combine(startMenu, "HA DeskLink.lnk");
            if (File.Exists(lnkPath)) return; // already exists

            var ps = $@"
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut('{lnkPath}')
$sc.TargetPath = '{exePath}'
$sc.WorkingDirectory = '{Path.GetDirectoryName(exePath)}'
$sc.Description = 'HA DeskLink - Home Assistant Companion'
$sc.Save()
";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{ps.Replace("\"", "\\\"")}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            Process.Start(psi)?.WaitForExit(5000);
        }
        catch { /* not critical */ }
    }
}