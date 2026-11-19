using System;
using System.Threading;
using System.Threading.Tasks;
using AiteBar.TelegramPostEditor.Models;

namespace AiteBar.TelegramPostEditor.Services;

public sealed class PostAutosaveService : IDisposable
{
    private readonly PostRepository _repository;
    private readonly TimeSpan _debounceDelay;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private CancellationTokenSource? _pendingCts;
    private PostDocument? _pendingDocument;
    private bool _disposed;

    public event EventHandler<PostDocument>? Saved;
    public event EventHandler<Exception>? SaveFailed;

    public PostAutosaveService(PostRepository repository, TimeSpan? debounceDelay = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(750);
    }

    public void ScheduleSave(PostDocument document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);

        _pendingCts?.Cancel();
        _pendingCts?.Dispose();
        _pendingCts = new CancellationTokenSource();
        _pendingDocument = document;

        CancellationToken token = _pendingCts.Token;
        _ = DebounceSaveAsync(document, token);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _pendingCts?.Cancel();
        _pendingCts?.Dispose();
        _pendingCts = null;

        PostDocument? doc = _pendingDocument;
        if (doc == null)
        {
            return;
        }

        await PersistAsync(doc, createRevision: true, cancellationToken).ConfigureAwait(false);
        if (_pendingCts == null && ReferenceEquals(_pendingDocument, doc))
            _pendingDocument = null;
    }

    private async Task DebounceSaveAsync(PostDocument document, CancellationToken token)
    {
        try
        {
            await Task.Delay(_debounceDelay, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                return;
            }

            await PersistAsync(document, createRevision: false, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested && ReferenceEquals(_pendingDocument, document))
            {
                _pendingDocument = null;
            }

        }
        catch (OperationCanceledException)
        {
            // Superseded by newer edit or explicit flush.
        }
        catch (Exception ex)
        {
            SaveFailed?.Invoke(this, ex);
        }
    }

    private async Task PersistAsync(PostDocument document, bool createRevision, CancellationToken cancellationToken)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PostDocument saved = _repository.SavePost(document, createRevision);
            Saved?.Invoke(this, saved);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pendingCts?.Cancel();
        _pendingCts?.Dispose();
        _saveLock.Dispose();
    }
}
