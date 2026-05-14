using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZephyrDesktop.Models;
using ZephyrDesktop.Services.Api;

namespace ZephyrDesktop.Views;

public partial class CreateWorkGroupWindow : Window
{
    private readonly IWorkGroupApi _workGroupApi;
    private readonly IUserApi _userApi;

    private List<UserDto> _availableUsers = [];
    private readonly List<SubGroupRow> _subGroupRows = [];
    private int _subGroupCounter;

    public event Action<string>? OnCreated;

    public CreateWorkGroupWindow()
    {
        _workGroupApi = App.GetService<IWorkGroupApi>()!;
        _userApi = App.GetService<IUserApi>()!;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
        AddSubGroup("组长组", isFirst: true);
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var usersResponse = await _userApi.GetVisibleUsersAsync();
            if (usersResponse.IsSuccessStatusCode && usersResponse.Content != null)
            {
                _availableUsers = usersResponse.Content;
            }
        }
        catch { }
    }

    private void AddSubGroup(string defaultName = "", bool isFirst = false)
    {
        _subGroupCounter++;

        var nameBox = new TextBox
        {
            Text = string.IsNullOrEmpty(defaultName) ? $"小组 {_subGroupCounter}" : defaultName,
            FontSize = 13,
            Padding = new Thickness(8, 6, 8, 6),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d1d5db")!),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f9fafb")!),
            MaxLength = 50,
            MinWidth = 120
        };

        var deleteBtn = new Button
        {
            Content = "删除",
            FontSize = 11,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444")!),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(6, 2, 6, 2),
            Visibility = isFirst ? Visibility.Collapsed : Visibility.Visible
        };

        var userPicker = new Controls.UserPickerControl
        {
            Users = _availableUsers,
            Margin = new Thickness(0, 8, 0, 0)
        };

        deleteBtn.Click += (s, _) => RemoveSubGroup(s);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
        headerRow.Children.Add(new TextBlock
        {
            Text = "小组名称",
            FontSize = 12,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b7280")!),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        headerRow.Children.Add(nameBox);
        headerRow.Children.Add(new TextBlock
        {
            Text = "成员",
            FontSize = 12,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b7280")!),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 8, 0)
        });
        headerRow.Children.Add(deleteBtn);

        var contentStack = new StackPanel();
        contentStack.Children.Add(headerRow);
        contentStack.Children.Add(userPicker);

        var container = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fafafa")!),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e5e7eb")!),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 6, 0, 0),
            Child = contentStack
        };

        var row = new SubGroupRow(container, nameBox, userPicker, deleteBtn);
        _subGroupRows.Add(row);
        SubGroupsPanel.Children.Add(container);
    }

    private void RemoveSubGroup(object? sender)
    {
        if (sender is not Button button) return;

        var row = _subGroupRows.FirstOrDefault(r => r.DeleteBtn == button);
        if (row == null) return;

        _subGroupRows.Remove(row);
        SubGroupsPanel.Children.Remove(row.Container);
    }

    private void AddSubGroupButton_Click(object sender, RoutedEventArgs e)
    {
        AddSubGroup();
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            ErrorText.Text = "请输入工作组名称";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var hasMembers = _subGroupRows.Any(row =>
            row.UserPicker.SelectedUsers != null && row.UserPicker.SelectedUsers.Count > 0);

        if (!hasMembers)
        {
            ErrorText.Text = "请至少添加一名成员";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var request = new CreateWorkGroupRequest
        {
            Name = NameTextBox.Text.Trim(),
            Description = DescriptionTextBox.Text.Trim(),
            TemplateType = (TemplateTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "default",
            DueTime = DueDatePicker.SelectedDate?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Members = BuildMembersList()
        };

        try
        {
            await _workGroupApi.CreateWorkGroupAsync(request);
            ErrorText.Visibility = Visibility.Collapsed;
            OnCreated?.Invoke("");
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"创建失败: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private List<GroupMemberRequest> BuildMembersList()
    {
        var members = new List<GroupMemberRequest>();

        for (var i = 0; i < _subGroupRows.Count; i++)
        {
            var row = _subGroupRows[i];
            var selectedUsers = row.UserPicker.SelectedUsers;
            if (selectedUsers == null || selectedUsers.Count == 0) continue;

            var subGroupName = row.NameBox.Text.Trim();
            var role = i == 0 ? "leader" : "member";

            foreach (var user in selectedUsers)
            {
                members.Add(new GroupMemberRequest
                {
                    UserId = user.Id,
                    Role = role,
                    SubGroup = subGroupName
                });
            }
        }

        return members;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }

    private sealed record SubGroupRow(
        Border Container,
        TextBox NameBox,
        Controls.UserPickerControl UserPicker,
        Button DeleteBtn);
}