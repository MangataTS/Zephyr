using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Serilog;
using ZephyrDesktop.Events;

namespace ZephyrDesktop.Services;

public sealed class WebSocketService : IDisposable
{
    private readonly IConfiguration _config;
    private readonly TokenStorage _tokenStorage;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Timer? _heartbeatTimer;
    private Timer? _reconnectTimer;
    private int _reconnectDelay = 1000;
    private const int MaxReconnectDelay = 30000;
    private const int HeartbeatIntervalMs = 30000;
    private bool _intentionalStop;
    private DateTime _lastReceivedTime;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public WebSocketService(IConfiguration config, TokenStorage tokenStorage)
    {
        _config = config;
        _tokenStorage = tokenStorage;
    }

    public async Task ConnectAsync()
    {
        _intentionalStop = false;

        var token = await _tokenStorage.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            ScheduleReconnect();
            return;
        }

        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            _webSocket?.Dispose();
            _webSocket = new ClientWebSocket();

            var wsBaseUrl = _config["WebSocketUrl"] ?? "ws://localhost:8090";
            var uri = new Uri($"{wsBaseUrl}/ws/user?token={token}");

            await _webSocket.ConnectAsync(uri, _cts.Token);

            _reconnectDelay = 1000;
            _lastReceivedTime = DateTime.UtcNow;

            StartHeartbeat();
            _ = ReceiveLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "WS.Connect");
            ScheduleReconnect();
        }
    }

    public async Task DisconnectAsync()
    {
        _intentionalStop = true;
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
        _cts?.Cancel();

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "disconnect", CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Step}]", "WS.Disconnect");
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                _lastReceivedTime = DateTime.UtcNow;

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (!_intentionalStop)
                        ScheduleReconnect();
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    if (result.EndOfMessage)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        HandleMessage(json);
                    }
                    else
                    {
                        using var ms = new MemoryStream();
                        ms.Write(buffer, 0, result.Count);

                        while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
                        {
                            var next = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                            _lastReceivedTime = DateTime.UtcNow;

                            if (next.MessageType == WebSocketMessageType.Close)
                            {
                                if (!_intentionalStop)
                                    ScheduleReconnect();
                                return;
                            }

                            ms.Write(buffer, 0, next.Count);

                            if (next.EndOfMessage)
                                break;
                        }

                        var json = Encoding.UTF8.GetString(ms.ToArray());
                        HandleMessage(json);
                    }
                }
            }
        }
        catch
        {
            if (!_intentionalStop)
                ScheduleReconnect();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("event", out var eventEl)) return;
            var eventType = eventEl.GetString();

            switch (eventType)
            {
                case "note:assigned":
                    HandleNoteAssigned(root);
                    WeakReferenceMessenger.Default.Send(new SyncTriggerEvent());
                    break;
                case "note:reminded":
                    HandleNoteReminded(root);
                    WeakReferenceMessenger.Default.Send(new SyncTriggerEvent());
                    break;
                case "note:archived":
                    HandleNoteArchived(root);
                    WeakReferenceMessenger.Default.Send(new SyncTriggerEvent());
                    break;
                default:
                    WeakReferenceMessenger.Default.Send(new SyncTriggerEvent());
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "WS.HandleMessage");
        }
    }

    private void HandleNoteAssigned(JsonElement root)
    {
        var noteId = root.TryGetProperty("note_id", out var nid) ? nid.GetString() ?? "" : "";
        var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var fromName = root.TryGetProperty("from_name", out var fn) ? fn.GetString() ?? "" : "";

        WeakReferenceMessenger.Default.Send(new NoteAssignedEvent(noteId, title, fromName));
    }

    private void HandleNoteReminded(JsonElement root)
    {
        var noteId = root.TryGetProperty("note_id", out var nid) ? nid.GetString() ?? "" : "";
        var reminderName = root.TryGetProperty("reminder_name", out var rn) ? rn.GetString() ?? "" : "";
        var message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        var colorStatus = root.TryGetProperty("color_status", out var cs) ? cs.GetString() ?? "red" : "red";

        WeakReferenceMessenger.Default.Send(new NoteRemindedEvent(noteId, reminderName, message, colorStatus));
    }

    private void HandleNoteArchived(JsonElement root)
    {
        var noteId = root.TryGetProperty("note_id", out var nid) ? nid.GetString() ?? "" : "";
        var archivedBy = root.TryGetProperty("archived_by", out var ab) ? ab.GetString() ?? "" : "";

        WeakReferenceMessenger.Default.Send(new NoteArchivedEvent(noteId, archivedBy));
    }

    private void StartHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new Timer(async _ =>
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    if (DateTime.UtcNow - _lastReceivedTime > TimeSpan.FromSeconds(90))
                    {
                        Log.Warning("[{Step}] No message received for 90s, reconnecting", "WS.Heartbeat");
                        await DisconnectAsync();
                        await ConnectAsync();
                        return;
                    }

                    var ping = Encoding.UTF8.GetBytes("{\"event\":\"ping\"}");
                    await _webSocket.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[{Step}]", "WS.Heartbeat");
                }
            }
        }, null, HeartbeatIntervalMs, HeartbeatIntervalMs);
    }

    private void ScheduleReconnect()
    {
        if (_intentionalStop) return;

        _reconnectTimer?.Dispose();
        _reconnectTimer = new Timer(async _ =>
        {
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
            await ConnectAsync();
        }, null, _reconnectDelay, Timeout.Infinite);

        _reconnectDelay = Math.Min(_reconnectDelay * 2, MaxReconnectDelay);
    }

    public void Dispose()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await DisconnectAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during WebSocket dispose");
            }
        });
        _cts?.Dispose();
        _heartbeatTimer?.Dispose();
        _reconnectTimer?.Dispose();
    }
}
