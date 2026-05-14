using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ZephyrDesktop.ViewModels;

namespace ZephyrDesktop.Views;

public partial class NoteOverviewWindow : Window
{
    private NoteOverviewViewModel _viewModel;

    public NoteOverviewWindow(NoteOverviewViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;

        NotesScrollViewer.ScrollChanged += (_, _) =>
        {
            InvalidateVisual();
        };
        Loaded += OnLoaded;

        SearchBox.TextChanged += (_, _) =>
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.SearchText))
            {
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(_viewModel.SearchText)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadNotesAsync();
        LoadBackgroundImage();
    }

    private void LoadBackgroundImage()
    {
        try
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "background_pure.png");
            if (!System.IO.File.Exists(path)) return;
            using var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            PureBackgroundImage.Source = decoder.Frames[0];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NoteOverviewWindow] LoadBackgroundImage failed: {ex.Message}");
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string tag)
        {
            _viewModel.FilterCategory = tag;
        }
    }

    private void FocusButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.DataContext is NoteOverviewItem item)
        {
            _viewModel.EditNoteCommand.Execute(item.Id);
        }
    }
}
