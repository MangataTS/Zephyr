using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using ZephyrDesktop.Data;
using ZephyrDesktop.Data.Repositories;
using ZephyrDesktop.Events;
using ZephyrDesktop.Services;
using ZephyrDesktop.Services.Api;
using ZephyrDesktop.ViewModels;
using ZephyrDesktop.Views;
using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;
using Serilog;

namespace ZephyrDesktop;

public partial class App : Application
{
    private ServiceProvider _serviceProvider = null!;
    private WindowManager _windowManager = null!;
    private TrayService _trayService = null!;
    private ToastService _toastService = null!;
    private WebSocketService _webSocketService = null!;
    private SyncEngine _syncEngine = null!;
    private LoginWindow? _loginWindow;

    private static readonly RefitSettings RefitSettings = new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        })
    };

    public static bool IsAutoLoginMode { get; private set; }

    public static T? GetService<T>() where T : class
    {
        return ((App)Current)._serviceProvider.GetService<T>();
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        IsAutoLoginMode = e.Args.Contains("--auto-login");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZephyrDesktop", "logs", "zephyr-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .WriteTo.Console()
            .CreateLogger();

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Log.Information("[{Step}] {Message}", "App", $"应用启动, auto-login={IsAutoLoginMode}");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Log.Error(ex!, "[{Step}]", "UnhandledException");
        };

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "[{Step}]", "DispatcherUnhandled");
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "[{Step}]", "UnobservedTask");
            args.SetObserved();
        };

        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            Log.Information("[{Step}] {Message}", "App", "配置已加载");

            var services = new ServiceCollection();
            ConfigureServices(services, config);
            _serviceProvider = services.BuildServiceProvider();

            Log.Information("[{Step}] {Message}", "App", "DI 容器已构建");

            _windowManager = _serviceProvider.GetRequiredService<WindowManager>();

            WeakReferenceMessenger.Default.Register<LoginSuccessEvent>(this, OnLoginSuccess);
            WeakReferenceMessenger.Default.Register<NoteCreatedEvent>(this, OnNoteCreated);
            WeakReferenceMessenger.Default.Register<NoteCompletedEvent>(this, OnNoteCompleted);
            WeakReferenceMessenger.Default.Register<NoteAssignedEvent>(this, OnNoteAssigned);
            WeakReferenceMessenger.Default.Register<NoteRemindedEvent>(this, OnNoteReminded);
            WeakReferenceMessenger.Default.Register<NoteArchivedEvent>(this, OnNoteArchived);
            WeakReferenceMessenger.Default.Register<NoteRestoredEvent>(this, OnNoteRestored);
            WeakReferenceMessenger.Default.Register<ForceLogoutEvent>(this, OnForceLogout);
            WeakReferenceMessenger.Default.Register<SyncTriggerEvent>(this, OnSyncTrigger);

            Log.Information("[{Step}] {Message}", "App", "事件已注册");

            InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "App.OnStartup");
        }
    }

    private async void InitializeAsync()
    {
        try
        {
            Log.Information("[{Step}] {Message}", "Init", "开始初始化...");

            var envService = _serviceProvider.GetRequiredService<WebViewEnvironmentService>();
            var noteRepo = _serviceProvider.GetRequiredService<LocalNoteRepository>();
            var tokenStorage = _serviceProvider.GetRequiredService<TokenStorage>();

            Log.Information("[{Step}] {Message}", "Init", "服务已获取，开始异步初始化");

            try
            {
                await noteRepo.InitializeAsync();
                Log.Information("[{Step}] {Message}", "Init", "SQLite 已初始化");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Step}]", "Init.SQLite");
            }

            try
            {
                await envService.InitializeAsync();
                Log.Information("[{Step}] {Message}", "Init", "WebView2 Environment 已创建");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Step}]", "Init.WebView2");
            }

            try
            {
                await envService.LoadEditorTemplateAsync();
                Log.Information("[{Step}] {Message}", "Init", "编辑器模板已加载");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Step}]", "Init.EditorTemplate");
            }

            if (await tokenStorage.IsTokenValidAsync())
            {
                Log.Information("[{Step}] {Message}", "Init", "Token 有效，直接进入主界面");
                ShowMainWindow();
            }
            else
            {
                Log.Information("[{Step}] {Message}", "Init", "Token 无效，显示登录窗口");
                ShowLoginWindow();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "Init");
            ShowLoginWindow();
        }
    }

    private void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(config);

        var apiBaseUrl = config["ApiBaseUrl"] ?? "http://localhost:8090";
        Log.Information("[{Step}] {Message}", "DI", $"API BaseUrl = {apiBaseUrl}");

        services.AddSingleton<TokenStorage>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<WebViewEnvironmentService>();
        services.AddSingleton<WindowManager>();
        services.AddSingleton<TrayService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<WebSocketService>();
        services.AddSingleton<SyncEngine>();
        services.AddSingleton<LocalNoteRepository>();
        services.AddSingleton(provider =>
        {
            var dbPath = provider.GetRequiredService<IConfiguration>()["DbPath"];
            return string.IsNullOrEmpty(dbPath) ? new AppDbContext() : new AppDbContext(dbPath);
        });
        services.AddSingleton<Func<AppDbContext>>(provider => () =>
        {
            var dbPath = provider.GetRequiredService<IConfiguration>()["DbPath"];
            return string.IsNullOrEmpty(dbPath) ? new AppDbContext() : new AppDbContext(dbPath);
        });

        services.AddTransient<LoginViewModel>();
        services.AddTransient<CreateNoteViewModel>();
        services.AddTransient<NoteOverviewViewModel>();

        services.AddSingleton<IAuthApi>(provider =>
        {
            Log.Information("[{Step}] {Message}", "DI", "创建 IAuthApi...");
            var config = provider.GetRequiredService<IConfiguration>();
            var baseUrl = config["ApiBaseUrl"] ?? "http://localhost:8090";
            var tokenStorage = provider.GetRequiredService<TokenStorage>();
            var handler = new ApiUnwrappingHandler(tokenStorage, null);
            handler.InnerHandler = new HttpClientHandler();
            var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
            var api = RestService.For<IAuthApi>(client, RefitSettings);
            Log.Information("[{Step}] {Message}", "DI", "IAuthApi 创建成功");
            return api;
        });

        services.AddSingleton<INoteApi>(provider =>
        {
            Log.Information("[{Step}] {Message}", "DI", "创建 INoteApi...");
            var client = CreateHttpClient(provider);
            var api = RestService.For<INoteApi>(client, RefitSettings);
            Log.Information("[{Step}] {Message}", "DI", "INoteApi 创建成功");
            return api;
        });

        services.AddSingleton<ITagApi>(provider =>
        {
            Log.Information("[{Step}] {Message}", "DI", "创建 ITagApi...");
            var client = CreateHttpClient(provider);
            var api = RestService.For<ITagApi>(client, RefitSettings);
            Log.Information("[{Step}] {Message}", "DI", "ITagApi 创建成功");
            return api;
        });

        services.AddSingleton<IUserApi>(provider =>
        {
            Log.Information("[{Step}] {Message}", "DI", "创建 IUserApi...");
            var client = CreateHttpClient(provider);
            var api = RestService.For<IUserApi>(client, RefitSettings);
            Log.Information("[{Step}] {Message}", "DI", "IUserApi 创建成功");
            return api;
        });

        services.AddSingleton<IWorkGroupApi>(provider =>
        {
            Log.Information("[{Step}] {Message}", "DI", "创建 IWorkGroupApi...");
            var client = CreateHttpClient(provider);
            var api = RestService.For<IWorkGroupApi>(client, RefitSettings);
            Log.Information("[{Step}] {Message}", "DI", "IWorkGroupApi 创建成功");
            return api;
        });
    }

    private HttpClient CreateHttpClient(IServiceProvider provider)
    {
        var config = provider.GetRequiredService<IConfiguration>();
        var baseUrl = config["ApiBaseUrl"] ?? "http://localhost:8090";
        var tokenStorage = provider.GetRequiredService<TokenStorage>();
        var authApi = provider.GetRequiredService<IAuthApi>();
        var handler = new ApiUnwrappingHandler(tokenStorage, () => authApi);
        handler.InnerHandler = new HttpClientHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        return client;
    }

    private void ShowLoginWindow()
    {
        Log.Information("[{Step}] {Message}", "UI", "ShowLoginWindow 被调用");
        _loginWindow?.Close();
        var vm = _serviceProvider.GetRequiredService<LoginViewModel>();
        _loginWindow = new LoginWindow(vm);
        _loginWindow.Show();
        Log.Information("[{Step}] {Message}", "UI", "LoginWindow 已显示");
    }

    private void ShowMainWindow()
    {
        Log.Information("[{Step}] {Message}", "UI", "ShowMainWindow 被调用");
        _loginWindow?.Close();
        _loginWindow = null;
        _windowManager.ShowFloatingButton();

        _trayService ??= _serviceProvider.GetRequiredService<TrayService>();
        _trayService.Initialize();

        _webSocketService ??= _serviceProvider.GetRequiredService<WebSocketService>();
        _ = _webSocketService.ConnectAsync();

        _syncEngine ??= _serviceProvider.GetRequiredService<SyncEngine>();
        _ = _syncEngine.StartupSyncAsync().ContinueWith(_ =>
        Dispatcher.Invoke(() => _trayService.RefreshStatus()),
        TaskScheduler.Default);

        Log.Information("[{Step}] {Message}", "UI", "FloatingButton + Tray + WebSocket + SyncEngine 已启动");
    }

    private void OnLoginSuccess(object recipient, LoginSuccessEvent message)
    {
        Log.Information("[{Step}] {Message}", "Event", $"OnLoginSuccess 被调用, User={message.LoginResponse.User?.Name}");
        try
        {
            Dispatcher.Invoke(ShowMainWindow);
            Log.Information("[{Step}] {Message}", "Event", "OnLoginSuccess 处理完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "Event.OnLoginSuccess");
        }
    }

    private void OnNoteCreated(object recipient, NoteCreatedEvent message)
    {
        Dispatcher.Invoke(() =>
        {
            _windowManager.CreateStickyNote(
                message.Note.Id,
                message.Note.Title,
                message.Note.Content,
                message.Note.ColorStatus);
        });
    }

    private void OnNoteCompleted(object recipient, NoteCompletedEvent message)
    {
        Dispatcher.Invoke(() => _windowManager.CloseStickyNote(message.NoteId));
    }

    private void OnNoteAssigned(object recipient, NoteAssignedEvent message)
    {
        Dispatcher.Invoke(() =>
        {
            _windowManager.CreateStickyNote(message.NoteId, message.Title, "", "yellow");
            _toastService ??= _serviceProvider.GetRequiredService<ToastService>();
            var noteId = message.NoteId;
            _toastService.ShowToast("新任务", string.IsNullOrEmpty(message.FromName) ? "有新任务指派给您" : $"{message.FromName} 指派了新任务", () =>
            {
                if (_windowManager.OpenNotes.TryGetValue(noteId, out var note))
                    note.Activate();
            });
        });
    }

    private void OnNoteReminded(object recipient, NoteRemindedEvent message)
    {
        Dispatcher.Invoke(() =>
        {
            if (_windowManager.OpenNotes.TryGetValue(message.NoteId, out var note))
            {
                if (note.ViewModel != null)
                    note.ViewModel.ColorStatus = message.ColorStatus;
                note.TriggerPulseAnimation();
            }
            _toastService ??= _serviceProvider.GetRequiredService<ToastService>();
            var noteId = message.NoteId;
            _toastService.ShowToast("盯办提醒", string.IsNullOrEmpty(message.ReminderName) ? "有任务被盯办" : $"{message.ReminderName} 盯办了你", () =>
            {
                if (_windowManager.OpenNotes.TryGetValue(noteId, out var note))
                    note.Activate();
            });
        });
    }

    private void OnNoteArchived(object recipient, NoteArchivedEvent message)
    {
        Dispatcher.Invoke(() => _windowManager.CloseStickyNote(message.NoteId));
    }

    private void OnNoteRestored(object recipient, NoteRestoredEvent message)
    {
        Dispatcher.Invoke(() =>
        {
            _windowManager.CreateStickyNote(message.NoteId, message.Title, message.Content, message.ColorStatus);
        });
    }

    private void OnForceLogout(object recipient, ForceLogoutEvent message)
    {
        Log.Information("[{Step}] {Message}", "Event", "OnForceLogout 被调用");
        try
        {
            Dispatcher.Invoke(() =>
            {
                _windowManager.CloseAllStickyNotes();
                _windowManager.HideFloatingButton();
                ShowLoginWindow();
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "Event.OnForceLogout");
        }
    }

    private void OnSyncTrigger(object recipient, SyncTriggerEvent message)
    {
        _syncEngine?.TriggerSync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _syncEngine?.Dispose();
        _webSocketService?.Dispose();
        _toastService?.Dispose();
        _trayService?.Dispose();
        Log.Information("[{Step}] {Message}", "App", $"应用退出, ExitCode={e.ApplicationExitCode}");
        _serviceProvider.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
