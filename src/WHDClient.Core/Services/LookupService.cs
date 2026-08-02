using WHDClient.Core.Api;
using WHDClient.Core.Models;

namespace WHDClient.Core.Services;

/// <summary>
/// Fetches and caches lookup entities (statuses, priorities, request types, locations, techs).
/// Each entity lazy-loads and caches independently behind its own lock, so a page only ever
/// waits on the lookups it actually uses (e.g. the queue needs statuses, not request types).
/// </summary>
public class LookupService
{
    private readonly WhdApiClient _api;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(10);

    private List<StatusType>? _statusTypes;
    private List<PriorityType>? _priorityTypes;
    private List<RequestType>? _requestTypes;
    private List<Location>? _locations;
    private List<Tech>? _techs;
    private DateTimeOffset _statusTypesAt = DateTimeOffset.MinValue;
    private DateTimeOffset _priorityTypesAt = DateTimeOffset.MinValue;
    private DateTimeOffset _requestTypesAt = DateTimeOffset.MinValue;
    private DateTimeOffset _locationsAt = DateTimeOffset.MinValue;
    private DateTimeOffset _techsAt = DateTimeOffset.MinValue;
    // Several pages can request the same lookup concurrently — serialize each entity's refresh.
    private readonly SemaphoreSlim _statusTypesLock = new(1, 1);
    private readonly SemaphoreSlim _priorityTypesLock = new(1, 1);
    private readonly SemaphoreSlim _requestTypesLock = new(1, 1);
    private readonly SemaphoreSlim _locationsLock = new(1, 1);
    private readonly SemaphoreSlim _techsLock = new(1, 1);

    public LookupService(WhdApiClient api) => _api = api;

    private bool Stale(DateTimeOffset loadedAt) => DateTimeOffset.UtcNow - loadedAt > _cacheLifetime;

    /// <summary>
    /// Warms the cheap lookups (statuses, priorities, locations, techs) in parallel — e.g. in the
    /// background after sign-in. Request types are deliberately excluded: that list is slow
    /// (~3s) and only the Search/New Ticket pages need it, so it loads on demand instead.
    /// </summary>
    public async Task EnsureLoadedAsync(bool force = false, CancellationToken ct = default)
    {
        await Task.WhenAll(
            EnsureStatusTypesAsync(force, ct),
            EnsurePriorityTypesAsync(force, ct),
            EnsureLocationsAsync(force, ct),
            EnsureTechsAsync(force, ct));
    }

    private async Task EnsureStatusTypesAsync(bool force, CancellationToken ct)
    {
        await _statusTypesLock.WaitAsync(ct);
        try
        {
            if (!force && _statusTypes != null && !Stale(_statusTypesAt)) return;
            _statusTypes = await _api.GetListAsync<StatusType>("/StatusTypes", null, ct);
            _statusTypesAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _statusTypesLock.Release();
        }
    }

    private async Task EnsurePriorityTypesAsync(bool force, CancellationToken ct)
    {
        await _priorityTypesLock.WaitAsync(ct);
        try
        {
            if (!force && _priorityTypes != null && !Stale(_priorityTypesAt)) return;
            _priorityTypes = await _api.GetListAsync<PriorityType>("/PriorityTypes", null, ct);
            _priorityTypesAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _priorityTypesLock.Release();
        }
    }

    private async Task EnsureRequestTypesAsync(bool force, CancellationToken ct)
    {
        await _requestTypesLock.WaitAsync(ct);
        try
        {
            if (!force && _requestTypes != null && !Stale(_requestTypesAt)) return;
            var requestTypes = await _api.GetListAsync<RequestType>("/RequestTypes",
                new Dictionary<string, string?> { ["list"] = "all", ["limit"] = "1000", ["style"] = "details" }, ct);
            await BackfillRequestTypeStubsAsync(requestTypes, ct);
            _requestTypes = requestTypes;
            _requestTypesAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _requestTypesLock.Release();
        }
    }

    private async Task EnsureLocationsAsync(bool force, CancellationToken ct)
    {
        await _locationsLock.WaitAsync(ct);
        try
        {
            if (!force && _locations != null && !Stale(_locationsAt)) return;
            _locations = await _api.GetListAsync<Location>("/Locations",
                new Dictionary<string, string?> { ["limit"] = "500" }, ct);
            _locationsAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _locationsLock.Release();
        }
    }

    private async Task EnsureTechsAsync(bool force, CancellationToken ct)
    {
        await _techsLock.WaitAsync(ct);
        try
        {
            if (!force && _techs != null && !Stale(_techsAt)) return;
            var techs = await _api.GetListAsync<Tech>("/Techs",
                new Dictionary<string, string?> { ["limit"] = "500", ["style"] = "details" }, ct);
            await BackfillTechStubsAsync(techs, ct);
            _techs = techs;
            _techsAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _techsLock.Release();
        }
    }

    // The RequestTypes list endpoint intermittently returns stub records ({id, type} only) for some
    // types — those must be re-fetched individually to get their name, parentId and archived flag.
    // Fetched in parallel: a serial round-trip per stub added seconds to cold loads.
    private async Task BackfillRequestTypeStubsAsync(List<RequestType> requestTypes, CancellationToken ct)
    {
        var stubs = requestTypes.Where(r => r.ProblemTypeName == null && r.DetailDisplayName == null).ToList();
        await Task.WhenAll(stubs.Select(async stub =>
        {
            var full = await _api.GetAsync<RequestType>($"/RequestTypes/{stub.Id}",
                new Dictionary<string, string?> { ["style"] = "details" }, ct);
            if (full == null) return;
            requestTypes[requestTypes.IndexOf(stub)] = full;
        }));
    }

    public async Task<IReadOnlyList<StatusType>> GetStatusTypesAsync(CancellationToken ct = default)
    {
        await EnsureStatusTypesAsync(false, ct);
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
        await EnsurePriorityTypesAsync(false, ct);
        return _priorityTypes!;
    }

    /// <summary>All request types, including archived ones (needed to render old tickets and rebuild hierarchy paths).</summary>
    public async Task<IReadOnlyList<RequestType>> GetRequestTypesAsync(CancellationToken ct = default)
    {
        await EnsureRequestTypesAsync(false, ct);
        return _requestTypes!;
    }

    /// <summary>Only request types that may be selected for new tickets (not archived, not deleted).</summary>
    public async Task<IReadOnlyList<RequestType>> GetSelectableRequestTypesAsync(CancellationToken ct = default)
        => (await GetRequestTypesAsync(ct)).Where(r => r.IsSelectable).ToList();

    public async Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken ct = default)
    {
        await EnsureLocationsAsync(false, ct);
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
        await EnsureTechsAsync(false, ct);
        return _techs!;
    }

    // The Techs list endpoint can return stub records ({id, type} only) for some techs —
    // re-fetch those individually (in parallel) to get their name and inactive flag.
    private async Task BackfillTechStubsAsync(List<Tech> techs, CancellationToken ct)
    {
        var stubs = techs.Where(t => t.FirstName == null && t.LastName == null && t.ServerDisplayName == null).ToList();
        await Task.WhenAll(stubs.Select(async stub =>
        {
            var full = await _api.GetAsync<Tech>($"/Techs/{stub.Id}",
                new Dictionary<string, string?> { ["style"] = "details" }, ct);
            if (full == null) return;
            techs[techs.IndexOf(stub)] = full;
        }));
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
