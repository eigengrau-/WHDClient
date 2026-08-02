using WHDClient.Core.Models;

namespace WHDClient.Core.ChangeDetection;

public enum TicketChangeKind
{
    /// <summary>A ticket newly appeared in the "mine" list (assigned to the user).</summary>
    AssignedToMe,
    /// <summary>A ticket already in the "mine" list has a newer lastUpdated stamp.</summary>
    MyTicketUpdated,
    /// <summary>A ticket newly appeared in a watched filter/list.</summary>
    NewMatchingTicket
}

public record TicketChange(TicketChangeKind Kind, int TicketId, string Subject, DateTimeOffset? LastUpdated, string? SourceName = null);

/// <summary>
/// Pure diffing logic: compares consecutive snapshots of ticket lists and emits change events.
/// The first snapshot after start (or after sign-in) is treated as baseline and produces no events.
/// </summary>
public class ListChangeTracker
{
    private Dictionary<int, DateTimeOffset?> _known = new();
    private bool _baselineSet;

    public void Reset()
    {
        _known = new Dictionary<int, DateTimeOffset?>();
        _baselineSet = false;
    }

    /// <summary>
    /// Feed the latest snapshot of a monitored list. Returns detected changes
    /// (empty for the very first snapshot after a reset).
    /// </summary>
    public IReadOnlyList<TicketChange> ProcessSnapshot(
        IEnumerable<Ticket> tickets, TicketChangeKind newTicketKind, string? sourceName = null)
    {
        var changes = new List<TicketChange>();
        var snapshot = new Dictionary<int, DateTimeOffset?>();

        foreach (var t in tickets)
        {
            var stamp = t.EffectiveLastUpdated;
            snapshot[t.Id] = stamp;

            if (!_baselineSet) continue;

            if (!_known.TryGetValue(t.Id, out var oldStamp))
            {
                changes.Add(new TicketChange(newTicketKind, t.Id, t.DisplaySubject, stamp, sourceName));
            }
            else if (newTicketKind != TicketChangeKind.NewMatchingTicket
                     && stamp.HasValue && oldStamp.HasValue && stamp > oldStamp)
            {
                changes.Add(new TicketChange(TicketChangeKind.MyTicketUpdated, t.Id, t.DisplaySubject, stamp, sourceName));
            }
        }

        _known = snapshot;
        _baselineSet = true;
        return changes;
    }
}
