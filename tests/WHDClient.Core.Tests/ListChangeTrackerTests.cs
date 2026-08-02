using WHDClient.Core.ChangeDetection;
using WHDClient.Core.Models;
using Xunit;

namespace WHDClient.Core.Tests;

public class ListChangeTrackerTests
{
    private static Ticket T(int id, string updated) => new()
    {
        Id = id,
        Subject = $"Ticket {id}",
        LastUpdatedUtc = DateTimeOffset.Parse(updated)
    };

    [Fact]
    public void FirstSnapshot_IsBaseline_NoEvents()
    {
        var tracker = new ListChangeTracker();
        var changes = tracker.ProcessSnapshot(new[] { T(1, "2026-01-01T00:00:00Z") }, TicketChangeKind.AssignedToMe);
        Assert.Empty(changes);
    }

    [Fact]
    public void NewTicketInMineList_RaisesAssignedToMe()
    {
        var tracker = new ListChangeTracker();
        tracker.ProcessSnapshot(new[] { T(1, "2026-01-01T00:00:00Z") }, TicketChangeKind.AssignedToMe);
        var changes = tracker.ProcessSnapshot(
            new[] { T(1, "2026-01-01T00:00:00Z"), T(2, "2026-01-02T00:00:00Z") },
            TicketChangeKind.AssignedToMe);
        var c = Assert.Single(changes);
        Assert.Equal(TicketChangeKind.AssignedToMe, c.Kind);
        Assert.Equal(2, c.TicketId);
    }

    [Fact]
    public void UpdatedTicket_RaisesMyTicketUpdated()
    {
        var tracker = new ListChangeTracker();
        tracker.ProcessSnapshot(new[] { T(1, "2026-01-01T00:00:00Z") }, TicketChangeKind.AssignedToMe);
        var changes = tracker.ProcessSnapshot(new[] { T(1, "2026-01-01T01:00:00Z") }, TicketChangeKind.AssignedToMe);
        var c = Assert.Single(changes);
        Assert.Equal(TicketChangeKind.MyTicketUpdated, c.Kind);
        Assert.Equal(1, c.TicketId);
    }

    [Fact]
    public void UnchangedSnapshot_NoEvents()
    {
        var tracker = new ListChangeTracker();
        tracker.ProcessSnapshot(new[] { T(1, "2026-01-01T00:00:00Z") }, TicketChangeKind.AssignedToMe);
        var changes = tracker.ProcessSnapshot(new[] { T(1, "2026-01-01T00:00:00Z") }, TicketChangeKind.AssignedToMe);
        Assert.Empty(changes);
    }

    [Fact]
    public void WatchedFilter_UpdatedTicketDoesNotRaiseUpdate()
    {
        var tracker = new ListChangeTracker();
        tracker.ProcessSnapshot(new[] { T(1, "2026-01-01T00:00:00Z") }, TicketChangeKind.NewMatchingTicket, "filter");
        var changes = tracker.ProcessSnapshot(new[] { T(1, "2026-01-01T01:00:00Z") }, TicketChangeKind.NewMatchingTicket, "filter");
        Assert.Empty(changes);
    }

    [Fact]
    public void Reset_RequiresNewBaseline()
    {
        var tracker = new ListChangeTracker();
        tracker.ProcessSnapshot(new[] { T(1, "2026-01-01T00:00:00Z") }, TicketChangeKind.AssignedToMe);
        tracker.Reset();
        var changes = tracker.ProcessSnapshot(new[] { T(1, "2026-01-01T00:00:00Z") }, TicketChangeKind.AssignedToMe);
        Assert.Empty(changes);
    }
}
