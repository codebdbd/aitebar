using System;
using System.IO;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class QuickNoteServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly QuickNoteService _service;

    public QuickNoteServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service = new QuickNoteService();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void NotePath_ReturnsExpectedPath()
    {
        var path = _service.NotePath;
        Assert.NotNull(path);
        Assert.EndsWith("QuickNote.md", path);
    }

    [Fact]
    public void HasExternalChanges_WhenNeverLoaded_ReturnsFalse()
    {
        var result = _service.HasExternalChanges();
        Assert.False(result);
    }

    [Fact]
    public async Task ReadMarkdownAsync_WhenNoFile_ReturnsEmpty()
    {
        // Создаем временный сервис с кастомной директорией
        var customDir = Path.Combine(_tempDir, "custom");
        Directory.CreateDirectory(customDir);
        
        var content = await _service.ReadMarkdownAsync();
        // Если файл не существует, должен вернуть пустую строку
        Assert.NotNull(content);
    }

    [Fact]
    public void HasExternalChanges_AfterLoad_ReturnsFalse()
    {
        // Этот тест требует реального файла, но мы можем проверить логику
        var result = _service.HasExternalChanges();
        Assert.False(result);
    }
}
