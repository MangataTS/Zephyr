using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using ZephyrDesktop.Events;
using ZephyrDesktop.Models;
using ZephyrDesktop.Services;

namespace ZephyrDesktop.ViewModels;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        Log.Information("[{Step}] {Message}", "VM", "LoginAsync 被调用");

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入用户名和密码";
            HasError = true;
            Log.Information("[{Step}] {Message}", "VM", "验证失败：用户名或密码为空");
            return;
        }

        IsLoading = true;
        HasError = false;
        ErrorMessage = "";

        try
        {
            Log.Information("[{Step}] {Message}", "VM", $"调用 AuthService.LoginAsync, username={Username}");
            var loginResponse = await _authService.LoginAsync(Username, Password);
            Log.Information("[{Step}] {Message}", "VM", $"LoginAsync 返回成功, User={loginResponse.User?.Name}, Token长度={loginResponse.AccessToken?.Length}");

            Log.Information("[{Step}] {Message}", "VM", "发送 LoginSuccessEvent...");
            WeakReferenceMessenger.Default.Send(new LoginSuccessEvent(loginResponse));
            Log.Information("[{Step}] {Message}", "VM", "LoginSuccessEvent 已发送");
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "登录请求已取消";
            HasError = true;
            Log.Information("[{Step}] {Message}", "VM", "登录被取消");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
            Log.Error(ex, "[{Step}]", "VM");
        }
        finally
        {
            IsLoading = false;
            Log.Information("[{Step}] {Message}", "VM", "LoginAsync 完成 (finally)");
        }
    }
}
