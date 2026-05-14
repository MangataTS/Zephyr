using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using Serilog;

namespace ZephyrDesktop.Services;

public sealed class ToastService : IDisposable
{
    private static readonly Dictionary<string, Action> _pendingActions = new();
    private static readonly object _lock = new();
    private bool _registered;

    public ToastService()
    {
        try
        {
            ToastNotificationManagerCompat.OnActivated += OnToastActivated;
            _registered = true;
            Log.Information("[{Step}] {Message}", "Toast", "Toast notification support registered");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}] {Message}", "Toast", "Failed to register toast notification support");
        }
    }

    public void ShowToast(string title, string body, Action? onClick = null)
    {
        if (!_registered) return;
        try
        {
            var toastId = Guid.NewGuid().ToString();
            if (onClick != null)
            {
                lock (_lock) { _pendingActions[toastId] = onClick; }
            }

            new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .Show(toast =>
                {
                    toast.Tag = toastId;
                    toast.Dismissed += (s, e) =>
                    {
                        lock (_lock) { _pendingActions.Remove(toastId); }
                    };
                    toast.Failed += (s, e) =>
                    {
                        Log.Error("[{Step}] {Message}", "Toast", $"Toast failed: {e.ErrorCode}");
                        lock (_lock) { _pendingActions.Remove(toastId); }
                    };
                });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}] {Message}", "Toast", "Failed to show toast notification");
        }
    }

    private static void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = e.Argument;
        if (!string.IsNullOrEmpty(args))
        {
            lock (_lock)
            {
                if (_pendingActions.TryGetValue(args, out var action))
                {
                    _pendingActions.Remove(args);
                    Application.Current?.Dispatcher.BeginInvoke(action);
                }
            }
        }
    }

    public void Dispose()
    {
        ToastNotificationManagerCompat.OnActivated -= OnToastActivated;
    }
}
