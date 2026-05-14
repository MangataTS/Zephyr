using System.IO;
using Microsoft.EntityFrameworkCore;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Data;

public sealed class AppDbContext : DbContext
{
    private readonly string _dbPath;

    public DbSet<LocalNote> LocalNotes => Set<LocalNote>();

    public AppDbContext(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "data", "zephyr.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dir = Path.GetDirectoryName(_dbPath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Content).HasColumnType("TEXT");
            entity.Property(e => e.TagsJson).HasColumnType("TEXT");
            entity.Property(e => e.ColorStatus).HasDefaultValue("yellow");
            entity.Property(e => e.SourceType).HasDefaultValue("self");
            entity.Property(n => n.HasConflict).HasDefaultValue(false);
            entity.Property(n => n.SyncOperationType).HasDefaultValue(0);
            entity.Property(e => e.SubTag).HasDefaultValue("");
            entity.Property(e => e.TemplateType).HasDefaultValue("default");
            entity.Property(e => e.IsHidden).HasDefaultValue(false);
            entity.HasIndex(e => e.UpdatedAt);
            entity.HasIndex(e => e.IsArchived);
        });
    }
}

public sealed class LocalNote
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string ColorStatus { get; set; } = "yellow";
    public string SourceType { get; set; } = "self";
    public string TagsJson { get; set; } = "[]";
    public string CreatorId { get; set; } = "";
    public string OwnerIds { get; set; } = "";
    public string? DepartmentId { get; set; }
    public string? GroupId { get; set; }
    public bool IsArchived { get; set; }
    public string SubTag { get; set; } = "";
    public string TemplateType { get; set; } = "default";
    public bool IsHidden { get; set; } = false;
    public DateTime? DueTime { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RemindCount { get; set; }
    public DateTime LastSyncAt { get; set; } = DateTime.UtcNow;
    public bool IsDirty { get; set; }
    public int SyncOperationType { get; set; } = 0;
    public double ScreenX { get; set; } = 100;
    public double ScreenY { get; set; } = 100;
    public double Width { get; set; } = 280;
    public double Height { get; set; } = 320;
    public bool IsMinimized { get; set; }
    public bool IsTopmost { get; set; } = true;
    public bool HasConflict { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
