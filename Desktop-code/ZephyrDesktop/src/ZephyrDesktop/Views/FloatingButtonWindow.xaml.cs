using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;
using ZephyrDesktop.Services;

namespace ZephyrDesktop.Views;

public partial class FloatingButtonWindow : Window
{
    private readonly double _dragThreshold;
    private Point _mouseDownPoint;
    private bool _isDragging;

    public Action? OnCreateNote { get; set; }
    public Action? OnWorkbench { get; set; }
    public Action? OnNoteOverview { get; set; }
    public Action? OnWorkGroup { get; set; }
    public Action? OnCreateWorkGroup { get; set; }

    public FloatingButtonWindow()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Width - Width - 20;
        Top = 20;
        var config = App.GetService<IConfiguration>();
        _dragThreshold = config?.GetValue("DragThreshold", 5.0) ?? 5.0;

        LeftClickPopup.Opened += (_, _) =>
        {
            var wm = App.GetService<WindowManager>();
            if (wm != null)
            {
                MenuToggleNotesText.Text = wm.IsNotesHidden ? "👁 显示所有任务" : "👁 隐藏所有任务";
            }
        };
    }

    private void ButtonBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownPoint = e.GetPosition(this);
        _isDragging = false;
        ButtonBorder.CaptureMouse();
    }

    private void ButtonBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var current = e.GetPosition(this);
        var delta = current - _mouseDownPoint;

        if (!_isDragging && (Math.Abs(delta.X) > _dragThreshold || Math.Abs(delta.Y) > _dragThreshold))
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            Left += delta.X;
            Top += delta.Y;
        }
    }

    private void ButtonBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ButtonBorder.ReleaseMouseCapture();

        if (!_isDragging)
        {
            LeftClickPopup.IsOpen = true;
        }

        _isDragging = false;
    }

    private void MenuItemNormalTask_Click(object sender, MouseButtonEventArgs e)
    {
        LeftClickPopup.IsOpen = false;
        OnCreateNote?.Invoke();
    }

    private void MenuItemWorkGroup_Click(object sender, MouseButtonEventArgs e)
    {
        LeftClickPopup.IsOpen = false;
        OnWorkGroup?.Invoke();
    }

    private void MenuItemCreateWorkGroup_Click(object sender, MouseButtonEventArgs e)
    {
        LeftClickPopup.IsOpen = false;
        OnCreateWorkGroup?.Invoke();
    }

    private void MenuItemNoteOverview_Click(object sender, MouseButtonEventArgs e)
    {
        LeftClickPopup.IsOpen = false;
        OnNoteOverview?.Invoke();
    }

    private void MenuItemWorkbench_Click(object sender, MouseButtonEventArgs e)
    {
        LeftClickPopup.IsOpen = false;
        OnWorkbench?.Invoke();
    }

    private void MenuItemToggleNotes_Click(object sender, MouseButtonEventArgs e)
    {
        LeftClickPopup.IsOpen = false;
        var wm = App.GetService<WindowManager>();
        if (wm == null) return;
        if (wm.IsNotesHidden)
            wm.ShowAllNotes();
        else
            wm.HideAllNotes();
    }

    private void MenuItem_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3F4F6"));
        }
    }

    private void MenuItem_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = System.Windows.Media.Brushes.Transparent;
        }
    }
}
