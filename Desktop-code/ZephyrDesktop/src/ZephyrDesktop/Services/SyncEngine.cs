using System.Net.NetworkInformation;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using ZephyrDesktop.Data.Repositories;
using Serilog;
using ZephyrDesktop.Events;
using ZephyrDesktop.Models;
using ZephyrDesktop.Services.Api;
using ZephyrDesktop.ViewModels;
using ZephyrDesktop.Views;

namespace ZephyrDesktop.Services;

public sealed class SyncEngine : IDisposable
{
    private readonly INoteApi _noteApi;
    private readonly LocalNoteRepository _noteRepo;
    private readonly WindowManager _windowManager;
    private readonly IConfiguration _config;
    private readonly int _syncIntervalSeconds;
    private readonly int _syncDebounceMs;
    private Timer? _syncTimer;
    private Timer? _debounceTimer;
    private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);
    private bool _isOnline = true;
    private DateTime _lastSyncTime;

    public SyncEngine(
        INoteApi noteApi,
        LocalNoteRepository noteRepo,
        WindowManager windowManager,
        IConfiguration config)
    {
        _noteApi = noteApi;
        _noteRepo = noteRepo;
        _windowManager = windowManager;
        _config = config;
        _syncIntervalSeconds = config.GetValue("SyncIntervalSeconds", 30);
        _syncDebounceMs = config.GetValue("SyncDebounceMs", 5000);
        _lastSyncTime = DateTime.UtcNow;
    }

    private bool _fullSyncDone;

    public bool IsFullSyncDone => _fullSyncDone;

    public async Task StartupSyncAsync()
    {
        var localNotes = await _noteRepo.GetActiveNotesAsync();

        foreach (var local in localNotes)
        {
            if (_windowManager.OpenNotes.ContainsKey(local.Id)) continue;

            var note = _windowManager.CreateStickyNote(
                local.Id, local.Title, local.Content, local.ColorStatus, local.IsTopmost,
                syncOperationType: local.SyncOperationType,
                sourceType: local.SourceType,
                ownerIds: local.OwnerIds,
                dueTime: local.DueTime,
                completedAt: local.CompletedAt,
                remindCount: local.RemindCount,
                templateType: local.TemplateType,
                subTag: local.SubTag);

            note.Left = local.ScreenX;
            note.Top = local.ScreenY;
            note.Width = local.Width;
            note.Height = local.Height;

            EnsureOnScreen(note);

            if (local.IsMinimized)
                note.Fold();
        }

        _ = FullSyncAsync();

        _syncTimer = new Timer(
            _ => _ = IncrementalSyncAsync(),
            null,
            TimeSpan.FromSeconds(_syncIntervalSeconds),
            TimeSpan.FromSeconds(_syncIntervalSeconds));

        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
    }

    private async Task<List<NoteDto>> FetchAllNotesAsync(
        string status = "active",
        string? updatedAfter = null)
    {
        var allNotes = new List<NoteDto>();
        int page = 1;
        const int pageSize = 100;

        while (true)
        {
            var result = await _noteApi.ListNotesAsync(
                status: status,
                updated_after: updatedAfter,
                page: page,
                page_size: pageSize);

            allNotes.AddRange(result.Data);

            if (allNotes.Count >= result.Total)
                break;

            page++;
        }

        return allNotes;
    }

    private async Task FullSyncAsync()
    {
        try
        {
            await UploadDirtyNotesAsync();

            var allActive = await FetchAllNotesAsync(status: "active");
            var serverActiveIds = new HashSet<string>();

            foreach (var dto in allActive)
            {
                serverActiveIds.Add(dto.Id);

                var localNote = await _noteRepo.GetByIdAsync(dto.Id);
                var isConflict = localNote != null && localNote.SyncOperationType != (int)SyncOperationType.None && dto.UpdatedAt > localNote.UpdatedAt;

                if (isConflict)
                {
                    await _noteRepo.SetConflictAsync(localNote!.Id, true);
                    Log.Information("Conflict detected for note {NoteId}: server updated at {ServerUpdatedAt} > local updated at {LocalUpdatedAt} and local is dirty",
                        localNote.Id, dto.UpdatedAt, localNote.UpdatedAt);

                    if (_windowManager.OpenNotes.TryGetValue(dto.Id, out var conflictNote)
                        && conflictNote.ViewModel != null)
                    {
                        conflictNote.ViewModel.HasConflict = true;
                    }

                    await _noteRepo.SyncMetadataOnlyAsync(dto);
                }
                else
                {
                    await _noteRepo.SyncFromServerAsync(dto);
                }

                if (_windowManager.OpenNotes.TryGetValue(dto.Id, out var note))
                {
                    if (note.ViewModel != null)
                    {
                        if (!isConflict)
                        {
                            await note.ViewModel.UpdateFromDtoAsync(dto);
                            await note.UpdateWebViewContentAsync(dto.Content);
                        }
                    }
                }
                else
                {
                    _windowManager.CreateStickyNote(dto.Id, dto.Title, dto.Content, dto.ColorStatus,
                        dueTime: dto.DueTime,
                        completedAt: dto.CompletedAt,
                        remindCount: dto.RemindCount,
                        templateType: dto.TemplateType,
                        subTag: dto.SubTag);
                    if (_windowManager.OpenNotes.TryGetValue(dto.Id, out var newNote) && newNote.ViewModel != null)
                    {
                        await newNote.ViewModel.UpdateFromDtoAsync(dto);
                    }
                }
            }

            var allArchived = await FetchAllNotesAsync(status: "archived");
            var serverArchivedIds = new HashSet<string>();

            foreach (var dto in allArchived)
            {
                serverArchivedIds.Add(dto.Id);
                _windowManager.CloseStickyNote(dto.Id);
                await _noteRepo.MarkArchivedAsync(dto.Id);
            }

            var localNotes = await _noteRepo.GetActiveNotesAsync();
            foreach (var local in localNotes)
            {
                if (!serverActiveIds.Contains(local.Id) && !serverArchivedIds.Contains(local.Id))
                {
                    _windowManager.CloseStickyNote(local.Id);
                    await _noteRepo.MarkArchivedAsync(local.Id);
                }
                else if (serverArchivedIds.Contains(local.Id) && !serverActiveIds.Contains(local.Id))
                {
                    _windowManager.CloseStickyNote(local.Id);
                    await _noteRepo.MarkArchivedAsync(local.Id);
                }
            }

            _lastSyncTime = DateTime.UtcNow;
            _fullSyncDone = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "SyncEngine.FullSync");
        }
    }

    public void TriggerSync()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(
            _ => _ = IncrementalSyncAsync(),
            null,
            TimeSpan.FromMilliseconds(_syncDebounceMs),
            Timeout.InfiniteTimeSpan);
    }

    private async Task IncrementalSyncAsync()
    {
        if (!_isOnline) return;

        await _syncLock.WaitAsync();

        try
        {
            await UploadDirtyNotesAsync();
            await PullServerChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Step}]", "SyncEngine.IncrementalSync");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task UploadDirtyNotesAsync()
    {
        var dirtyNotes = await _noteRepo.GetDirtyNotesAsync();

        foreach (var local in dirtyNotes)
        {
            try
            {
                var opType = (SyncOperationType)local.SyncOperationType;

                if (opType == SyncOperationType.None)
                {
                    continue;
                }
                else if (opType == SyncOperationType.Created)
                {
                    var result = await _noteApi.CreateNoteAsync(new CreateNoteRequest
                    {
                        Title = local.Title,
                        Content = local.Content,
                        SourceType = local.SourceType,
                        OwnerIds = local.OwnerIds
                    });

                    if (result != null)
                    {
                        await _noteRepo.ReplaceIdAsync(local.Id, result.Id, result);

                        if (_windowManager.OpenNotes.TryGetValue(local.Id, out var note)
                            && note.ViewModel != null)
                        {
                            _windowManager.ReplaceNoteId(local.Id, result.Id);
                            note.UpdateNoteId(result.Id);
                            note.ViewModel.UpdateNoteId(result.Id);
                            note.ViewModel.IsPendingSync = false;
                        }
                    }
                }
                else if (opType == SyncOperationType.Updated)
                {
                    await _noteApi.UpdateNoteAsync(local.Id, new UpdateNoteRequest
                    {
                        Title = local.Title,
                        Content = local.Content,
                        ColorStatus = local.ColorStatus,
                        OwnerIds = local.OwnerIds
                    });

                    await _noteRepo.UpdateContentAsync(local.Id, local.Title, local.Content, local.ColorStatus, local.SourceType, (int)SyncOperationType.None);

                    if (_windowManager.OpenNotes.TryGetValue(local.Id, out var note)
                        && note.ViewModel != null)
                    {
                        note.ViewModel.IsPendingSync = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Step}]", "SyncEngine.UploadDirty");
                break;
            }
        }
    }

    private async Task PullServerChangesAsync()
    {
        var updatedAfter = _lastSyncTime.ToString("o");
        var activeNotes = await FetchAllNotesAsync(status: "active", updatedAfter: updatedAfter);

        foreach (var dto in activeNotes)
        {
            var localNote = await _noteRepo.GetByIdAsync(dto.Id);
            var isConflict = localNote != null && localNote.SyncOperationType != (int)SyncOperationType.None && dto.UpdatedAt > localNote.UpdatedAt;

            if (isConflict)
            {
                await _noteRepo.SetConflictAsync(localNote!.Id, true);
                Log.Information("Conflict detected for note {NoteId}: server updated at {ServerUpdatedAt} > local updated at {LocalUpdatedAt} and local is dirty",
                    localNote.Id, dto.UpdatedAt, localNote.UpdatedAt);

                if (_windowManager.OpenNotes.TryGetValue(dto.Id, out var conflictNote)
                    && conflictNote.ViewModel != null)
                {
                    conflictNote.ViewModel.HasConflict = true;
                }

                await _noteRepo.SyncMetadataOnlyAsync(dto);
            }
            else
            {
                await _noteRepo.SyncFromServerAsync(dto);
            }

            if (_windowManager.OpenNotes.TryGetValue(dto.Id, out var note))
            {
                if (note.ViewModel != null)
                {
                    if (!isConflict)
                    {
                        await note.ViewModel.UpdateFromDtoAsync(dto);
                        await note.UpdateWebViewContentAsync(dto.Content);
                    }
                }
            }
            else
            {
                _windowManager.CreateStickyNote(
                    dto.Id, dto.Title, dto.Content, dto.ColorStatus,
                    dueTime: dto.DueTime,
                    completedAt: dto.CompletedAt,
                    remindCount: dto.RemindCount,
                    templateType: dto.TemplateType,
                    subTag: dto.SubTag);
                if (_windowManager.OpenNotes.TryGetValue(dto.Id, out var newNote) && newNote.ViewModel != null)
                {
                    await newNote.ViewModel.UpdateFromDtoAsync(dto);
                }
            }
        }

        var archivedNotes = await FetchAllNotesAsync(status: "archived", updatedAfter: updatedAfter);

        foreach (var dto in archivedNotes)
        {
            _windowManager.CloseStickyNote(dto.Id);
            await _noteRepo.MarkArchivedAsync(dto.Id);
        }

        _lastSyncTime = DateTime.UtcNow;
    }

    private void OnNetworkChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        _isOnline = e.IsAvailable;

        if (_isOnline)
        {
            WeakReferenceMessenger.Default.Send(new NetworkStatusEvent(true));
            _ = IncrementalSyncAsync();
        }
        else
        {
            WeakReferenceMessenger.Default.Send(new NetworkStatusEvent(false));
        }
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
        _debounceTimer?.Dispose();
        _syncLock.Dispose();
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
    }

    private static void EnsureOnScreen(Window window)
    {
        var screenBounds = System.Windows.SystemParameters.WorkArea;
        if (window.Left + window.Width < screenBounds.Left + 50)
            window.Left = screenBounds.Left + 50;
        if (window.Top + window.Height < screenBounds.Top + 20)
            window.Top = screenBounds.Top + 20;
        if (window.Left > screenBounds.Right - 50)
            window.Left = screenBounds.Right - window.Width;
        if (window.Top > screenBounds.Bottom - 50)
            window.Top = screenBounds.Bottom - window.Height;
    }
}
