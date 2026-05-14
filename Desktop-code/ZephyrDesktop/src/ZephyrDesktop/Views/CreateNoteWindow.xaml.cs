using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Serilog;
using ZephyrDesktop.Services;
using ZephyrDesktop.ViewModels;

namespace ZephyrDesktop.Views;

public partial class CreateNoteWindow : Window
{
    private readonly WebViewEnvironmentService _envService;
    private readonly CreateNoteViewModel _viewModel;

    private const string TitlePlaceholder = "任务名称";
    private const string SubTagPlaceholder = "二级标签（可选）";
    private static readonly SolidColorBrush PlaceholderBrush = new(Colors.Gray);
    private static readonly SolidColorBrush NormalBrush = new(Colors.Black);

    private bool _titleIsPlaceholder = true;
    private bool _subTagIsPlaceholder = true;

    public CreateNoteWindow()
    {
        _envService = App.GetService<WebViewEnvironmentService>()!;
        _viewModel = App.GetService<CreateNoteViewModel>()!;
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;

        SetPlaceholder(TitleBox, TitlePlaceholder, ref _titleIsPlaceholder);
        SetPlaceholder(SubTagBox, SubTagPlaceholder, ref _subTagIsPlaceholder);
    }

    private static void SetPlaceholder(TextBox box, string text, ref bool isPlaceholder)
    {
        box.Text = text;
        box.Foreground = PlaceholderBrush;
        isPlaceholder = true;
    }

    private static void RemovePlaceholder(TextBox box, ref bool isPlaceholder)
    {
        if (isPlaceholder)
        {
            box.Text = "";
            box.Foreground = NormalBrush;
            isPlaceholder = false;
        }
    }

    private static void RestorePlaceholderIfNeeded(TextBox box, string placeholder, ref bool isPlaceholder)
    {
        if (string.IsNullOrEmpty(box.Text) || (isPlaceholder && box.Text == placeholder))
        {
            box.Text = placeholder;
            box.Foreground = PlaceholderBrush;
            isPlaceholder = true;
        }
        else
        {
            isPlaceholder = false;
        }
    }

    private void TitleBox_GotFocus(object sender, RoutedEventArgs e)
    {
        RemovePlaceholder(TitleBox, ref _titleIsPlaceholder);
    }

    private void TitleBox_LostFocus(object sender, RoutedEventArgs e)
    {
        RestorePlaceholderIfNeeded(TitleBox, TitlePlaceholder, ref _titleIsPlaceholder);
    }

    private void SubTagBox_GotFocus(object sender, RoutedEventArgs e)
    {
        RemovePlaceholder(SubTagBox, ref _subTagIsPlaceholder);
    }

    private void SubTagBox_LostFocus(object sender, RoutedEventArgs e)
    {
        RestorePlaceholderIfNeeded(SubTagBox, SubTagPlaceholder, ref _subTagIsPlaceholder);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.InitializeAsync();
        LoadBackgroundImage();

        try
        {
            await EditorWebView.EnsureCoreWebView2Async(_envService.Environment);
            EditorWebView.CoreWebView2.NavigateToString(_envService.EditorTemplateHtml);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "CreateNote.InitWebView");
        }
    }

    private void LoadBackgroundImage()
    {
        try
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "background_tec.png");
            if (!System.IO.File.Exists(path)) return;
            using var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            TecBackgroundImage.Source = decoder.Frames[0];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateNoteWindow] LoadBackgroundImage failed: {ex.Message}");
        }
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_titleIsPlaceholder) TitleBox.Text = "";
        if (_subTagIsPlaceholder) SubTagBox.Text = "";

        if (EditorWebView?.CoreWebView2 == null)
        {
            _viewModel.Content = "";
            await _viewModel.CreateCommand.ExecuteAsync(null);
            return;
        }

        try
        {
            var result = await EditorWebView.CoreWebView2.ExecuteScriptAsync("getContent()");
            var content = System.Text.Json.JsonSerializer.Deserialize<string>(result) ?? "";
            _viewModel.Content = content;
        }
        catch
        {
            _viewModel.Content = "";
        }

        await _viewModel.CreateCommand.ExecuteAsync(null);

        if (string.IsNullOrEmpty(_viewModel.ErrorMessage))
        {
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        EditorWebView.Dispose();
    }
}
