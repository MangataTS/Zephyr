using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Web.WebView2.Core;
using Serilog;
using ZephyrDesktop.Events;
using ZephyrDesktop.Services;

namespace ZephyrDesktop.Views;

public partial class WorkbenchWindow : Window
{
    private readonly WebViewEnvironmentService _envService;
    private const int ResizeBorderThickness = 6;

    public WorkbenchWindow()
    {
        _envService = App.GetService<WebViewEnvironmentService>()!;
        InitializeComponent();
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        HwndSource.FromHwnd(helper.Handle)?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        if (msg == WM_NCHITTEST)
        {
            var x = (short)(lParam & 0xFFFF);
            var y = (short)((lParam >> 16) & 0xFFFF);
            var pos = PointFromScreen(new Point(x, y));

            var ht = HitTest(pos);
            if (ht != 0)
            {
                handled = true;
                return ht;
            }
        }
        return 0;
    }

    private int HitTest(Point pos)
    {
        var x = pos.X;
        var y = pos.Y;
        var w = ActualWidth;
        var h = ActualHeight;
        var b = ResizeBorderThickness;

        var onLeft = x <= b;
        var onRight = x >= w - b;
        var onTop = y <= b;
        var onBottom = y >= h - b;

        if (onTop && onLeft) return 13;
        if (onTop && onRight) return 14;
        if (onBottom && onLeft) return 16;
        if (onBottom && onRight) return 17;
        if (onTop) return 12;
        if (onBottom) return 15;
        if (onLeft) return 10;
        if (onRight) return 11;

        return 0;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WorkbenchWebView.EnsureCoreWebView2Async(_envService.Environment);

            var config = App.GetService<Microsoft.Extensions.Configuration.IConfiguration>()!;
            var webFrontendUrl = config["WebFrontendUrl"] ?? "http://localhost:3001";
            var token = await App.GetService<TokenStorage>()!.GetAccessTokenAsync();

            WorkbenchWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WorkbenchWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            WorkbenchWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            WorkbenchWebView.CoreWebView2.Navigate($"{webFrontendUrl}/workbench/archive?token={token}&mode=desktop");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "Workbench.InitWebView");
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var msg = args.TryGetWebMessageAsString();
            using var doc = JsonDocument.Parse(msg);
            var type = doc.RootElement.GetProperty("type").GetString();

            switch (type)
            {
                case "restoreNote":
                    {
                        var noteId = doc.RootElement.GetProperty("noteId").GetString() ?? "";
                        var title = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                        var content = doc.RootElement.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                        var colorStatus = doc.RootElement.TryGetProperty("colorStatus", out var cs) ? cs.GetString() ?? "yellow" : "yellow";

                        Dispatcher.Invoke(() =>
                        {
                            WeakReferenceMessenger.Default.Send(new NoteRestoredEvent(noteId, title, content, colorStatus));
                        });
                        break;
                    }
                case "authExpired":
                    Dispatcher.Invoke(() =>
                    {
                        WeakReferenceMessenger.Default.Send(new ForceLogoutEvent());
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "Workbench.WebMessage");
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        WorkbenchWebView.Dispose();
    }
}
