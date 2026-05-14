using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Serilog;
using ZephyrDesktop.Services;

namespace ZephyrDesktop.Views;

public partial class WorkGroupWindow : Window
{
    private readonly string _baseUrl;
    private readonly string _jwtToken;
    private readonly string _initialRoute;
    private readonly WebViewEnvironmentService _envService;
    private bool _webViewInitialized;

    private static readonly Regex DashboardRouteRegex = new(
        @"workbench/groups/(.+)/dashboard",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public WorkGroupWindow(string baseUrl, string jwtToken, string initialRoute = "/workbench?mode=desktop&view=groups&hide_create=1&hide_sidebar=1")
    {
        _baseUrl = baseUrl;
        _jwtToken = jwtToken;
        _initialRoute = initialRoute;
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
            await WorkGroupWebView.EnsureCoreWebView2Async(_envService.Environment);

            WorkGroupWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WorkGroupWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            WorkGroupWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            WorkGroupWebView.NavigationStarting += OnNavigationStarting;

            NavigateTo(_initialRoute);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "WorkGroup.InitWebView");
        }

        _webViewInitialized = true;
    }

    public void NavigateTo(string route)
    {
        if (WorkGroupWebView?.CoreWebView2 == null) return;

        var url = $"{_baseUrl}{route}&token={_jwtToken}";
        WorkGroupWebView.CoreWebView2.Navigate(url);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var match = DashboardRouteRegex.Match(e.Uri);
        if (match.Success)
        {
            e.Cancel = true;

            var groupId = match.Groups[1].Value;
            Dispatcher.Invoke(() =>
            {
                var dashboard = new DashboardWindow(_baseUrl, _jwtToken, groupId);
                dashboard.Show();
            });
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess && WorkGroupWebView?.CoreWebView2 != null)
        {
            Dispatcher.Invoke(() =>
            {
                var title = WorkGroupWebView.CoreWebView2.DocumentTitle;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    TitleText.Text = title;
                }
            });
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (WorkGroupWebView?.CoreWebView2?.CanGoBack == true)
        {
            WorkGroupWebView.CoreWebView2.GoBack();
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (WorkGroupWebView?.CoreWebView2?.CanGoForward == true)
        {
            WorkGroupWebView.CoreWebView2.GoForward();
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        WorkGroupWebView?.CoreWebView2?.Reload();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
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
        if (_webViewInitialized)
        {
            WorkGroupWebView.Dispose();
        }
    }
}