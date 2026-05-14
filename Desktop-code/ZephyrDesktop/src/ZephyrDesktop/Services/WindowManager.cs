using System.Collections.Concurrent;
using System.Windows;
using Microsoft.Extensions.Configuration;
using ZephyrDesktop.Data;
using ZephyrDesktop.Data.Repositories;
using ZephyrDesktop.Services.Api;
using ZephyrDesktop.ViewModels;
using ZephyrDesktop.Views;

namespace ZephyrDesktop.Services;

public sealed class WindowManager
{
    private readonly ConcurrentDictionary<string, StickyNoteWindow> _openNotes = [];
    private FloatingButtonWindow? _floatingButton;
    private CreateNoteWindow? _createNoteWindow;
    private WorkbenchWindow? _workbenchWindow;
    private NoteOverviewWindow? _noteOverviewWindow;
    private WorkGroupWindow? _workGroupWindow;

    private readonly int _maxDesktopNotes;
    private readonly int _editDebounceMs;
    private bool _isNotesHidden;

    private static readonly Dictionary<string, int> ColorPriority = new()
    {
        ["red"] = 3,
        ["yellow"] = 2,
        ["green"] = 1
    };

    public WindowManager(IConfiguration config)
    {
        _maxDesktopNotes = config.GetValue("MaxDesktopNotes", 20);
        _editDebounceMs = config.GetValue("EditDebounceMs", 500);
    }

    public IReadOnlyDictionary<string, StickyNoteWindow> OpenNotes => _openNotes;

    public void ReplaceNoteId(string oldId, string newId)
    {
        if (_openNotes.TryRemove(oldId, out var window))
        {
            _openNotes[newId] = window;
        }
    }

    public void ShowFloatingButton()
    {
        EnsureOnUiThread(() =>
        {
            if (_floatingButton != null)
            {
                _floatingButton.Show();
                return;
            }

            _floatingButton = new FloatingButtonWindow();
            _floatingButton.OnCreateNote = () => ShowCreateNoteWindow();
            _floatingButton.OnWorkbench = () => ShowWorkbenchWindow();
            _floatingButton.OnNoteOverview = () => ShowNoteOverviewWindow();
            _floatingButton.OnWorkGroup = () => ShowWorkGroupWindow();
            _floatingButton.OnCreateWorkGroup = () => ShowCreateWorkGroupWindow();
            _floatingButton.Closed += (_, _) => _floatingButton = null;
            _floatingButton.Show();
        });
    }

    public void ShowCreateNoteWindow()
    {
        EnsureOnUiThread(() =>
        {
            if (_createNoteWindow != null)
            {
                _createNoteWindow.Activate();
                return;
            }

            _createNoteWindow = new CreateNoteWindow();
            _createNoteWindow.Closed += (_, _) => _createNoteWindow = null;
            _createNoteWindow.Show();
        });
    }

    public void ShowWorkbenchWindow()
    {
        EnsureOnUiThread(() =>
        {
            if (_workbenchWindow != null)
            {
                _workbenchWindow.Activate();
                return;
            }

            _workbenchWindow = new WorkbenchWindow();
            _workbenchWindow.Closed += (_, _) => _workbenchWindow = null;
            _workbenchWindow.Show();
        });
    }

    public void ShowNoteOverviewWindow()
    {
        EnsureOnUiThread(() =>
        {
            if (_noteOverviewWindow != null)
            {
                _noteOverviewWindow.Activate();
                return;
            }

            var vm = App.GetService<NoteOverviewViewModel>()!;
            _noteOverviewWindow = new NoteOverviewWindow(vm);
            _noteOverviewWindow.Closed += (_, _) => _noteOverviewWindow = null;
            _noteOverviewWindow.Show();
        });
    }

    public void ShowWorkGroupWindow()
    {
        EnsureOnUiThread(() =>
        {
            if (_workGroupWindow != null)
            {
                _workGroupWindow.Activate();
                return;
            }

            var config = App.GetService<IConfiguration>()!;
            var tokenStorage = App.GetService<TokenStorage>()!;
            var baseUrl = config.GetValue<string>("WebFrontUrl") ?? "http://localhost:3001";
            var jwtToken = tokenStorage.GetAccessTokenAsync().GetAwaiter().GetResult() ?? "";

            _workGroupWindow = new WorkGroupWindow(baseUrl, jwtToken);
            _workGroupWindow.Closed += (_, _) => _workGroupWindow = null;
            _workGroupWindow.Show();
        });
    }

    public void ShowCreateWorkGroupWindow()
    {
        EnsureOnUiThread(() =>
        {
            if (_workGroupWindow != null)
            {
                _workGroupWindow.Close();
                _workGroupWindow = null;
            }

            var createWindow = new CreateWorkGroupWindow();
            createWindow.OnCreated += (_) =>
            {
                ShowWorkGroupWindow();
            };
            createWindow.ShowDialog();
        });
    }

    public void FocusNote(string noteId)
    {
        EnsureOnUiThread(() =>
        {
            if (_openNotes.TryGetValue(noteId, out var existing))
            {
                if (existing.IsFolded)
                {
                    existing.Expand();
                }
                existing.Topmost = true;
                existing.Topmost = false;
                existing.Activate();
                return;
            }

            var noteRepo = App.GetService<LocalNoteRepository>()!;
            var localNote = noteRepo.GetByIdAsync(noteId).GetAwaiter().GetResult();
            if (localNote != null)
            {
                CreateStickyNote(localNote.Id, localNote.Title, localNote.Content, localNote.ColorStatus, localNote.IsTopmost,
                    dueTime: localNote.DueTime,
                    completedAt: localNote.CompletedAt,
                    remindCount: localNote.RemindCount,
                    templateType: localNote.TemplateType,
                    subTag: localNote.SubTag);
            }
        });
    }

