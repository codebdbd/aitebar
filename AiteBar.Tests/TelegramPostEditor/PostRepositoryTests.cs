using System;
using System.IO;
using System.Linq;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar.Tests.TelegramPostEditor;

public sealed class PostRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PostDbMigrator _migrator;
    private readonly PostRepository _repository;
    private readonly PostMediaStore _mediaStore;

    public PostRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aitebar_tg_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _migrator = new PostDbMigrator(Path.Combine(_tempDir, "posts.db"));
        _repository = new PostRepository(_migrator);
        _mediaStore = new PostMediaStore(_migrator, Path.Combine(_tempDir, "Media"));
    }

    [Fact]
    public void SaveSearchRevisionAndRestore_WorkAcrossRestarts()
    {
        var post = new PostDocument
        {
            Title = "Первый пост",
            PublishTitle = "Заголовок",
            IncludePublishTitle = true,
            Tags = ["news", "release"],
            Blocks =
            [
                new PostBlock
                {
                    Type = PostBlockType.Paragraph,
                    Runs = [new PostTextRun { Text = "Привет, ", Formats = PostTextFormats.Bold }, new PostTextRun { Text = "мир!", Formats = PostTextFormats.Spoiler }]
                }
            ]
        };

        _repository.SavePost(post, createRevision: true);
        Assert.Equal(1, post.Revision);

        post.Blocks[0].Runs[0].Text = "Обновлённый ";
        _repository.SavePost(post, createRevision: true);
        Assert.Equal(2, post.Revision);

        IReadOnlyList<PostRevision> revisions = _repository.GetRevisions(post.Id);
        Assert.Equal(2, revisions.Count);

        IReadOnlyList<PostDocument> foundByTag = _repository.SearchPosts(new PostSearchFilter { Tag = "release" });
        Assert.Single(foundByTag);

        IReadOnlyList<PostDocument> foundByText = _repository.SearchPosts(new PostSearchFilter { SearchText = "Обновлённый" });
        Assert.Single(foundByText);

        PostDocument restored = _repository.RestoreRevision(post.Id, 1);
        Assert.Equal(3, restored.Revision);
        Assert.Equal("Привет, ", restored.Blocks[0].Runs[0].Text);

        Assert.True(_repository.SoftDeletePost(post.Id));
        Assert.Empty(_repository.SearchPosts(new PostSearchFilter { IncludeDeleted = false }));
        Assert.Single(_repository.SearchPosts(new PostSearchFilter { IncludeDeleted = true }));

        Assert.True(_repository.RestorePost(post.Id));
        Assert.Single(_repository.SearchPosts());
    }

    [Fact]
    public void BuiltInTemplates_CreateIndependentDrafts()
    {
        _repository.EnsureBuiltInTemplates();
        IReadOnlyList<PostTemplate> templates = _repository.GetTemplates();
        Assert.True(templates.Count >= 4);

        PostDocument draft = _repository.CreateDraftFromTemplate(templates[0].Id);
        Assert.NotEqual(templates[0].Document.Id, draft.Id);
        Assert.Equal(WorkflowStatus.DRAFT, draft.WorkflowStatus);

        string exported = _repository.ExportTemplateToJson(templates[0]);
        PostTemplate imported = _repository.ImportTemplateFromJson(exported);
        Assert.False(imported.IsBuiltIn);
        Assert.NotEqual(templates[0].Id, imported.Id);
    }

    [Fact]
    public void MediaStore_DeduplicatesBySha256_AndCleansUnusedOnly()
    {
        string source = Path.Combine(_tempDir, "sample.png");
        File.WriteAllBytes(source, [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4]);

        PostMediaItem first = _mediaStore.ImportFile(source);
        PostMediaItem second = _mediaStore.ImportFile(source);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(MediaKind.Photo, first.Kind);

        var post = new PostDocument
        {
            Title = "With Media",
            Media = [new PostMediaAttachment { MediaId = first.Id, SortOrder = 0 }]
        };
        _repository.SavePost(post);

        Assert.Equal(0, _mediaStore.CleanupUnusedMedia());
        Assert.NotNull(_mediaStore.GetById(first.Id));

        _repository.PermanentlyDeletePost(post.Id);
        Assert.Equal(1, _mediaStore.CleanupUnusedMedia());
        Assert.Null(_mediaStore.GetById(first.Id));
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore temp cleanup issues on Windows.
        }
    }
}
