#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace HaDeskLink;

/// <summary>
/// Embedded HA Dashboard using WebView2.
/// Automatically installs WebView2 Runtime if missing.
/// </summary>
public class DashboardWindow : Form
{
    private WebView2? _webView;
    private readonly string _haUrl;
    private static bool _installPrompted = false;

    public DashboardWindow(string haUrl)
    {
        _haUrl = haUrl;
        Text = "HA DeskLink - Dashboard";
        Size = new System.Drawing.Size(1300, 850);
        MinimumSize = new System.Drawing.Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        try
        {
            await _webView.EnsureCoreWebView2Async(null);
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Navigate(_haUrl);
        }
        catch (Exception)
        {
            // WebView2 Runtime missing – offer to install
            if (!_installPrompted)
            {
                _installPrompted = true;
                Close(); // Close empty window first

                var result = MessageBox.Show(
                    "WebView2 Runtime wird f\u00fcr das eingebettete Dashboard ben\u00f6tigt.\n\n" +
                    "Jetzt herunterladen und installieren?\n" +
                    "(Danach App neu starten)",
                    "WebView2 fehlt",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        // Download and run WebView2 bootstrapper
                        var url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
                        var tmpPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
                        using var http = new HttpClient();
                        var bytes = await http.GetByteArrayAsync(url);
                        File.WriteAllBytes(tmpPath, bytes);
                        Process.Start(new ProcessStartInfo(tmpPath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Download fehlgeschlagen: {ex.Message}\n\n" +
                            "Bitte manuell installieren:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                            "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    // Fallback: open in default browser
                    Process.Start(new ProcessStartInfo(_haUrl) { UseShellExecute = true });
                }
            }
            else
            {
                Close();
                Process.Start(new ProcessStartInfo(_haUrl) { UseShellExecute = true });
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _webView?.Dispose();
        base.OnFormClosing(e);
    }

    private static DashboardWindow? _instance;

    public static void Open(string haUrl)
    {
        if (_instance != null && !_instance.IsDisposed)
        {
            _instance.Activate();
            return;
        }
        _instance = new DashboardWindow(haUrl);
        _instance.Show();
    }
}