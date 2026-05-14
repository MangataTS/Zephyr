using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using ZephyrDesktop.Data.Repositories;
using Serilog;
using ZephyrDesktop.Events;
using ZephyrDesktop.Services;
using ZephyrDesktop.ViewModels;
using ZephyrDesktop.Views.Controls;
using CommunityToolkit.Mvvm.Messaging;

namespace ZephyrDesktop.Views;

public partial class StickyNoteWindow : Window
{
    private string _noteId;
    private readonly string _initialContent;
    private readonly WebViewEnvironmentService _envService;
    private readonly LocalNoteRepository _noteRepo;
    private StickyNoteViewModel? _viewModel;
    private bool _isFolded;
    private bool _isTopmost = true;
    private bool _webViewInitialized;
    private DispatcherTimer? _positionSaveTimer;
    private DispatcherTimer? _remainingTimeTimer;
    private bool _isFullscreen;
    private double _savedLeft, _savedTop, _savedWidth, _savedHeight;
    private string _titleBeforeEdit = "";

    private static readonly Dictionary<string, SolidColorBrush> ColorMap = new()
    {
        ["yellow"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")!),
        ["red"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2")!),
        ["green"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7")!)
    };

    private static readonly Dictionary<string, (string bgLight, string bg)> EditorColorMap = new()
    {
        ["yellow"] = ("#FFF9E5", "#FEF3C7"),
        ["red"] = ("#FFF0F0", "#FEE2E2"),
        ["green"] = ("#F0FFF4", "#DCFCE7")
    };

    public bool IsFolded => _isFolded;
    public bool IsTopmost => _isTopmost;

    public StickyNoteWindow(string noteId, string title, string content, string colorStatus, bool isTopmost = true)
    {
        _noteId = noteId;
        _initialContent = content;
        _envService = App.GetService<WebViewEnvironmentService>()!;
        _noteRepo = App.GetService<LocalNoteRepository>()!;
        _isTopmost = isTopmost;

        InitializeComponent();
        TitleText.Text = title;
        UpdateColor(colorStatus);
        Topmost = _isTopmost;
        UpdatePinButton();

        SubTagBox.TextChanged += (_, _) =>
        {
            SubTagPlaceholder.Visibility = string.IsNullOrEmpty(SubTagBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        };
        SubTagPlaceholder.Visibility = string.IsNullOrEmpty(SubTagBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        Loaded += OnLoaded;
        LocationChanged += OnLocationChanged;
        SizeChanged += OnSizeChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        if (_webViewInitialized) return;

        try
        {
            LoadingText.Visibility = Visibility.Visible;
            await ContentWebView.EnsureCoreWebView2Async(_envService.Environment);
            ContentWebView.CoreWebView2.NavigateToString(_envService.EditorTemplateHtml);

            ContentWebView.CoreWebView2.NavigationCompleted += async (_, _) =>
            {
                var contentToSet = _viewModel?.Content ?? _initialContent;
                if (!string.IsNullOrEmpty(contentToSet))
                {
                    var escaped = JsonSerializer.Serialize(contentToSet);
                    await ContentWebView.CoreWebView2.ExecuteScriptAsync($"setContent({escaped})");
                }

                var currentColor = _viewModel?.ColorStatus ?? "yellow";
                if (EditorColorMap.TryGetValue(currentColor, out var colors))
                {
                    await ContentWebView.CoreWebView2.ExecuteScriptAsync(
                        $"setNoteColors('{colors.bgLight}', '{colors.bg}')");
                }

                LoadingText.Visibility = Visibility.Collapsed;

                if (_pendingFadeIn)
                    PlayFadeInAnimation();
            };

            ContentWebView.CoreWebView2.WebMessageReceived += (s, args) =>
            {
                try
                {
                    var msg = args.TryGetWebMessageAsString();
                    using var doc = JsonDocument.Parse(msg);
                    var type = doc.RootElement.GetProperty("type").GetString();

                    if (type == "blur" || type == "input")
                    {
                        if (doc.RootElement.TryGetProperty("html", out var htmlEl))
                        {
                            var html = htmlEl.GetString() ?? "";
                            Dispatcher.Invoke(() =>
                            {
                                if (_viewModel != null)
                                {
                                    _viewModel.Content = html;
                                    _viewModel.MarkDirty();
                                }
                            });
                        }
                    }
                }
                catch
                {
                    Dispatcher.Invoke(() => _viewModel?.MarkDirty());
                }
            };

            _webViewInitialized = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "StickyNote.InitWebView");
        }
    }

    public void SetViewModel(StickyNoteViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(StickyNoteViewModel.ColorStatus))
            {
                Dispatcher.Invoke(() => UpdateColor(_viewModel.ColorStatus));
            }
            else if (e.PropertyName == nameof(StickyNoteViewModel.DueTime) ||
                     e.PropertyName == nameof(StickyNoteViewModel.RemindCount) ||
                     e.PropertyName == nameof(StickyNoteViewModel.TemplateType))
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateTaskTypeDisplay();
                    UpdateSupervisionState(_viewModel.IsSupervisionTask);
                });
            }
        };

