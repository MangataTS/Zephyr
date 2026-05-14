using System.Drawing;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Hardcodet.Wpf.TaskbarNotification;
using ZephyrDesktop.Events;
using ZephyrDesktop.Views;

namespace ZephyrDesktop.Services;

public sealed class TrayService : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private readonly WindowManager _windowManager;
    private Icon? _defaultIcon;
    private Icon? _alertIcon;

    public TrayService(WindowManager windowManager)
    {
        _windowManager = windowManager;
    }

    public void Initialize()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _defaultIcon = LoadIconFromFile(Path.Combine(baseDir, "icon_tray.ico"));
        _alertIcon = LoadIconFromFile(Path.Combine(baseDir, "icon_alert.ico"));

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "轻燕桌面端",
            Icon = _defaultIcon ?? CreateFallbackIcon(System.Drawing.Color.DodgerBlue),
            Visibility = Visibility.Visible,
            ContextMenu = CreateContextMenu()
        };

        _trayIcon.TrayLeftMouseDown += (_, _) => _windowManager.ShowCreateNoteWindow();

        WeakReferenceMessenger.Default.Register<NoteCompletedEvent>(this, (_, _) => UpdateTrayStatus());
        WeakReferenceMessenger.Default.Register<NetworkStatusEvent>(this, (_, msg) =>
        {
            if (_trayIcon == null) return;
            _trayIcon.ToolTipText = msg.IsOnline ? "轻燕桌面端" : "轻燕桌面端 - 离线";
        });

        WeakReferenceMessenger.Default.Register<NoteRemindedEvent>(this, (_, msg) => UpdateTrayStatus());

        UpdateTrayStatus();
    }

    private static Icon? LoadIconFromFile(string path)
    {
        try
        {
            return File.Exists(path) ? new Icon(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static Icon CreateFallbackIcon(System.Drawing.Color color)
    {
        var size = 16;
        var bmp = new System.Drawing.Bitmap(size, size);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.Transparent);
        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
        g.FillRectangle(brush, 1, 0, 12, 14);
        using var whiteBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
        g.FillRectangle(whiteBrush, 3, 3, 8, 1);
        g.FillRectangle(whiteBrush, 3, 6, 6, 1);
        g.FillRectangle(whiteBrush, 3, 9, 7, 1);
        using var foldBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(180, color.R, color.G, color.B));
        var foldPath = new System.Drawing.Drawing2D.GraphicsPath();
        foldPath.AddLine(10, 0, 13, 3);
        foldPath.AddLine(10, 3, 10, 0);
        foldPath.CloseFigure();
        g.FillPath(foldBrush, foldPath);
        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private System.Windows.Controls.ContextMenu CreateContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var createItem = new System.Windows.Controls.MenuItem { Header = "📋 普通任务" };
        createItem.Click += (_, _) => _windowManager.ShowCreateNoteWindow();
        menu.Items.Add(createItem);

        var workGroupItem = new System.Windows.Controls.MenuItem { Header = "🏢 专项工作组" };
        workGroupItem.Click += (_, _) => _windowManager.ShowWorkGroupWindow();
        menu.Items.Add(workGroupItem);

        var createWorkGroupItem = new System.Windows.Controls.MenuItem { Header = "✨ 创建专项工作" };
        createWorkGroupItem.Click += (_, _) => _windowManager.ShowCreateWorkGroupWindow();
        menu.Items.Add(createWorkGroupItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var overviewItem = new System.Windows.Controls.MenuItem { Header = "📊 任务概览" };
        overviewItem.Click += (_, _) => _windowManager.ShowNoteOverviewWindow();
        menu.Items.Add(overviewItem);

        var workbenchItem = new System.Windows.Controls.MenuItem { Header = "💼 工作台" };
        workbenchItem.Click += (_, _) => _windowManager.ShowWorkbenchWindow();
        menu.Items.Add(workbenchItem);

        var toggleNotesItem = new System.Windows.Controls.MenuItem();
        toggleNotesItem.Header = _windowManager.IsNotesHidden ? "👁 显示所有任务" : "👁 隐藏所有任务";
        toggleNotesItem.Click += (_, _) =>
        {
            if (_windowManager.IsNotesHidden)
                _windowManager.ShowAllNotes();
            else
                _windowManager.HideAllNotes();
            toggleNotesItem.Header = _windowManager.IsNotesHidden ? "👁 显示所有任务" : "👁 隐藏所有任务";
        };
        menu.Items.Add(toggleNotesItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "退出" };
        exitItem.Click += (_, _) =>
        {
            _windowManager.CloseAllStickyNotes();
            Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);

        menu.Opened += (_, _) =>
        {
            toggleNotesItem.Header = _windowManager.IsNotesHidden ? "👁 显示所有任务" : "👁 隐藏所有任务";
        };

        return menu;
    }

    private void UpdateTrayStatus()
    {
        if (_trayIcon == null) return;

        var notes = _windowManager.OpenNotes.Values.ToList();
        var hasRed = notes.Any(n => n.ViewModel?.ColorStatus?.Equals("red", StringComparison.OrdinalIgnoreCase) == true);

        if (hasRed && _alertIcon != null)
        {
            _trayIcon.Icon = _alertIcon;
            _trayIcon.ToolTipText = "轻燕桌面端 - 有盯办提醒";
        }
        else if (_defaultIcon != null)
        {
            _trayIcon.Icon = _defaultIcon;
            _trayIcon.ToolTipText = "轻燕桌面端";
        }
    }

    public void RefreshStatus()
    {
        Application.Current.Dispatcher.Invoke(UpdateTrayStatus);
    }

    public void ShowBalloonTip(string title, string message)
    {
        _trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
    }

    public void Dispose()
    {
        _defaultIcon?.Dispose();
        _alertIcon?.Dispose();
        _trayIcon?.Dispose();
    }
}