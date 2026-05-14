using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZephyrDesktop.Data;
using ZephyrDesktop.Data.Repositories;
using Serilog;
using ZephyrDesktop.Events;
using ZephyrDesktop.Models;
using ZephyrDesktop.Services;
using ZephyrDesktop.Services.Api;
using CommunityToolkit.Mvvm.Messaging;

namespace ZephyrDesktop.ViewModels;

public sealed partial class StickyNoteViewModel : ViewModelBase
{
    private string _noteId;
    private readonly INoteApi _noteApi;
    private readonly ITagApi _tagApi;
    private readonly AuthService _authService;
    private readonly LocalNoteRepository _noteRepo;
    private readonly DispatcherTimer _localSaveTimer;
    private readonly DispatcherTimer _apiSyncTimer;
    private bool _isSyncingFromDto;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _content = "";

    [ObservableProperty]
    private string _colorStatus = "yellow";

    [ObservableProperty]
    private string _sourceType = "self";

    [ObservableProperty]
    private int _syncOperationType;

    public bool IsDirty => SyncOperationType != (int)Models.SyncOperationType.None;

    [ObservableProperty]
    private bool _hasConflict;

    [ObservableProperty]
    private bool _isPendingSync;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private List<TagDto> _availableTags = [];

    [ObservableProperty]
    private List<string> _selectedTagIds = [];

    [ObservableProperty]
    private string _subTag = "";

    [ObservableProperty]
    private string _templateType = "default";

    [ObservableProperty]
    private int _remindCount;

    [ObservableProperty]
    private DateTime? _dueTime;

    [ObservableProperty]
    private DateTime? _completedAt;

    public bool IsSupervisionTask => RemindCount > 0 && CompletedAt == null;
    public bool IsGroupTask => TemplateType == "group";
    public string TaskTypeLabel => IsGroupTask ? "专项组任务" : IsSupervisionTask ? "盯办任务" : "";

    public string NoteId => _noteId;

    public void UpdateNoteId(string newId)
    {
        _noteId = newId;
    }

    public StickyNoteViewModel(string noteId, INoteApi noteApi, ITagApi tagApi, AuthService authService, LocalNoteRepository noteRepo, int editDebounceMs = 500)
    {
        _noteId = noteId;
        _noteApi = noteApi;
        _tagApi = tagApi;
        _authService = authService;
        _noteRepo = noteRepo;

        _localSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(editDebounceMs) };
        _localSaveTimer.Tick += async (_, _) =>
        {
            _localSaveTimer.Stop();
            if (IsDirty)
            {
                await SaveToLocalAsync();
                _apiSyncTimer.Start();
            }
        };

        _apiSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _apiSyncTimer.Tick += async (_, _) =>
        {
            _apiSyncTimer.Stop();
            if (IsDirty)
            {
                await SaveToApiAsync();
            }
        };

        _ = LoadAvailableTagsAsync();
    }

    public async Task LoadAvailableTagsAsync()
    {
        try
        {
            var response = await _tagApi.GetTagsAsync();
            if (response.IsSuccessStatusCode && response.Content != null)
            {
                AvailableTags = response.Content;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "StickyNoteViewModel.LoadAvailableTags");
        }
    }

    partial void OnSyncOperationTypeChanged(int value)
    {
        OnPropertyChanged(nameof(IsDirty));
    }

    partial void OnSelectedTagIdsChanged(List<string> value)
    {
        if (_isSyncingFromDto) return;
        _ = SyncTagsToApiAsync();
    }

    private async Task SyncTagsToApiAsync()
    {
        try
        {
            await _noteApi.UpdateNoteAsync(_noteId, new UpdateNoteRequest
            {
                Tags = SelectedTagIds
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "StickyNoteViewModel.SyncTagsToApi");
        }
    }

    public void MarkDirty()
    {
        IsPendingSync = true;
        if (HasConflict)
        {
            HasConflict = false;
            _ = _noteRepo.SetConflictAsync(_noteId, false);
        }
        _ = _noteRepo.SetSyncOperationTypeAsync(_noteId, (int)Models.SyncOperationType.Updated);
        _apiSyncTimer.Stop();
        _localSaveTimer.Stop();
        _localSaveTimer.Start();
    }

    private async Task SaveToLocalAsync()
    {
        try
        {
            await _noteRepo.UpdateContentAsync(_noteId, Title, Content, ColorStatus, SourceType, (int)Models.SyncOperationType.Updated);
            await _noteRepo.UpdateSubTagAsync(_noteId, SubTag);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "StickyNoteViewModel.SaveToLocal");
        }
    }

    private async Task SaveToApiAsync()
    {
        try
        {
            await _noteApi.UpdateNoteAsync(_noteId, new UpdateNoteRequest
            {
                Title = Title,
                Content = Content,
                ColorStatus = ColorStatus,
                Tags = SelectedTagIds,
                SubTag = SubTag
            });
            IsPendingSync = false;
            await _noteRepo.UpdateContentAsync(_noteId, Title, Content, ColorStatus, SourceType, (int)Models.SyncOperationType.None);
            await _noteRepo.SetSyncOperationTypeAsync(_noteId, (int)Models.SyncOperationType.None);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "StickyNoteViewModel.SaveToApi");
        }
    }

    [RelayCommand]
    private async Task SaveContentAsync()
    {
        if (!IsDirty) return;

        try
        {
            await SaveToApiAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        try
        {
            await _noteApi.CompleteNoteAsync(_noteId, new CompleteNoteRequest());
            await _noteRepo.MarkArchivedAsync(_noteId);
            WeakReferenceMessenger.Default.Send(new NoteCompletedEvent(_noteId));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public async Task UpdateFromDtoAsync(NoteDto dto)
    {
        Title = dto.Title;
        Content = dto.Content;
        ColorStatus = dto.ColorStatus;
        SourceType = dto.SourceType;
        SubTag = dto.SubTag;
        TemplateType = dto.TemplateType;
        RemindCount = dto.RemindCount;
        DueTime = dto.DueTime;
        CompletedAt = dto.CompletedAt;

        _isSyncingFromDto = true;
        SelectedTagIds = dto.Tags.Select(t => t.Id).ToList();
        _isSyncingFromDto = false;

        SyncOperationType = (int)Models.SyncOperationType.None;

        var localNote = await _noteRepo.GetByIdAsync(_noteId);
        if (localNote != null)
        {
            HasConflict = localNote.HasConflict;
            IsPendingSync = localNote.SyncOperationType != (int)Models.SyncOperationType.None;
        }
        else
        {
            IsPendingSync = false;
        }

        return;
    }
}
