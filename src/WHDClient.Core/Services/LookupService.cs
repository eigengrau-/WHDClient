using WHDClient.Core.Api;
using WHDClient.Core.Models;

namespace WHDClient.Core.Services;

/// <summary>Fetches and caches lookup entities (statuses, priorities, request types, locations, techs).</summary>
public class LookupService
{
    private readonly WhdApiClient _api;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(10);

    private List<StatusType>? _statusTypes;
    private List<PriorityType>? _priorityTypes;
    private List<RequestType>? _requestTypes;
    private List<Location>? _locations;
    private List<Tech>? _techs;
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;
    // Several pages load lookups concurrently at startup — serialize cache refreshes.
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public LookupService(WhdApiClient api) => _api = api;

    private bool Stale => DateTimeOffset.UtcNow - _loadedAt > _cacheLifetime;

    public async Task EnsureLoadedAsync(bool force = false, CancellationToken ct = default)
    {
        await _loadLock.WaitAsync(ct);
        try
        {
            if (!force && !Stale && _statusTypes != null) return;
            _statusTypes = await _api.GetListAsync<StatusType>("/StatusTypes", null, ct);
            _priorityTypes = await _api.GetListAsync<PriorityType>("/PriorityTypes", null, ct);
            var requestTypes = await _api.GetListAsync<RequestType>("/RequestTypes",
                new Dictionary<string, string?> { ["list"] = "all", ["limit"] = "1000", ["style"] = "details" }, ct);
            await BackfillRequestTypeStubsAsync(requestTypes, ct);
            _requestTypes = requestTypes;
            _locations = await _api.GetListAsync<Location>("/Locations",
                new Dictionary<string, string?> { ["limit"] = "500" }, ct);
            _loadedAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    // The RequestTypes list endpoint intermittently returns stub records ({id, type} only) for some
    // types — those must be re-fetched individually to get their name, parentId and archived flag.
    private async Task BackfillRequestTypeStubsAsync(List<RequestType> requestTypes, CancellationToken ct)
    {
        foreach (var stub in requestTypes.Where(r => r.ProblemTypeName == null && r.DetailDisplayName == null).ToList())
        {
            var full = await _api.GetAsync<RequestType>($"/RequestTypes/{stub.Id}",
                new Dictionary<string, string?> { ["style"] = "details" }, ct);
            if (full == null) continue;
            requestTypes[requestTypes.IndexOf(stub)] = full;
        }
    }

    public async Task<IReadOnlyList<StatusType>> GetStatusTypesAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct: ct);
        return _statusTypes!;
    }

    /// <summary>
    /// Ids of statuses meaning a ticket is no longer open. WHD exposes no open/closed flag via REST,
    /// so the built-in status names are matched (covers renames only if they keep these names).
    /// </summary>
    public async Task<IReadOnlyList<int>> GetClosedStatusIdsAsync(CancellationToken ct = default)
        => (await GetStatusTypesAsync(ct))
            .Where(s => string.Equals(s.StatusTypeName, "Closed", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(s.StatusTypeName, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Id)
            .ToList();

    public async Task<IReadOnlyList<PriorityType>> GetPriorityTypesAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct: ct);
        return _priorityTypes!;
    }

    /// <summary>All request types, including archived ones (needed to render old tickets and rebuild hierarchy paths).</summary>
    public async Task<IReadOnlyList<RequestType>> GetRequestTypesAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct: ct);
        return _requestTypes!;
    }

    /// <summary>Only request types that may be selected for new tickets (not archived, not deleted).</summary>
    public async Task<IReadOnlyList<RequestType>> GetSelectableRequestTypesAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct: ct);
        return _requestTypes!.Where(r => r.IsSelectable).ToList();
    }

    public async Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct: ct);
        return _locations!;
    }

    public async Task<IReadOnlyList<Tech>> GetTechsAsync(string? nameFilter = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            var qualifier = QualifierBuilder.Or(
                QualifierBuilder.Clause("firstName", QualifierBuilder.Op.CaseInsensitiveLike, $"*{nameFilter}*"),
                QualifierBuilder.Clause("lastName", QualifierBuilder.Op.CaseInsensitiveLike, $"*{nameFilter}*"));
            return await _api.GetListAsync<Tech>("/Techs",
                new Dictionary<string, string?> { ["qualifier"] = qualifier, ["limit"] = "25", ["style"] = "details" }, ct);
        }
        if (_techs == null || Stale)
        {
            var techs = await _api.GetListAsync<Tech>("/Techs",
                new Dictionary<string, string?> { ["limit"] = "500", ["style"] = "details" }, ct);
            await BackfillTechStubsAsync(techs, ct);
            _techs = techs;
        }
        return _techs;
    }

    // The Techs list endpoint can return stub records ({id, type} only) for some techs —
    // re-fetch those individually to get their name and inactive flag.
    private async Task BackfillTechStubsAsync(List<Tech> techs, CancellationToken ct)
    {
        foreach (var stub in techs.Where(t => t.FirstName == null && t.LastName == null && t.ServerDisplayName == null).ToList())
        {
            var full = await _api.GetAsync<Tech>($"/Techs/{stub.Id}",
                new Dictionary<string, string?> { ["style"] = "details" }, ct);
            if (full == null) continue;
            techs[techs.IndexOf(stub)] = full;
        }
    }

    /// <summary>Active techs only, sorted by display name — for assignment dropdowns.</summary>
    public async Task<IReadOnlyList<Tech>> GetActiveTechsAsync(CancellationToken ct = default)
        => (await GetTechsAsync(ct: ct))
            .Where(t => t.IsSelectable)
            .OrderBy(t => t.ListDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public Task<List<Client>> SearchClientsAsync(string nameFragment, CancellationToken ct = default)
    {
        var qualifier = QualifierBuilder.Or(
            QualifierBuilder.Clause("firstName", QualifierBuilder.Op.CaseInsensitiveLike, $"*{nameFragment}*"),
            QualifierBuilder.Clause("lastName", QualifierBuilder.Op.CaseInsensitiveLike, $"*{nameFragment}*"),
            QualifierBuilder.Clause("email", QualifierBuilder.Op.CaseInsensitiveLike, $"*{nameFragment}*"));
        return _api.GetListAsync<Client>("/Clients",
            new Dictionary<string, string?> { ["qualifier"] = qualifier, ["limit"] = "25", ["searchLdap"] = "true" }, ct);
    }
}
