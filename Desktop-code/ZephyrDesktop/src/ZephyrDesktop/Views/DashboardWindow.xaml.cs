using System.Windows;
using System.Windows.Input;
using Serilog;
using ZephyrDesktop.Services;

namespace ZephyrDesktop.Views;

public partial class DashboardWindow : Window
{
    private readonly string _baseUrl;
    private readonly string _jwtToken;
    private readonly string _groupId;
    private readonly WebViewEnvironmentService _envService;
    private bool _webViewInitialized;

    public DashboardWindow(string baseUrl, string jwtToken, string groupId)
    {
        _baseUrl = baseUrl;
        _jwtToken = jwtToken;
        _groupId = groupId;
        _envService = App.GetService<WebViewEnvironmentService>()!;

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_webViewInitialized) return;

        try
        {
            await DashboardWebView.EnsureCoreWebView2Async(_envService.Environment);

            DashboardWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            DashboardWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            var url = $"{_baseUrl}/workbench/groups/{_groupId}/dashboard?token={_jwtToken}&mode=desktop";
            DashboardWebView.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "Dashboard.InitWebView");
        }

        _webViewInitialized = true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_webViewInitialized)
        {
            DashboardWebView.Dispose();
        }
    }
}