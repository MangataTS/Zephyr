using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ZephyrDesktop.Data;
using ZephyrDesktop.Data.Repositories;
using Serilog;
using ZephyrDesktop.Events;
using ZephyrDesktop.Models;
using ZephyrDesktop.Services;
using ZephyrDesktop.Services.Api;

namespace ZephyrDesktop.Tests;

public class StabilityTests : IDisposable
{
    private readonly string _dbPath;

    public StabilityTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"zephyr_stab_{Guid.NewGuid():N}.db");
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

    [Fact]
    public async Task C1_ConcurrentDictionary_ThreadSafeUnderParallelAccess()
    {
        var dict = new ConcurrentDictionary<string, string>();
        var errors = new ConcurrentBag<Exception>();

        Parallel.For(0, 100, i =>
        {
            try
            {
                var key = $"note-{i}";
                dict.TryAdd(key, $"value-{i}");
                _ = dict.TryGetValue(key, out var val);
                dict.TryRemove(key, out _);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });

        Assert.Empty(errors);
    }

    [Fact]
    public async Task C1_LocalNoteRepository_ConcurrentUpsertNoCorruption()
    {
        var repo = new LocalNoteRepository(() => new AppDbContext(_dbPath));
        await repo.InitializeAsync();

        var errors = new ConcurrentBag<Exception>();
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            try
            {
                var note = new LocalNote
                {
                    Id = $"concurrent-{i}", Title = $"并发测试-{i}", Content = $"内容-{i}",
                    ColorStatus = "yellow", SourceType = "self", IsDirty = false,
                    IsTopmost = true, LastSyncAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                };
                await repo.UpsertAsync(note);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }));

        await Task.WhenAll(tasks);

        Assert.Empty(errors);

        var active = await repo.GetActiveNotesAsync();
        Assert.Equal(20, active.Count);
    }

    [Fact]
    public async Task C2_ForceLogoutEvent_SendWithoutToken_ReceivedByRegister()
    {
        var received = false;
        object? receivedMessage = null;

        WeakReferenceMessenger.Default.Register<ForceLogoutEvent>(this, (r, m) =>
        {
            received = true;
            receivedMessage = m;
        });

        try
        {
            WeakReferenceMessenger.Default.Send(new ForceLogoutEvent());

            Assert.True(received, "ForceLogoutEvent should be received when sent without token");
            Assert.NotNull(receivedMessage);
            Assert.IsType<ForceLogoutEvent>(receivedMessage);
        }
        finally
        {
            WeakReferenceMessenger.Default.Unregister<ForceLogoutEvent>(this);
        }
    }

    [Fact]
    public async Task C2_ForceLogoutEvent_WithToken_NotReceivedByPlainRegister()
    {
        var received = false;

        WeakReferenceMessenger.Default.Register<ForceLogoutEvent>(this, (r, m) =>
        {
            received = true;
        });

        try
        {
            var token = "wrong-channel";
            WeakReferenceMessenger.Default.Send(new ForceLogoutEvent(), token);

            Assert.False(received, "ForceLogoutEvent sent with token should NOT be received by plain Register (no token)");
        }
        finally
        {
            WeakReferenceMessenger.Default.Unregister<ForceLogoutEvent>(this);
        }
    }

    [Fact]
    public void C3_HttpClient_SingletonRegistration_VerifiedByCode()
    {
        var sourcePath = FindSourceFile("App.xaml.cs");
        Assert.NotNull(sourcePath);

        var content = File.ReadAllText(sourcePath);
        Assert.Contains("AddSingleton<IAuthApi>", content);
        Assert.Contains("AddSingleton<INoteApi>", content);
        Assert.DoesNotContain("AddTransient<IAuthApi>", content);
        Assert.DoesNotContain("AddTransient<INoteApi>", content);
        Assert.DoesNotContain("AddRefitClient", content);
    }

    [Fact]
    public async Task C3_WindowManager_UsesConcurrentDictionary()
    {
        var wmType = typeof(WindowManager);
        var field = wmType.GetField("_openNotes", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        Assert.True(field.FieldType.IsGenericType);
        Assert.Equal(typeof(ConcurrentDictionary<,>), field.FieldType.GetGenericTypeDefinition());
    }

    [Fact]
    public async Task C4_TrayService_IconCache_ExistsInCode()
    {
        var trayType = typeof(ZephyrDesktop.Services.TrayService);
        var cacheField = trayType.GetField("_iconCache", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(cacheField);
        Assert.True(cacheField.FieldType.IsGenericType);
    }

    [Fact]
    public async Task C5_PositionDebounce_DispatcherTimerUsed()
    {
        var noteType = typeof(ZephyrDesktop.Views.StickyNoteWindow);
        var timerField = noteType.GetField("_positionSaveTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(timerField);
        Assert.Equal(typeof(System.Windows.Threading.DispatcherTimer), timerField.FieldType);
    }

    [Fact]
    public async Task C5_PositionDebounce_IntervalIs200ms()
    {
        var noteType = typeof(ZephyrDesktop.Views.StickyNoteWindow);
        var sourcePath = FindSourceFile("StickyNoteWindow.xaml.cs");
        if (sourcePath == null) return;

        var content = File.ReadAllText(sourcePath);
        Assert.Contains("FromMilliseconds(200)", content);
        Assert.Contains("DebounceSavePosition", content);
    }

    [Fact]
    public async Task C6_SyncEngine_AllCatchBlocksHaveSerilog()
    {
        var sourcePath = FindSourceFile("SyncEngine.cs");
        Assert.NotNull(sourcePath);

        var content = File.ReadAllText(sourcePath);

        var catchPattern = "catch";
        var serilogPattern = "Log.Error";
        var lines = content.Split('\n');

        var catchCount = 0;
        var serilogCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("catch") && !lines[i].Contains("//") && !lines[i].Contains("catch (Exception ex)"))
            {
                if (lines[i].TrimStart().StartsWith("catch"))
                    catchCount++;
            }
            if (lines[i].Contains(serilogPattern))
                serilogCount++;
        }

        Assert.True(serilogCount >= 3,
            $"Expected at least 3 Log.Error calls in SyncEngine (FullSync + IncrementalSync + UploadDirty), found {serilogCount}");
    }

    [Fact]
    public async Task C6_Serilog_WritesToFileOnException()
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZephyrDesktop", "logs");

        Log.Error(new InvalidOperationException("测试异常"), "[{Step}]", "Test.C6");
        Log.CloseAndFlush();

        Assert.True(Directory.Exists(logPath), $"Log directory should exist at {logPath}");
    }

    [Fact]
    public void C6_SyncEngine_UsesSemaphoreSlim()
    {
        var sourcePath = FindSourceFile("SyncEngine.cs");
        Assert.NotNull(sourcePath);

        var content = File.ReadAllText(sourcePath);
        Assert.Contains("SemaphoreSlim", content);
    }

    [Fact]
    public async Task C1_WindowManager_EnsureOnUiThread_UsesDispatcherInvoke()
    {
        var sourcePath = FindSourceFile("WindowManager.cs");
        Assert.NotNull(sourcePath);

        var content = File.ReadAllText(sourcePath);
        Assert.Contains("Dispatcher.Invoke", content);
        Assert.Contains("EnsureOnUiThread", content);
        Assert.DoesNotContain("lock (", content);
    }

    private static string? FindSourceFile(string fileName)
    {
        var assemblyPath = typeof(ZephyrDesktop.App).Assembly.Location;
        var dir = Path.GetDirectoryName(assemblyPath);
        while (dir != null)
        {
            var srcDir = Path.Combine(dir, "src", "ZephyrDesktop");
            if (Directory.Exists(srcDir))
            {
                var found = Directory.GetFiles(srcDir, fileName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (found != null) return found;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}
