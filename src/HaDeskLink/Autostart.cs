#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace HaDeskLink;

/// <summary>
/// Manages Windows autostart (Run registry key) and Start Menu shortcut.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HA_DeskLink";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) != null;
    }

    public static void Enable()
    {
        var exePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory + "HA_DeskLink.exe";
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        key?.SetValue(AppName, $"\"{exePath}\"");
        CreateStartMenuShortcut(exePath);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        key?.DeleteValue(AppName, false);
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
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{ps.Replace("\"", "\\\"")}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            System.Diagnostics.Process.Start(psi)?.WaitForExit(5000);
        }
        catch { /* not critical */ }
    }
}