    public StickyNoteWindow CreateStickyNote(string noteId, string title, string content, string colorStatus, bool isTopmost = true, int syncOperationType = 0, string sourceType = "self", string ownerIds = "", DateTime? dueTime = null, DateTime? completedAt = null, int remindCount = 0, string templateType = "default", string subTag = "")
    {
        if (_openNotes.TryGetValue(noteId, out var existing))
        {
            EnsureOnUiThread(() => existing.Activate());
            return existing;
        }

        StickyNoteWindow? note = null;

        EnsureOnUiThread(() =>
        {
            EnforceMaxNotesLimit();

            note = new StickyNoteWindow(noteId, title, content, colorStatus, isTopmost);

            var noteApi = App.GetService<INoteApi>()!;
            var tagApi = App.GetService<ITagApi>()!;
            var authService = App.GetService<AuthService>()!;
            var noteRepo = App.GetService<LocalNoteRepository>()!;
            var viewModel = new StickyNoteViewModel(noteId, noteApi, tagApi, authService, noteRepo, _editDebounceMs);
            viewModel.Title = title;
            viewModel.Content = content;
            viewModel.ColorStatus = colorStatus;
            viewModel.DueTime = dueTime;
            viewModel.CompletedAt = completedAt;
            viewModel.RemindCount = remindCount;
            viewModel.TemplateType = templateType;
            viewModel.SubTag = subTag;
            viewModel.IsPendingSync = syncOperationType != 0;
            note!.SetViewModel(viewModel);

            note.Closed += (_, _) => _openNotes.TryRemove(noteId, out _);
            _openNotes[noteId] = note;
            note.Show();
        });

        _ = App.GetService<LocalNoteRepository>()!.UpsertAsync(new LocalNote
        {
            Id = noteId,
            Title = title,
            Content = content,
            ColorStatus = colorStatus,
            SourceType = sourceType,
            OwnerIds = ownerIds,
            SyncOperationType = syncOperationType,
            IsTopmost = true,
            LastSyncAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        BringSupervisionNotesToTop();

        return note!;
    }

    public void CloseStickyNote(string noteId)
    {
        if (_openNotes.TryGetValue(noteId, out var note))
        {
            EnsureOnUiThread(() =>
            {
                note.Close();
                _openNotes.TryRemove(noteId, out _);
            });
        }
    }

    public void CloseAllStickyNotes()
    {
        EnsureOnUiThread(() =>
        {
            foreach (var note in _openNotes.Values)
            {
                note.Close();
            }
            _openNotes.Clear();
        });
    }

    public bool IsNotesHidden => _isNotesHidden;

    public void HideAllNotes()
    {
        EnsureOnUiThread(() =>
        {
            foreach (var note in _openNotes.Values)
            {
                if (note.ViewModel?.IsSupervisionTask == true) continue;
                note.Hide();
            }
            _isNotesHidden = true;
        });
    }

    public void ShowAllNotes()
    {
        EnsureOnUiThread(() =>
        {
            foreach (var note in _openNotes.Values)
            {
                note.Show();
            }
            _isNotesHidden = false;

            BringSupervisionNotesToTop();
        });
    }

    public void HideNote(string noteId)
    {
        EnsureOnUiThread(() =>
        {
            if (_openNotes.TryGetValue(noteId, out var note))
            {
                if (note.ViewModel?.IsSupervisionTask == true) return;
                note.Hide();
            }
        });
    }

    public void ShowNote(string noteId)
    {
        EnsureOnUiThread(() =>
        {
            if (_openNotes.TryGetValue(noteId, out var note))
            {
                note.Show();
            }
        });
    }

    public void HideFloatingButton()
    {
        EnsureOnUiThread(() =>
        {
            if (_floatingButton != null)
            {
                _floatingButton.Hide();
            }
        });
    }

    private void EnforceMaxNotesLimit()
    {
        var expandedNotes = _openNotes.Values
            .Where(n => !n.IsFolded)
            .ToList();

        while (expandedNotes.Count >= _maxDesktopNotes && expandedNotes.Count > 0)
        {
            var lowestPriority = expandedNotes
                .OrderBy(n => GetNotePriority(n))
                .ThenBy(n => n.ViewModel?.Title)
                .First();

            lowestPriority.Fold();
            expandedNotes.Remove(lowestPriority);
        }
    }

    private static int GetNotePriority(StickyNoteWindow note)
    {
        var colorStatus = note.ViewModel?.ColorStatus ?? "yellow";
        return ColorPriority.TryGetValue(colorStatus, out var p) ? p : 0;
    }

    private void BringSupervisionNotesToTop()
    {
        foreach (var note in _openNotes.Values)
        {
            if (note.ViewModel?.IsSupervisionTask == true)
            {
                note.Topmost = false;
                note.Topmost = true;
            }
        }
    }

    private static void EnsureOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            action();
        }
        else
        {
            Application.Current?.Dispatcher.Invoke(action);
        }
    }
}
