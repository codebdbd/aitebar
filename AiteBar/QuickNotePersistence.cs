using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

namespace AiteBar;

[System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
internal interface IQuickNotePersistence
{
    string? LastConflictCopyPath { get; }
    bool HasLoadFailed => false;
    bool HasExternalChanges();
    Task<bool> HasExternalChangesAsync() => Task.FromResult(HasExternalChanges());
    void Load(FlowDocument document);
    Task SaveAsync(FlowDocument document);
    Task<string> SaveConflictCopyAsync(FlowDocument document);
    void OpenConflictCopy();
    void RevealConflictCopy() => OpenConflictCopy();
}

[System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
internal sealed class QuickNotePersistence(QuickNoteService service) : IQuickNotePersistence
{
    private readonly QuickNoteService _service = service ?? throw new ArgumentNullException(nameof(service));

    public string? LastConflictCopyPath => _service.LastConflictCopyPath;
    public bool HasLoadFailed => _service.HasLoadFailed;
    public bool HasExternalChanges() => _service.HasExternalChanges();
    public Task<bool> HasExternalChangesAsync() => _service.HasExternalChangesAsync();
    public void Load(FlowDocument document) => _service.Load(document);
    public Task SaveAsync(FlowDocument document) => _service.SaveAsync(document);
    public Task<string> SaveConflictCopyAsync(FlowDocument document) => _service.SaveConflictCopyAsync(document);
    public void OpenConflictCopy() => _service.OpenConflictCopy();
    public void RevealConflictCopy() => _service.RevealConflictCopy();
}

internal interface IQuickNoteClipboard
{
    bool TrySetText(string text);
    bool TryGetImage(out BitmapSource? image);
    bool TrySetImage(BitmapSource image);
}

internal sealed class QuickNoteClipboard : IQuickNoteClipboard
{
    public bool TrySetText(string text)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or InvalidOperationException)
            {
                if (attempt == 2)
                {
                    Logger.Log(ex);
                }
                else
                {
                    System.Threading.Thread.Sleep(40);
                }
            }
        }

        return false;
    }

    public bool TryGetImage(out BitmapSource? image)
    {
        image = null;
        try
        {
            if (!Clipboard.ContainsImage())
            {
                return false;
            }

            image = Clipboard.GetImage();
            return image != null;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or InvalidOperationException)
        {
            Logger.Log(ex);
            return false;
        }
    }

    public bool TrySetImage(BitmapSource image)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetImage(image);
                return true;
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or InvalidOperationException)
            {
                if (attempt == 2)
                {
                    Logger.Log(ex);
                }
                else
                {
                    System.Threading.Thread.Sleep(40);
                }
            }
        }

        return false;
    }
}
