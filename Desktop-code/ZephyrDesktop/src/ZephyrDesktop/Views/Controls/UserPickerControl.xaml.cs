using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Views.Controls;

public partial class UserPickerControl : UserControl
{
    private ObservableCollection<UserItemViewModel> _userItems = [];
    private bool _isUpdating;
    private bool _popupJustClosed;
    private Window? _parentWindow;
    private bool _skipNextWindowPreview;

    public static readonly DependencyProperty UsersProperty =
        DependencyProperty.Register(nameof(Users), typeof(List<UserDto>), typeof(UserPickerControl),
            new PropertyMetadata(null, OnUsersChanged));

    public static readonly DependencyProperty SelectedUsersProperty =
        DependencyProperty.Register(nameof(SelectedUsers), typeof(List<UserDto>), typeof(UserPickerControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedUsersChanged));

    public List<UserDto> Users
    {
        get => (List<UserDto>)GetValue(UsersProperty);
        set => SetValue(UsersProperty, value);
    }

    public List<UserDto> SelectedUsers
    {
        get => (List<UserDto>)GetValue(SelectedUsersProperty);
        set => SetValue(SelectedUsersProperty, value);
    }

    public UserPickerControl()
    {
        InitializeComponent();
        Loaded += (_, _) => _parentWindow = Window.GetWindow(this);
    }

    private static void OnUsersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (UserPickerControl)d;
        ctrl.RebuildUserItems();
    }

    private static void OnSelectedUsersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (UserPickerControl)d;
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

    private void RebuildUserItems()
    {
        _userItems.Clear();
        var users = Users ?? [];
        var selectedIds = SelectedUsers?.Select(u => u.Id).ToList() ?? [];

        foreach (var user in users)
        {
            _userItems.Add(new UserItemViewModel
            {
                Id = user.Id,
                Name = user.Name,
                Username = user.Username,
                IsSelected = selectedIds.Contains(user.Id)
            });
        }

        UsersList.ItemsSource = _userItems;
        EmptyText.Visibility = _userItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateCapsules();
    }

    private void SyncCheckboxesFromSelection()
    {
        var selectedIds = SelectedUsers?.Select(u => u.Id).ToList() ?? [];
        foreach (var item in _userItems)
        {
            item.IsSelected = selectedIds.Contains(item.Id);
        }
    }

    private void UpdateCapsules()
    {
        SelectedUsersPanel.Children.Clear();
        var selected = SelectedUsers ?? [];

        foreach (var user in selected)
        {
            var text = new TextBlock
            {
                Text = user.Name,
                FontSize = 12,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")!),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 4, 0),
                Child = text
            };

            SelectedUsersPanel.Children.Add(border);
        }

        PlaceholderText.Visibility = selected.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FilterUsers(string keyword)
    {
        var users = Users ?? [];
        var filtered = string.IsNullOrWhiteSpace(keyword)
            ? users
            : users.Where(u =>
                u.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                u.Username.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        _userItems.Clear();
        var selectedIds = SelectedUsers?.Select(u => u.Id).ToList() ?? [];
        foreach (var user in filtered)
        {
            _userItems.Add(new UserItemViewModel
            {
                Id = user.Id,
                Name = user.Name,
                Username = user.Username,
                IsSelected = selectedIds.Contains(user.Id)
            });
        }

        EmptyText.Visibility = _userItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
            DropDownPopup.IsOpen = true;
            SearchBox.Text = "";
            FilterUsers("");
            _skipNextWindowPreview = true;
            SubscribeToWindow();
            Dispatcher.BeginInvoke(() => SearchBox.Focus(), System.Windows.Threading.DispatcherPriority.Input);
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
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterUsers(SearchBox.Text);
    }

    private void UserCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        if (sender is CheckBox checkBox && checkBox.DataContext is UserItemViewModel item)
        {
            item.IsSelected = checkBox.IsChecked == true;
        }
        _isUpdating = true;
        try
        {
            var selectedUsers = _userItems
                .Where(i => i.IsSelected)
                .Select(i => new UserDto { Id = i.Id, Name = i.Name, Username = i.Username })
                .ToList();
            SetCurrentValue(SelectedUsersProperty, selectedUsers);
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
        UnsubscribeFromWindow();
        Dispatcher.BeginInvoke(() => _popupJustClosed = false, System.Windows.Threading.DispatcherPriority.Input);
    }
}

internal class UserItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";

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

    public event PropertyChangedEventHandler? PropertyChanged;
}
