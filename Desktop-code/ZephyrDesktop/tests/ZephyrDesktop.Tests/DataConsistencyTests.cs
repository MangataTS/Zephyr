using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ZephyrDesktop.Data;
using ZephyrDesktop.Data.Repositories;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Tests;

public class DataConsistencyTests : IDisposable
{
    private readonly string _dbPath;
    private readonly LocalNoteRepository _repo;

    public DataConsistencyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"zephyr_test_{Guid.NewGuid():N}.db");
        _repo = new LocalNoteRepository(() => new AppDbContext(_dbPath));
        _repo.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            var dir = Path.GetDirectoryName(_dbPath)!;
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch { }
    }

    private static LocalNote CreateTestNote(string id, string title = "测试任务", string content = "测试内容",
        string colorStatus = "yellow", int syncOperationType = 0, double screenX = 100, double screenY = 100,
        bool isTopmost = true, bool isMinimized = false)
    {
        return new LocalNote
        {
            Id = id, Title = title, Content = content, ColorStatus = colorStatus,
            SourceType = "self", SyncOperationType = syncOperationType, IsTopmost = isTopmost,
            IsMinimized = isMinimized, ScreenX = screenX, ScreenY = screenY,
            Width = 280, Height = 320, LastSyncAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
    }

    private static NoteDto CreateTestDto(string id, string title = "服务端任务", string content = "服务端内容",
        string colorStatus = "yellow")
    {
        return new NoteDto
        {
            Id = id, Title = title, Content = content, ColorStatus = colorStatus,
            SourceType = "assigned", CreatorId = "u1", OwnerIds = "u2",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task B1_UpsertAsync_NoteExistsInSQLiteImmediately()
    {
        var note = CreateTestNote("b1-1", "B1测试", "即时写入");
        await _repo.UpsertAsync(note);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b1-1");
        Assert.NotNull(found);
        Assert.Equal("B1测试", found.Title);
        Assert.Equal("即时写入", found.Content);
        Assert.Equal((int)SyncOperationType.None, found.SyncOperationType);
    }

    [Fact]
    public async Task B1_UpdateContentAsync_SQLiteMatchesMemory()
    {
        var note = CreateTestNote("b1-2");
        await _repo.UpsertAsync(note);

        await _repo.UpdateContentAsync("b1-2", "新标题", "新内容", "red", "self", (int)SyncOperationType.Updated);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b1-2");
        Assert.NotNull(found);
        Assert.Equal("新标题", found.Title);
        Assert.Equal("新内容", found.Content);
        Assert.Equal("red", found.ColorStatus);
        Assert.Equal((int)SyncOperationType.Updated, found.SyncOperationType);
    }

    [Fact]
    public async Task B1_MarkArchivedAsync_SQLiteReflectsArchive()
    {
        var note = CreateTestNote("b1-3");
        await _repo.UpsertAsync(note);

        await _repo.MarkArchivedAsync("b1-3");

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b1-3");
        Assert.NotNull(found);
        Assert.True(found.IsArchived);
        Assert.NotNull(found.CompletedAt);
    }

    [Fact]
    public async Task B1_GetActiveNotesAsync_ExcludesArchived()
    {
        await _repo.UpsertAsync(CreateTestNote("b1-4a", "活跃任务"));
        await _repo.UpsertAsync(CreateTestNote("b1-4b", "归档任务"));
        await _repo.MarkArchivedAsync("b1-4b");

        var active = await _repo.GetActiveNotesAsync();
        Assert.Single(active);
        Assert.Equal("活跃任务", active[0].Title);
    }

    [Fact]
    public async Task B2_SyncFromServerAsync_NewNoteInserted()
    {
        var dto = CreateTestDto("b2-1", "服务端新建", "来自服务端");
        await _repo.SyncFromServerAsync(dto);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b2-1");
        Assert.NotNull(found);
        Assert.Equal("服务端新建", found.Title);
        Assert.Equal("来自服务端", found.Content);
        Assert.Equal("assigned", found.SourceType);
        Assert.Equal((int)SyncOperationType.None, found.SyncOperationType);
    }

    [Fact]
    public async Task B2_SyncFromServerAsync_ExistingNoteUpdated()
    {
        await _repo.UpsertAsync(CreateTestNote("b2-2", "旧标题", "旧内容", "yellow"));

        var dto = CreateTestDto("b2-2", "新标题", "新内容", "red");
        await _repo.SyncFromServerAsync(dto);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b2-2");
        Assert.NotNull(found);
        Assert.Equal("新标题", found.Title);
        Assert.Equal("新内容", found.Content);
        Assert.Equal("red", found.ColorStatus);
        Assert.Equal((int)SyncOperationType.None, found.SyncOperationType);
    }

    [Fact]
    public async Task B3_SchemaMigration_OldDbWithoutIsTopmost_AutoMigrates()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "ALTER TABLE LocalNotes DROP COLUMN IsTopmost";
        await cmd.ExecuteNonQueryAsync();

        await connection.CloseAsync();

        await _repo.InitializeAsync();

        await using var connection2 = new SqliteConnection($"Data Source={_dbPath}");
        await connection2.OpenAsync();

        var columns = new HashSet<string>();
        await using var cmd2 = connection2.CreateCommand();
        cmd2.CommandText = "PRAGMA table_info(LocalNotes)";
        await using var reader = await cmd2.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));

        await connection2.CloseAsync();

        Assert.Contains("IsTopmost", columns);
    }

    [Fact]
    public async Task B3_SchemaMigration_ExistingDataPreserved()
    {
        var note = CreateTestNote("b3-1", "迁移测试", "数据保留验证", "green");
        await _repo.UpsertAsync(note);

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "ALTER TABLE LocalNotes DROP COLUMN IsTopmost";
        await cmd.ExecuteNonQueryAsync();

        await connection.CloseAsync();

        await _repo.InitializeAsync();

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b3-1");
        Assert.NotNull(found);
        Assert.Equal("迁移测试", found.Title);
        Assert.Equal("数据保留验证", found.Content);
        Assert.Equal("green", found.ColorStatus);
    }

    [Fact]
    public async Task B3_SchemaMigration_DefaultIsTopmostIsTrue()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "ALTER TABLE LocalNotes DROP COLUMN IsTopmost";
        await cmd.ExecuteNonQueryAsync();

        await connection.CloseAsync();

        await _repo.InitializeAsync();

        await using var db = new AppDbContext(_dbPath);
        var allNotes = await db.LocalNotes.ToListAsync();
        foreach (var n in allNotes)
        {
            Assert.True(n.IsTopmost);
        }
    }

    [Fact]
    public async Task B4_SyncFromServerAsync_PreservesLocalPosition()
    {
        var note = CreateTestNote("b4-1", screenX: 500, screenY: 300);
        note.Width = 400;
        note.Height = 500;
        await _repo.UpsertAsync(note);

        var dto = CreateTestDto("b4-1", "更新标题", "更新内容");
        await _repo.SyncFromServerAsync(dto);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b4-1");
        Assert.NotNull(found);
        Assert.Equal("更新标题", found.Title);
        Assert.Equal(500, found.ScreenX);
        Assert.Equal(300, found.ScreenY);
        Assert.Equal(400, found.Width);
        Assert.Equal(500, found.Height);
    }

    [Fact]
    public async Task B4_SyncFromServerAsync_PreservesIsMinimized()
    {
        var note = CreateTestNote("b4-2", isMinimized: true);
        await _repo.UpsertAsync(note);

        var dto = CreateTestDto("b4-2", "更新后标题", "更新后内容");
        await _repo.SyncFromServerAsync(dto);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b4-2");
        Assert.NotNull(found);
        Assert.True(found.IsMinimized);
    }

    [Fact]
    public async Task B4_SyncFromServerAsync_PreservesIsTopmost()
    {
        var note = CreateTestNote("b4-3", isTopmost: false);
        await _repo.UpsertAsync(note);

        var dto = CreateTestDto("b4-3", "更新后标题", "更新后内容");
        await _repo.SyncFromServerAsync(dto);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b4-3");
        Assert.NotNull(found);
        Assert.False(found.IsTopmost);
    }

    [Fact]
    public async Task B5_ApiFailure_DataRetainedLocally()
    {
        var note = CreateTestNote("b5-1", "断网测试", "断网内容", "yellow", syncOperationType: 0);
        await _repo.UpsertAsync(note);

        await _repo.UpdateContentAsync("b5-1", "断网测试", "编辑后内容", "yellow", "self", (int)SyncOperationType.Updated);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b5-1");
        Assert.NotNull(found);
        Assert.Equal("编辑后内容", found.Content);
        Assert.Equal((int)SyncOperationType.Updated, found.SyncOperationType);
    }

    [Fact]
    public async Task B5_ApiFailure_DirtyNotesQueryable()
    {
        await _repo.UpsertAsync(CreateTestNote("b5-2a", "干净任务", syncOperationType: 0));
        await _repo.UpsertAsync(CreateTestNote("b5-2b", "脏任务", syncOperationType: (int)SyncOperationType.Updated));

        var dirty = await _repo.GetDirtyNotesAsync();
        Assert.Single(dirty);
        Assert.Equal("脏任务", dirty[0].Title);
    }

    [Fact]
    public async Task B5_ApiFailure_DirtyClearedAfterSync()
    {
        var note = CreateTestNote("b5-3", "同步测试", syncOperationType: (int)SyncOperationType.Updated);
        await _repo.UpsertAsync(note);

        await _repo.UpdateContentAsync("b5-3", "同步测试", "同步后内容", "yellow", "self", (int)SyncOperationType.None);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b5-3");
        Assert.NotNull(found);
        Assert.Equal((int)SyncOperationType.None, found.SyncOperationType);
    }

    [Fact]
    public async Task B1_UpdateTopmostAsync_SQLiteReflectsChange()
    {
        var note = CreateTestNote("b1-5", isTopmost: true);
        await _repo.UpsertAsync(note);

        await _repo.UpdateTopmostAsync("b1-5", false);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b1-5");
        Assert.NotNull(found);
        Assert.False(found.IsTopmost);
    }

    [Fact]
    public async Task B1_UpdatePositionAsync_SQLiteReflectsChange()
    {
        var note = CreateTestNote("b1-6");
        await _repo.UpsertAsync(note);

        await _repo.UpdatePositionAsync("b1-6", 250, 150, 350, 450);

        await using var db = new AppDbContext(_dbPath);
        var found = await db.LocalNotes.FindAsync("b1-6");
        Assert.NotNull(found);
        Assert.Equal(250, found.ScreenX);
        Assert.Equal(150, found.ScreenY);
        Assert.Equal(350, found.Width);
        Assert.Equal(450, found.Height);
    }
}
