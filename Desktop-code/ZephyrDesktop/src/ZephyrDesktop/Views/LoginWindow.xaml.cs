using System.Windows;
using System.Windows.Input;
using Serilog;
using ZephyrDesktop.ViewModels;

namespace ZephyrDesktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Log.Information("[{Step}] {Message}", "LoginWindow", "窗口已创建并绑定 DataContext");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Keyboard.Focus(UsernameBox);
        Log.Information("[{Step}] {Message}", "LoginWindow", "窗口已加载，焦点已设置");

        if (App.IsAutoLoginMode)
        {
            Log.Information("[{Step}] {Message}", "LoginWindow", "检测到 --auto-login 模式，3秒后自动登录");
            _ = AutoLoginAsync();
        }
    }

    private async Task AutoLoginAsync()
    {
        await Task.Delay(3000);

        Log.Information("[{Step}] {Message}", "AutoLogin", "开始自动登录...");

        if (DataContext is LoginViewModel vm)
        {
            vm.Username = "admin";
            PasswordBox.Password = "Admin@123";
            vm.Password = "Admin@123";

            Log.Information("[{Step}] {Message}", "AutoLogin", $"用户名={vm.Username}, 密码已设置");

            try
            {
                Log.Information("[{Step}] {Message}", "AutoLogin", "调用 LoginCommand...");
                await vm.LoginCommand.ExecuteAsync(null);
                Log.Information("[{Step}] {Message}", "AutoLogin", $"LoginCommand 完成. HasError={vm.HasError}, ErrorMessage={vm.ErrorMessage}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Step}]", "AutoLogin");
            }
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.Password = PasswordBox.Password;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        DragMove();
    }
}
