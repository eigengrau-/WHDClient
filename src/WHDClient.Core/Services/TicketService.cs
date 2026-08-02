using WHDClient.Core.Api;
using WHDClient.Core.Models;

namespace WHDClient.Core.Services;

public enum TicketListKind { Mine, Group, Flagged, Recent }

/// <summary>High-level ticket operations used by the UI and smoke tests.</summary>
public class TicketService
{
    private readonly WhdApiClient _api;

    public TicketService(WhdApiClient api) => _api = api;

    private static string ListPath(TicketListKind kind) => kind switch
    {
        TicketListKind.Mine => "/Tickets/mine",
        TicketListKind.Group => "/Tickets/group",
        TicketListKind.Flagged => "/Tickets/flagged",
        TicketListKind.Recent => "/Tickets/recent",
        _ => "/Tickets/mine"
    };

    // WHD paging is 1-based; page=0 returns HTTP 500.
    // style=details: short lists omit statustype/prioritytype/location objects.
    public Task<List<Ticket>> GetTicketsAsync(TicketListKind kind, int page = 1, int limit = 100, string? qualifier = null, CancellationToken ct = default)
    {
        var q = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["limit"] = limit.ToString(),
            ["withTUC"] = "true",
            ["style"] = "details",
            ["qualifier"] = qualifier
        };
        return _api.GetListAsync<Ticket>(ListPath(kind), q, ct);
    }

    public Task<List<Ticket>> SearchTicketsAsync(string qualifier, int page = 1, int limit = 100, CancellationToken ct = default)
    {
        var q = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["limit"] = limit.ToString(),
            ["withTUC"] = "true",
            ["style"] = "details",
            ["qualifier"] = qualifier
        };
        return _api.GetListAsync<Ticket>("/Tickets", q, ct);
    }

    public Task<Ticket?> GetTicketAsync(int id, bool details = true, CancellationToken ct = default)
    {
        var q = details ? new Dictionary<string, string?> { ["style"] = "details", ["withTUC"] = "true" } : null;
        return _api.GetAsync<Ticket>($"/Tickets/{id}", q, ct);
    }

    public Task<List<TicketNote>> GetNotesAsync(int ticketId, CancellationToken ct = default)
    {
        var q = new Dictionary<string, string?> { ["jobTicketId"] = ticketId.ToString(), ["limit"] = "200", ["style"] = "details" };
        return _api.GetListAsync<TicketNote>("/TicketNotes", q, ct);
    }

    public Task<Ticket?> CreateTicketAsync(object payload, CancellationToken ct = default)
        => _api.PostAsync<Ticket>("/Tickets", payload, null, ct);

    public Task<Ticket?> UpdateTicketAsync(int id, object payload, CancellationToken ct = default)
        => _api.PutAsync<Ticket>($"/Tickets/{id}", payload, ct);

    public Task DeleteTicketAsync(int id, CancellationToken ct = default)
        => _api.DeleteAsync($"/Tickets/{id}", null, false, null, ct);

    /// <summary>POST /ra/TechNotes — returns the created note (its id is needed for note attachments).</summary>
    public Task<TicketNote?> AddTechNoteAsync(TechNotePayload note, CancellationToken ct = default)
        => _api.PostAsync<TicketNote>("/TechNotes", note, null, ct);
}
