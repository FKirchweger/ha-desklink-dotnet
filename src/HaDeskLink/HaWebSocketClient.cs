#nullable enable
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Connects to Home Assistant via WebSocket to receive push notifications.
/// Uses the mobile_app push_websocket_channel protocol so no inbound port/IP is needed.
/// Works for all users regardless of network setup.
/// </summary>
public class HaWebSocketClient : IDisposable
{
    private readonly string _haUrl;
    private readonly string _token;
    private readonly NotifyIcon? _trayIcon;
    private readonly Action<string>? _onCommand;
    private System.Net.WebSockets.ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private int _msgId = 1;

    public HaWebSocketClient(string haUrl, string token, NotifyIcon? trayIcon, Action<string>? onCommand = null)
    {
        _haUrl = haUrl.TrimEnd('/');
        _token = token;
        _trayIcon = trayIcon;
        _onCommand = onCommand;
    }

    public async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();
        var wsUrl = _haUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/api/websocket";

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                _ws = new System.Net.WebSockets.ClientWebSocket();
                await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);

                // Step 1: Receive auth_required message
                var buffer = new byte[8192];
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);

                if (!msg.Contains("auth_required"))
                    throw new Exception("Expected auth_required from HA");

                // Step 2: Send auth
                var authMsg = JsonSerializer.Serialize(new { type = "auth", access_token = _token });
                var authBytes = Encoding.UTF8.GetBytes(authMsg);
                await _ws.SendAsync(new ArraySegment<byte>(authBytes), System.Net.WebSockets.WebSocketMessageType.Text, true, _cts.Token);

                // Step 3: Receive auth_ok
                result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (!msg.Contains("auth_ok"))
                    throw new Exception("Auth failed");

                // Step 4: Subscribe to mobile_app push notification channel
                var subMsg = JsonSerializer.Serialize(new
                {
                    id = _msgId++,
                    type = "mobile_app/push_notification_channel",
                    webhook_id = "" // Will be set externally
                });

                // Listen for messages
                await ListenLoop(buffer);
            }
            catch (System.Net.WebSockets.WebSocketException)
            {
                // Reconnect after delay
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch { }

            if (!_cts.Token.IsCancellationRequested)
                await Task.Delay(5000, _cts.Token); // Wait 5s before reconnect
        }
    }

    private async Task ListenLoop(byte[] buffer)
    {
        while (_ws?.State == System.Net.WebSockets.WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
        {
            try
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                    break;

                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // Handle fragmented messages
                while (!result.EndOfMessage)
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                        return;
                }

                // Process notification
                ProcessMessage(msg);
            }
            catch (OperationCanceledException) { break; }
            catch { break; }
        }
    }

    private void ProcessMessage(string msg)
    {
        try
        {
            var doc = JsonDocument.Parse(msg);
            var root = doc.RootElement;

            // HA sends push notifications via websocket events
            // The event type is "mobile_app_push_notification"
            if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "event")
            {
                if (root.TryGetProperty("event", out var eventEl))
                {
                    if (eventEl.TryGetProperty("data", out var dataEl))
                    {
                        var json = dataEl.GetRawText();
                        NotificationHandler.TryHandleNotification(json, _trayIcon);
                    }
                }
            }
        }
        catch { }
    }

    public async Task SubscribeToNotificationsAsync(string webhookId)
    {
        if (_ws?.State != System.Net.WebSockets.WebSocketState.Open) return;

        var subMsg = JsonSerializer.Serialize(new
        {
            id = _msgId++,
            type = "mobile_app/push_notification_channel",
            webhook_id = webhookId
        });
        var bytes = Encoding.UTF8.GetBytes(subMsg);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), System.Net.WebSockets.WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _ws?.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Stopping", CancellationToken.None); } catch { }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _ws?.Dispose();
            _cts?.Dispose();
            _disposed = true;
        }
    }
}