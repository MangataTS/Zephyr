using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Views.Controls;

public partial class TagSelectorControl : UserControl
{
    private ObservableCollection<TagItemViewModel> _tagItems = [];
    private bool _isUpdating;
    private bool _popupJustClosed;
    private Window? _parentWindow;
    private bool _skipNextWindowPreview;

    public static readonly DependencyProperty AvailableTagsProperty =
        DependencyProperty.Register(nameof(AvailableTags), typeof(List<TagDto>), typeof(TagSelectorControl),
            new PropertyMetadata(null, OnAvailableTagsChanged));

    public static readonly DependencyProperty SelectedTagIdsProperty =
        DependencyProperty.Register(nameof(SelectedTagIds), typeof(List<string>), typeof(TagSelectorControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedTagIdsChanged));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(TagSelectorControl),
            new PropertyMetadata(false, OnIsDropDownOpenChanged));

    public List<TagDto> AvailableTags
    {
        get => (List<TagDto>)GetValue(AvailableTagsProperty);
        set => SetValue(AvailableTagsProperty, value);
    }

    public List<string> SelectedTagIds
    {
        get => (List<string>)GetValue(SelectedTagIdsProperty);
        set => SetValue(SelectedTagIdsProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public TagSelectorControl()
    {
        InitializeComponent();
        Loaded += (_, _) => _parentWindow = Window.GetWindow(this);
    }

    private static void OnAvailableTagsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TagSelectorControl)d;
        ctrl.RebuildTagItems();
    }

    private static void OnSelectedTagIdsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TagSelectorControl)d;
        if (ctrl._isUpdating) return;
        ctrl._isUpdating = true;
        try
        {
            ctrl.SyncCheckboxesFromSelection();
            ctrl.UpdateCapsules();
        }
        finally
        {
            ctrl._isUpdating = false;
        }
    }

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TagSelectorControl)d;
        ctrl.DropDownPopup.IsOpen = ctrl.IsDropDownOpen;
    }

    private void RebuildTagItems()
    {
        _tagItems.Clear();
        var tags = AvailableTags;
        var selectedIds = SelectedTagIds ?? [];

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                _tagItems.Add(new TagItemViewModel
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    Color = tag.Color,
                    IsSelected = selectedIds.Contains(tag.Id)
                });
            }
        }

        TagsList.ItemsSource = _tagItems;
        EmptyText.Visibility = _tagItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateCapsules();
    }

    private void SyncCheckboxesFromSelection()
    {
        var selectedIds = SelectedTagIds ?? [];
        foreach (var item in _tagItems)
        {
            item.IsSelected = selectedIds.Contains(item.Id);
        }
    }

    private void UpdateCapsules()
    {
        SelectedTagsPanel.Children.Clear();
        var selectedIds = SelectedTagIds ?? [];
        var tags = AvailableTags ?? [];

        foreach (var tag in tags.Where(t => selectedIds.Contains(t.Id)))
        {
            SelectedTagsPanel.Children.Add(CreateCapsule(tag));
        }

        PlaceholderText.Visibility = selectedIds.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border CreateCapsule(TagDto tag)
    {
        var dotBrush = TryParseColor(tag.Color, out var color)
            ? new SolidColorBrush(color)
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")!);

        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = dotBrush,
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var text = new TextBlock
        {
            Text = tag.Name,
            FontSize = 12,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(dot);
        panel.Children.Add(text);

        return new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")!),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 4, 0),
            Child = panel
        };
    }

    private static bool TryParseColor(string colorString, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(colorString);
            return true;
        }
        catch
        {
            color = Colors.Transparent;
            return false;
        }
    }

    private void TriggerBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_popupJustClosed) return;
        if (DropDownPopup.IsOpen)
        {
            DropDownPopup.IsOpen = false;
        }
        else
        {
            IsDropDownOpen = true;
            _skipNextWindowPreview = true;
            SubscribeToWindow();
        }
    }

    private void SubscribeToWindow()
    {
        if (_parentWindow == null) _parentWindow = Window.GetWindow(this);
        if (_parentWindow != null)
            _parentWindow.PreviewMouseLeftButtonDown += OnWindowPreviewMouseLeftButtonDown;
    }

    private void UnsubscribeFromWindow()
    {
        if (_parentWindow != null)
            _parentWindow.PreviewMouseLeftButtonDown -= OnWindowPreviewMouseLeftButtonDown;
    }

    private void OnWindowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_skipNextWindowPreview)
        {
            _skipNextWindowPreview = false;
            return;
        }

        if (!DropDownPopup.IsOpen) return;

        var pos = e.GetPosition(DropDownPopup.Child);
        var popupBounds = new Rect(0, 0, DropDownPopup.Child?.RenderSize.Width ?? 0, DropDownPopup.Child?.RenderSize.Height ?? 0);
        var triggerPos = e.GetPosition(TriggerBorder);
        var triggerBounds = new Rect(0, 0, TriggerBorder.RenderSize.Width, TriggerBorder.RenderSize.Height);

        if (!popupBounds.Contains(pos) && !triggerBounds.Contains(triggerPos))
        {
            DropDownPopup.IsOpen = false;
            IsDropDownOpen = false;
        }
    }

    private void TagCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        if (sender is CheckBox checkBox && checkBox.DataContext is TagItemViewModel item)
        {
            item.IsSelected = checkBox.IsChecked == true;
        }
        _isUpdating = true;
        try
        {
            var selectedIds = _tagItems.Where(i => i.IsSelected).Select(i => i.Id).ToList();
            SetCurrentValue(SelectedTagIdsProperty, selectedIds);
            UpdateCapsules();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void DropDownPopup_Closed(object? sender, EventArgs e)
    {
        _popupJustClosed = true;
        IsDropDownOpen = false;
        UnsubscribeFromWindow();
        Dispatcher.BeginInvoke(() => _popupJustClosed = false, System.Windows.Threading.DispatcherPriority.Input);
    }
}

internal class TagItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public Brush ColorBrush
    {
        get
        {
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(Color));
            }
            catch
            {
                return new SolidColorBrush(Colors.Gray);
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