        _remainingTimeTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _remainingTimeTimer.Tick += (_, _) => UpdateRemainingTime();
        _remainingTimeTimer.Start();
        UpdateTaskTypeDisplay();

        if (_viewModel.IsSupervisionTask)
        {
            Topmost = true;
            _isTopmost = true;
            UpdatePinButton();
            PinButton.IsEnabled = false;
            HideButton.IsEnabled = false;
            HideButton.Opacity = 0.3;
        }
    }

    public StickyNoteViewModel? ViewModel => _viewModel;
    public string NoteId => _noteId;

    public void UpdateNoteId(string newId)
    {
        _noteId = newId;
    }

    public async Task UpdateWebViewContentAsync(string content)
    {
        if (!_webViewInitialized || ContentWebView?.CoreWebView2 == null) return;

        try
        {
            var escaped = JsonSerializer.Serialize(content);
            await ContentWebView.CoreWebView2.ExecuteScriptAsync($"setContent({escaped})");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "StickyNote.UpdateWebViewContent");
        }
    }

    public void Fold()
    {
        if (_isFolded) return;
        _isFolded = true;

        TaskTypeBar.Visibility = Visibility.Collapsed;
        ContentArea.Visibility = Visibility.Collapsed;
        ActionBar.Visibility = Visibility.Collapsed;
        TagSelector.Visibility = Visibility.Collapsed;
        TagSelector.IsDropDownOpen = false;
        SubTagBox.Visibility = Visibility.Collapsed;
        FoldButton.Content = "+";
        FoldButton.ToolTip = "展开";

        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;

        _ = SavePositionAsync();
    }

    public async void Expand()
    {
        if (!_isFolded) return;

        TaskTypeBar.Visibility = Visibility.Visible;
        ContentArea.Visibility = Visibility.Visible;
        ActionBar.Visibility = Visibility.Visible;
        TagSelector.Visibility = Visibility.Visible;
        SubTagBox.Visibility = Visibility.Visible;
        FoldButton.Content = "−";
        FoldButton.ToolTip = "折叠";

        ContentArea.Opacity = 0;
        ActionBar.Opacity = 0;
        TagSelector.Opacity = 0;

        SizeToContent = SizeToContent.Manual;
        Height = _viewModel != null ? 320 : 320;
        ResizeMode = ResizeMode.CanResizeWithGrip;

        _isFolded = false;

        if (!_webViewInitialized)
        {
            LoadingText.Visibility = Visibility.Visible;
            _pendingFadeIn = true;
            await InitializeWebViewAsync();
        }
        else
        {
            PlayFadeInAnimation();
        }

        _ = SavePositionAsync();
    }

    private bool _pendingFadeIn;

    private void PlayFadeInAnimation()
    {
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        ContentArea.BeginAnimation(OpacityProperty, fadeIn);
        ActionBar.BeginAnimation(OpacityProperty, fadeIn);
        TagSelector.BeginAnimation(OpacityProperty, fadeIn);
        _pendingFadeIn = false;
    }

    public void TriggerPulseAnimation()
    {
        var animation = new Storyboard();

        var shadow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Red,
            ShadowDepth = 0,
            BlurRadius = 20,
            Opacity = 0.8
        };

        var opacityAnimation = new DoubleAnimation
        {
            From = 0.8,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(500),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3)
        };

        Storyboard.SetTarget(opacityAnimation, NoteBorder);
        Storyboard.SetTargetProperty(opacityAnimation,
            new PropertyPath("(FrameworkElement.Effect).(DropShadowEffect.Opacity)"));

        NoteBorder.Effect = shadow;
        animation.Children.Add(opacityAnimation);
        animation.Completed += (_, _) => NoteBorder.Effect = null;
        animation.Begin(NoteBorder);
    }

    public void TriggerCompleteAnimation()
    {
        var animation = new Storyboard();

        var opacityAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(600),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(opacityAnimation, NoteBorder);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));

        animation.Children.Add(opacityAnimation);
        animation.Completed += (_, _) => { };
        animation.Begin(NoteBorder);
    }

    private void UpdateColor(string colorStatus)
    {
        if (ColorMap.TryGetValue(colorStatus, out var brush))
        {
            NoteBorder.Background = brush;
        }

        if (_webViewInitialized && EditorColorMap.TryGetValue(colorStatus, out var colors))
        {
            _ = ContentWebView.CoreWebView2.ExecuteScriptAsync(
                $"setNoteColors('{colors.bgLight}', '{colors.bg}')");
        }
    }

    private void NoteBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        var isOverInteractive = IsDescendantOf<Button>(source)
                              || IsDescendantOf<TextBox>(source)
                              || IsDescendantOf<Selector>(source)
                              || IsDescendantOf<UserControl>(source);

        if (isOverInteractive) return;

        if (TagSelector.IsDropDownOpen)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        DragMove();
    }

    private static bool IsDescendantOf<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source != null)
        {
            if (source is T) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void TitleText_GotFocus(object sender, RoutedEventArgs e)
    {
        if (TitleText.IsReadOnly)
        {
            TitleText.IsReadOnly = false;
            _titleBeforeEdit = TitleText.Text;
        }
    }

    private void TitleText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TitleText.IsReadOnly)
        {
            CommitTitleEdit();
        }
    }

    private void TitleText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitTitleEdit();
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(TitleText), null);
            Keyboard.ClearFocus();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            TitleText.Text = _titleBeforeEdit;
            TitleText.IsReadOnly = true;
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(TitleText), null);
            Keyboard.ClearFocus();
        }
    }

    private void CommitTitleEdit()
    {
        TitleText.IsReadOnly = true;
        if (TitleText.Text != _titleBeforeEdit && _viewModel != null)
        {
            _viewModel.Title = TitleText.Text;
            _viewModel.MarkDirty();
        }
    }

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            Left = _savedLeft;
            Top = _savedTop;
            Width = _savedWidth;
            Height = _savedHeight;
            _isFullscreen = false;
            FoldButton.ToolTip = "折叠";
        }
        else
        {
            _savedLeft = Left;
            _savedTop = Top;
            _savedWidth = Width;
            _savedHeight = Height;
            Left = SystemParameters.WorkArea.Left;
            Top = SystemParameters.WorkArea.Top;
            Width = SystemParameters.WorkArea.Width;
            Height = SystemParameters.WorkArea.Height;
            _isFullscreen = true;
            FoldButton.ToolTip = "还原";
        }
    }

    private void SubTagBox_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveSubTag();
    }

    private void SubTagBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SaveSubTag();
            Keyboard.ClearFocus();
        }
    }

    private void SaveSubTag()
    {
        if (_viewModel != null)
        {
            _ = _noteRepo.UpdateSubTagAsync(_noteId, SubTagBox.Text);
        }
    }

    private void UpdateTaskTypeDisplay()
    {
        if (_viewModel == null) return;

        var label = _viewModel.TaskTypeLabel;
        if (!string.IsNullOrEmpty(label))
        {
            TaskTypeLabel.Visibility = Visibility.Visible;
            TaskTypeText.Text = $"【{label}】";
            if (_viewModel.IsGroupTask)
            {
                TaskTypeLabel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")!);
                TaskTypeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D4ED8")!);
            }
            else if (_viewModel.IsSupervisionTask)
            {
                TaskTypeLabel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2")!);
                TaskTypeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")!);
            }
        }
        else
        {
            TaskTypeLabel.Visibility = Visibility.Collapsed;
        }

        UpdateRemainingTime();
    }

    private void UpdateRemainingTime()
    {
        if (_viewModel == null || !_viewModel.IsGroupTask && !_viewModel.IsSupervisionTask || _viewModel.DueTime == null)
        {
            RemainingTimeText.Visibility = Visibility.Collapsed;
            return;
        }

        var due = _viewModel.DueTime.Value;
        var now = DateTime.UtcNow;
        var diff = due - now;

        if (diff.TotalSeconds > 0)
        {
            var days = (int)diff.TotalDays;
            var hours = diff.Hours;
            RemainingTimeText.Text = $"距期剩余{days}天";
            RemainingTimeText.Foreground = new SolidColorBrush(Colors.Gray);
        }
        else
        {
            var overdue = now - due;
            var days = (int)overdue.TotalDays;
            var hours = overdue.Hours;
            RemainingTimeText.Text = days > 0 ? $"已超期{days}天{hours}时" : $"已超期{hours}时{overdue.Minutes}分";
            RemainingTimeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")!);
        }

        RemainingTimeText.Visibility = Visibility.Visible;
    }

    public void UpdateSupervisionState(bool isSupervision)
    {
        if (isSupervision)
        {
            Topmost = true;
            _isTopmost = true;
            UpdatePinButton();
            PinButton.IsEnabled = false;
        }
        else
        {
            PinButton.IsEnabled = true;
        }

        HideButton.IsEnabled = !isSupervision;
        HideButton.Opacity = isSupervision ? 0.3 : 1.0;
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        var wm = App.GetService<WindowManager>();
        wm?.HideNote(_noteId);
    }

    private void FoldButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isFolded)
            Expand();
        else
            Fold();
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _isTopmost = !_isTopmost;
        Topmost = _isTopmost;
        UpdatePinButton();
        _ = _noteRepo.UpdateTopmostAsync(_noteId, _isTopmost);
    }

    private void UpdatePinButton()
    {
        PinButton.Content = _isTopmost ? "📌" : "📍";
        PinButton.ToolTip = _isTopmost ? "取消置顶" : "置顶";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("是否归档此任务？", "归档确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _viewModel?.CompleteCommand.Execute(null);
        }
    }

    private void MenuArchive_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("是否归档此任务？", "归档确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _viewModel?.CompleteCommand.Execute(null);
        }
    }

    private async void CompleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        await SyncContentFromWebViewAsync();

        try
        {
            await _viewModel.CompleteCommand.ExecuteAsync(null);
            TriggerCompleteAnimation();
            await Task.Delay(600);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "StickyNote.Complete");
        }
    }

    private async Task SyncContentFromWebViewAsync()
    {
        if (ContentWebView?.CoreWebView2 == null || _viewModel == null) return;

        try
        {
            var result = await ContentWebView.CoreWebView2.ExecuteScriptAsync("getContent()");
            var content = JsonSerializer.Deserialize<string>(result) ?? "";
            _viewModel.Content = content;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "StickyNote.SyncContent");
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        DebounceSavePosition();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DebounceSavePosition();
    }

    private void DebounceSavePosition()
    {
        _positionSaveTimer?.Stop();
        _positionSaveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _positionSaveTimer.Tick -= OnPositionSaveTimerTick;
        _positionSaveTimer.Tick += OnPositionSaveTimerTick;
        _positionSaveTimer.Start();
    }

    private void OnPositionSaveTimerTick(object? sender, EventArgs e)
    {
        _positionSaveTimer?.Stop();
        _ = SavePositionAsync();
    }

    private async Task SavePositionAsync()
    {
        try
        {
            await _noteRepo.UpdatePositionAsync(_noteId, Left, Top, Width, Height);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "StickyNote.SavePosition");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_webViewInitialized)
            ContentWebView.Dispose();
        _remainingTimeTimer?.Stop();
    }
}
