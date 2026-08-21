using System;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace AiteBar;

[System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
internal interface IQuickNotePersistence
{
    string? LastConflictCopyPath { get; }
    bool HasExternalChanges();
    void Load(FlowDocument document);
    Task SaveAsync(FlowDocument document);
    Task<string> SaveConflictCopyAsync(FlowDocument document);
    void OpenInEditor();
    void OpenConflictCopy();
}

[System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
internal sealed class QuickNotePersistence(QuickNoteService service) : IQuickNotePersistence
{
    private readonly QuickNoteService _service = service ?? throw new ArgumentNullException(nameof(service));

    public string? LastConflictCopyPath => _service.LastConflictCopyPath;
    public bool HasExternalChanges() => _service.HasExternalChanges();
    public void Load(FlowDocument document) => _service.Load(document);
    public Task SaveAsync(FlowDocument document) => _service.SaveAsync(document);
    public Task<string> SaveConflictCopyAsync(FlowDocument document) => _service.SaveConflictCopyAsync(document);
    public void OpenInEditor() => _service.OpenInEditor();
    public void OpenConflictCopy() => _service.OpenConflictCopy();
}
