using Microsoft.EntityFrameworkCore;
using Serilog;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Data.Repositories;

public sealed class LocalNoteRepository
{
    private readonly Func<AppDbContext> _dbFactory;

    public LocalNoteRepository(Func<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<LocalNote>> GetActiveNotesAsync()
    {
        await using var db = _dbFactory();
        return await db.LocalNotes
            .Where(n => !n.IsArchived)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<LocalNote>> GetAllNotesAsync()
    {
        await using var db = _dbFactory();
        return await db.LocalNotes
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();
    }

    public async Task<LocalNote?> GetByIdAsync(string id)
    {
        await using var db = _dbFactory();
        return await db.LocalNotes.FindAsync(id);
    }

    public async Task UpsertAsync(LocalNote note)
    {
        await using var db = _dbFactory();
        var existing = await db.LocalNotes.FindAsync(note.Id);
        if (existing != null)
        {
            db.Entry(existing).CurrentValues.SetValues(note);
        }
        else
        {
            await db.LocalNotes.AddAsync(note);
        }
        await db.SaveChangesAsync();
    }

    public async Task UpdatePositionAsync(string id, double x, double y, double width, double height)
    {
        await using var db = _dbFactory();
        var note = await db.LocalNotes.FindAsync(id);
        if (note == null) return;
        note.ScreenX = x;
        note.ScreenY = y;
        note.Width = width;
        note.Height = height;
        await db.SaveChangesAsync();
    }

    public async Task UpdateTopmostAsync(string id, bool isTopmost)
    {
        await using var db = _dbFactory();
        var note = await db.LocalNotes.FindAsync(id);
        if (note == null) return;
        note.IsTopmost = isTopmost;
        await db.SaveChangesAsync();
    }

    public async Task SetConflictAsync(string noteId, bool hasConflict)
    {
        await using var db = _dbFactory();
        var note = await db.LocalNotes.FindAsync(noteId);
        if (note == null) return;
        note.HasConflict = hasConflict;
        await db.SaveChangesAsync();
    }

    public async Task SetSyncOperationTypeAsync(string noteId, int syncOperationType)
    {
        await using var db = _dbFactory();
        var note = await db.LocalNotes.FindAsync(noteId);
        if (note == null) return;
        note.SyncOperationType = syncOperationType;
        await db.SaveChangesAsync();
    }

    public async Task UpdateSubTagAsync(string noteId, string subTag)
    {
        await using var db = _dbFactory();
        var note = await db.LocalNotes.FindAsync(noteId);
        if (note == null) return;
        note.SubTag = subTag;
        note.SyncOperationType = (int)Models.SyncOperationType.Updated;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task MarkArchivedAsync(string id)
    {
        await using var db = _dbFactory();
        var note = await db.LocalNotes.FindAsync(id);
        if (note == null) return;
        note.IsArchived = true;
        note.CompletedAt = DateTime.UtcNow;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task SyncFromServerAsync(NoteDto dto)
    {
        var local = new LocalNote
        {
            Id = dto.Id,
            Title = dto.Title,
            Content = dto.Content,
            ColorStatus = dto.ColorStatus,
            SourceType = dto.SourceType,
            TagsJson = System.Text.Json.JsonSerializer.Serialize(dto.Tags.Select(t => t.Name)),
            CreatorId = dto.CreatorId,
            OwnerIds = dto.OwnerIds,
            DepartmentId = dto.DepartmentId,
            GroupId = dto.GroupId,
            IsArchived = dto.IsArchived,
            SubTag = dto.SubTag,
            TemplateType = dto.TemplateType,
            DueTime = dto.DueTime,
            CompletedAt = dto.CompletedAt,
            RemindCount = dto.RemindCount,
            LastSyncAt = DateTime.UtcNow,
            SyncOperationType = (int)Models.SyncOperationType.None,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };

        await using var db = _dbFactory();
        var existing = await db.LocalNotes.FindAsync(dto.Id);
        if (existing != null)
        {
            local.ScreenX = existing.ScreenX;
            local.ScreenY = existing.ScreenY;
            local.Width = existing.Width;
            local.Height = existing.Height;
            local.IsMinimized = existing.IsMinimized;
            local.IsTopmost = existing.IsTopmost;
            db.Entry(existing).CurrentValues.SetValues(local);
        }
        else
        {
            await db.LocalNotes.AddAsync(local);
        }
        await db.SaveChangesAsync();
    }

    public async Task ReplaceIdAsync(string oldId, string newId, NoteDto dto)
    {
        await using var db = _dbFactory();
        await using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            var old = await db.LocalNotes.FindAsync(oldId);
            if (old == null) return;

            var replacement = new LocalNote
            {
                Id = newId,
                Title = dto.Title,
                Content = dto.Content,
                ColorStatus = dto.ColorStatus,
                SourceType = dto.SourceType,
                TagsJson = System.Text.Json.JsonSerializer.Serialize(dto.Tags.Select(t => t.Name)),
                CreatorId = dto.CreatorId,
                OwnerIds = dto.OwnerIds,
                DepartmentId = dto.DepartmentId,
                GroupId = dto.GroupId,
                IsArchived = dto.IsArchived,
                SubTag = dto.SubTag,
                TemplateType = dto.TemplateType,
                DueTime = dto.DueTime,
                CompletedAt = dto.CompletedAt,
                RemindCount = dto.RemindCount,
                LastSyncAt = DateTime.UtcNow,
                SyncOperationType = (int)Models.SyncOperationType.None,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                ScreenX = old.ScreenX,
                ScreenY = old.ScreenY,
                Width = old.Width,
                Height = old.Height,
                IsMinimized = old.IsMinimized,
                IsTopmost = old.IsTopmost,
                HasConflict = old.HasConflict
            };

            db.LocalNotes.Remove(old);
            await db.LocalNotes.AddAsync(replacement);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task SyncMetadataOnlyAsync(NoteDto dto)
    {
        await using var db = _dbFactory();
        var existing = await db.LocalNotes.FindAsync(dto.Id);
        if (existing == null) return;

        existing.UpdatedAt = dto.UpdatedAt;
        existing.ColorStatus = dto.ColorStatus;
        existing.RemindCount = dto.RemindCount;
        existing.SourceType = dto.SourceType;
        existing.OwnerIds = dto.OwnerIds;
        existing.DueTime = dto.DueTime;
        existing.TagsJson = System.Text.Json.JsonSerializer.Serialize(dto.Tags.Select(t => t.Name));
        existing.CompletedAt = dto.CompletedAt;
        existing.IsArchived = dto.IsArchived;
        existing.SubTag = dto.SubTag;
        existing.LastSyncAt = DateTime.UtcNow;
        existing.SyncOperationType = (int)Models.SyncOperationType.Updated;

        await db.SaveChangesAsync();
    }

    public async Task UpdateContentAsync(string id, string title, string content, string colorStatus, string sourceType, int syncOperationType)
    {
        await using var db = _dbFactory();
        var note = await db.LocalNotes.FindAsync(id);
        if (note == null) return;
        note.Title = title;
        note.Content = content;
        note.ColorStatus = colorStatus;
        note.SourceType = sourceType;
        note.SyncOperationType = syncOperationType;
        note.UpdatedAt = DateTime.UtcNow;
        if (syncOperationType == (int)Models.SyncOperationType.None)
            note.LastSyncAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task UpdateAssigneeAsync(string id, string ownerId, string sourceType)
    {
        await using var db = _dbFactory();
        var note = await db.LocalNotes.FindAsync(id);
        if (note == null) return;
        note.OwnerIds = ownerId;
        note.SourceType = sourceType;
        note.SyncOperationType = (int)Models.SyncOperationType.Updated;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<List<LocalNote>> GetDirtyNotesAsync()
    {
        await using var db = _dbFactory();
        return await db.LocalNotes
            .Where(n => n.SyncOperationType != 0 && !n.IsArchived)
            .ToListAsync();
    }

    public async Task InitializeAsync()
    {
        await using var db = _dbFactory();
        await db.Database.EnsureCreatedAsync();
        await MigrateSchemaAsync(db);
    }

    private static async Task MigrateSchemaAsync(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        var columns = new HashSet<string>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(LocalNotes)";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }

        if (!columns.Contains("IsTopmost"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE LocalNotes ADD COLUMN IsTopmost TEXT NOT NULL DEFAULT 1";
            await cmd.ExecuteNonQueryAsync();
        }

        if (!columns.Contains("HasConflict"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE LocalNotes ADD COLUMN HasConflict INTEGER NOT NULL DEFAULT 0";
            await cmd.ExecuteNonQueryAsync();
            Log.Information("Schema migration: added HasConflict column");
        }

        if (!columns.Contains("SyncOperationType"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE LocalNotes ADD COLUMN SyncOperationType INTEGER NOT NULL DEFAULT 0";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = "UPDATE LocalNotes SET SyncOperationType = 2 WHERE IsDirty = 1";
            await cmd.ExecuteNonQueryAsync();
            Log.Information("Schema migration: added SyncOperationType column");
        }

        if (!columns.Contains("SubTag"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE LocalNotes ADD COLUMN SubTag TEXT NOT NULL DEFAULT ''";
            await cmd.ExecuteNonQueryAsync();
            Log.Information("Schema migration: added SubTag column");
        }

        if (!columns.Contains("TemplateType"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE LocalNotes ADD COLUMN TemplateType TEXT NOT NULL DEFAULT 'default'";
            await cmd.ExecuteNonQueryAsync();
            Log.Information("Schema migration: added TemplateType column");
        }

        if (!columns.Contains("IsHidden"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE LocalNotes ADD COLUMN IsHidden INTEGER NOT NULL DEFAULT 0";
            await cmd.ExecuteNonQueryAsync();
            Log.Information("Schema migration: added IsHidden column");
        }

        if (!columns.Contains("OwnerIds"))
        {
            using var cmd = connection.CreateCommand();
            if (columns.Contains("OwnerId"))
            {
                cmd.CommandText = "ALTER TABLE LocalNotes RENAME COLUMN OwnerId TO OwnerIds";
            }
            else
            {
                cmd.CommandText = "ALTER TABLE LocalNotes ADD COLUMN OwnerIds TEXT NOT NULL DEFAULT ''";
            }
            await cmd.ExecuteNonQueryAsync();
            Log.Information("Schema migration: renamed OwnerId to OwnerIds");
        }

        await connection.CloseAsync();
    }
}
