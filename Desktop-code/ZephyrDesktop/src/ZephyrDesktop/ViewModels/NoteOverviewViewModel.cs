using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ZephyrDesktop.Data.Repositories;
using ZephyrDesktop.Events;
using ZephyrDesktop.Services;

namespace ZephyrDesktop.ViewModels;

public sealed partial class NoteOverviewViewModel : ViewModelBase
{
    private readonly LocalNoteRepository _noteRepo;
    private readonly SyncEngine _syncEngine;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _filterCategory = "All";

    [ObservableProperty]
    private int _totalCount;

    public ObservableCollection<NoteOverviewItem> Notes { get; } = [];
    public ObservableCollection<NoteOverviewItem> FilteredNotes { get; } = [];

    public NoteOverviewViewModel(LocalNoteRepository noteRepo, SyncEngine syncEngine)
    {
        _noteRepo = noteRepo;
        _syncEngine = syncEngine;

        WeakReferenceMessenger.Default.Register<SyncTriggerEvent>(this, (r, msg) =>
        {
            _ = ((NoteOverviewViewModel)r).LoadNotesAsync();
        });
    }

    public async Task LoadNotesAsync()
    {
        var allLocalNotes = await _noteRepo.GetAllNotesAsync();

        Notes.Clear();
        foreach (var note in allLocalNotes)
        {
            Notes.Add(new NoteOverviewItem
            {
                Id = note.Id,
                Title = note.Title,
                ColorStatus = note.ColorStatus,
                SourceType = note.SourceType,
                OwnerName = "",
                DueTime = note.DueTime,
                IsArchived = note.IsArchived,
                UpdatedAt = note.UpdatedAt,
                HasConflict = note.HasConflict,
                RemindCount = note.RemindCount
            });
        }

        ApplyFilter();

        _syncEngine.TriggerSync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnFilterCategoryChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = Notes.AsEnumerable();

        query = FilterCategory switch
        {
            "Mine" => query.Where(n => n.SourceType == "self" && !n.IsArchived),
            "Assigned" => query.Where(n => n.SourceType == "assigned" && !n.IsArchived),
            "Reminded" => query.Where(n => n.RemindCount > 0 && !n.IsArchived),
            "Completed" => query.Where(n => n.IsArchived),
            _ => query.Where(n => !n.IsArchived)
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim().ToLowerInvariant();
            query = query.Where(n => n.Title.ToLowerInvariant().Contains(search));
        }

        FilteredNotes.Clear();
        foreach (var item in query)
        {
            FilteredNotes.Add(item);
        }

        TotalCount = FilteredNotes.Count;
    }

    [RelayCommand]
    private void FocusNote(string noteId)
    {
        WeakReferenceMessenger.Default.Send(new FocusNoteEvent(noteId));
    }

    [RelayCommand]
    private void EditNote(string noteId)
    {
        var wm = App.GetService<WindowManager>();
        wm?.FocusNote(noteId);
    }
}

public sealed class NoteOverviewItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string ColorStatus { get; set; } = "yellow";
    public string SourceType { get; set; } = "self";
    public string OwnerName { get; set; } = "";
    public DateTime? DueTime { get; set; }
    public bool IsArchived { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool HasConflict { get; set; }
    public int RemindCount { get; set; }
    public string DueTimeText => DueTime.HasValue ? $" · 截止 {DueTime:MM-dd HH:mm}" : "";
    public string SourceText => SourceType == "assigned" ? "指派任务" : "自己创建";
}
