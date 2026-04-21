#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaDeskLink;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Global exception handler - log errors instead of silent crash
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var msg = e.ExceptionObject.ToString();
            File.WriteAllText(LogFile(), $"[CRASH] {DateTime.Now}\n{msg}");
            MessageBox.Show($"Schwerer Fehler:\n{msg}", "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        Application.ThreadException += (s, e) =>
        {
            File.WriteAllText(LogFile(), $"[UI-ERROR] {DateTime.Now}\n{e.Exception}");
            MessageBox.Show($"Fehler:\n{e.Exception.Message}", "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        try
        {
            // Request admin rights if not running as admin (needed for LibreHardwareMonitor)
            if (!IsRunningAsAdmin())
            {
                try
                {
                    var exePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory + "HA_DeskLink.exe";
                    var psi = new ProcessStartInfo(exePath)
                    {
                        UseShellExecute = true,
                        Verb = "runas",
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                    };
                    Process.Start(psi);
                    return; // Exit this non-admin instance
                }
                catch
                {
                    // User declined UAC – continue without admin (sensors may not work)
                }
            }

            var config = Config.Load();
            var configDir = Config.GetConfigDir();

            if (!File.Exists(Path.Combine(configDir, "registration.json")))
            {
                using var wizard = new SetupWizard();
                if (wizard.ShowDialog() == DialogResult.OK)
                {
                    config.HaUrl = wizard.HaUrl;
                    config.HaToken = wizard.HaToken;
                    config.VerifySsl = wizard.VerifySsl;
                    config.Save();
                }
                else return;
            }

            new DeskLinkApp(config).Run();
        }
        catch (Exception ex)
        {
            File.WriteAllText(LogFile(), $"[STARTUP] {DateTime.Now}\n{ex}");
            MessageBox.Show($"Startfehler:\n{ex.Message}\n\nLog: {LogFile()}", "HA DeskLink",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static string LogFile()
    {
        var dir = Config.GetConfigDir();
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "error.log");
    }

    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }
}