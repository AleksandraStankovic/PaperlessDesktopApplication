using PaperlessDesktop.Models;

namespace PaperlessDesktop.Services;

public class LookupCache
{
    private readonly PaperlessApiClient _api;

    private Dictionary<int, Tag> _tags = new();
    private Dictionary<int, Correspondent> _correspondents = new();
    private Dictionary<int, DocumentType> _documentTypes = new();

    public bool IsLoaded { get; private set; }

    public IReadOnlyCollection<Tag> Tags => _tags.Values;
    public IReadOnlyCollection<Correspondent> Correspondents => _correspondents.Values;
    public IReadOnlyCollection<DocumentType> DocumentTypes => _documentTypes.Values;

    public LookupCache(PaperlessApiClient api)
    {
        _api = api;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var tagsTask = _api.GetAllTagsAsync(ct);
        var correspondentsTask = _api.GetAllCorrespondentsAsync(ct);
        var typesTask = _api.GetAllDocumentTypesAsync(ct);
        await Task.WhenAll(tagsTask, correspondentsTask, typesTask);

        _tags = (await tagsTask).ToDictionary(t => t.Id);
        _correspondents = (await correspondentsTask).ToDictionary(c => c.Id);
        _documentTypes = (await typesTask).ToDictionary(t => t.Id);
        IsLoaded = true;
    }

    public Tag? GetTag(int id) => _tags.GetValueOrDefault(id);

    public void AddOrUpdateTag(Tag tag) => _tags[tag.Id] = tag;

    public void AdjustTagCount(int id, int delta)
    {
        if (_tags.TryGetValue(id, out var tag))
            tag.DocumentCount = Math.Max(0, tag.DocumentCount + delta);
    }

    public string? GetCorrespondentName(int? id)
        => id is int key && _correspondents.TryGetValue(key, out var c) ? c.Name : null;

    public string? GetDocumentTypeName(int? id)
        => id is int key && _documentTypes.TryGetValue(key, out var t) ? t.Name : null;
}