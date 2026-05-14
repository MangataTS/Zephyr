using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ZephyrDesktop.Data;
using ZephyrDesktop.Data.Repositories;
using ZephyrDesktop.Events;
using ZephyrDesktop.Models;
using ZephyrDesktop.Services;
using ZephyrDesktop.Services.Api;

namespace ZephyrDesktop.ViewModels;

public sealed partial class CreateNoteViewModel : ViewModelBase
{
    private readonly INoteApi _noteApi;
    private readonly ITagApi _tagApi;
    private readonly IUserApi _userApi;
    private readonly AuthService _authService;
    private readonly LocalNoteRepository _noteRepo;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _content = "";

    [ObservableProperty]
    private string _sourceType = "self";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private List<TagDto> _availableTags = [];

    [ObservableProperty]
    private List<string> _selectedTagIds = [];

    [ObservableProperty]
    private List<UserDto> _availableUsers = [];

    [ObservableProperty]
    private List<UserDto> _selectedAssignees = [];

    [ObservableProperty]
    private bool _canAssign;

    [ObservableProperty]
    private string _subTag = "";

    public CreateNoteViewModel(INoteApi noteApi, ITagApi tagApi, IUserApi userApi, AuthService authService, LocalNoteRepository noteRepo)
    {
        _noteApi = noteApi;
        _tagApi = tagApi;
        _userApi = userApi;
        _authService = authService;
        _noteRepo = noteRepo;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var response = await _tagApi.GetTagsAsync();
            if (response.IsSuccessStatusCode && response.Content != null)
            {
                AvailableTags = response.Content;
            }
        }
        catch
        {
        }

        try
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            CanAssign = currentUser != null && !string.Equals(currentUser.Role, "member", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            CanAssign = false;
        }

        if (CanAssign)
        {
            try
            {
                var userResponse = await _userApi.GetVisibleUsersAsync();
                if (userResponse.IsSuccessStatusCode && userResponse.Content != null)
                {
                    AvailableUsers = userResponse.Content;
                }
            }
            catch
            {
            }
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "请输入任务标题";
            return;
        }

        if (SelectedTagIds == null || SelectedTagIds.Count == 0)
        {
            ErrorMessage = "请选择一级标签";
            return;
        }

        IsLoading = true;
        ErrorMessage = "";

        try
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                var saved = await SaveLocallyAndOpenAsync();
                if (!saved) ErrorMessage = "创建失败";
                return;
            }

            var sourceType = SelectedAssignees?.Count > 0 ? "assigned" : "self";
            var ownerIds = SelectedAssignees != null ? string.Join(",", SelectedAssignees.Select(u => u.Id)) : "";

            var request = new CreateNoteRequest
            {
                Title = Title,
                Content = Content,
                SourceType = sourceType,
                Tags = SelectedTagIds,
                OwnerIds = ownerIds,
                SubTag = SubTag
            };

            var result = await _noteApi.CreateNoteAsync(request);
            if (result != null)
            {
                WeakReferenceMessenger.Default.Send(new NoteCreatedEvent(result));
            }
            else
            {
                var saved = await SaveLocallyAndOpenAsync();
                if (!saved) ErrorMessage = "创建失败";
            }
        }
        catch (Exception ex)
        {
            var saved = await SaveLocallyAndOpenAsync();
            if (!saved) ErrorMessage = $"创建失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<bool> SaveLocallyAndOpenAsync()
    {
        try
        {
            var tempId = Guid.NewGuid().ToString();
            var sourceType = SelectedAssignees?.Count > 0 ? "assigned" : "self";
            var ownerIds = SelectedAssignees != null ? string.Join(",", SelectedAssignees.Select(u => u.Id)) : "";
            var tagNames = AvailableTags.Where(t => SelectedTagIds.Contains(t.Id)).Select(t => t.Name).ToList();
            var tagsJson = System.Text.Json.JsonSerializer.Serialize(tagNames);

            var localNote = new LocalNote
            {
                Id = tempId,
                Title = Title,
                Content = Content,
                SyncOperationType = (int)SyncOperationType.Created,
                ColorStatus = "yellow",
                SourceType = sourceType,
                OwnerIds = ownerIds,
                TagsJson = tagsJson,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _noteRepo.UpsertAsync(localNote);

            var wm = App.GetService<WindowManager>();
            if (wm != null)
            {
                wm.CreateStickyNote(tempId, Title, Content, "yellow", true,
                    (int)SyncOperationType.Created, sourceType, ownerIds);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
