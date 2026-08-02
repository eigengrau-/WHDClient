using WHDClient.Core.ChangeDetection;
using WHDClient.Core.Services;

namespace WHDClient.Services;

/// <summary>
/// Background poller: watches "my tickets" and alert-enabled saved filters,
/// diffs consecutive snapshots and raises change events for notifications.
/// </summary>
public class PollingService
{
    private readonly WhdSessionContext _session;
    private readonly SettingsService _settings;
    private readonly ListChangeTracker _mineTracker = new();
    private readonly Dictionary<string, ListChangeTracker> _filterTrackers = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public event EventHandler<IReadOnlyList<TicketChange>>? ChangesDetected;
    public event EventHandler<string>? PollFailed;

    public bool IsRunning => _loop is { IsCompleted: false };

    public PollingService(WhdSessionContext session, SettingsService settings)
    {
        _session = session;
        _settings = settings;
    }

    public void Start()
    {
        Stop();
        _mineTracker.Reset();
        _filterTrackers.Clear();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _loop = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        // Small initial delay so the UI can settle after sign-in.
        try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            if (_session.IsSignedIn)
                await PollOnceAsync(ct);

            var interval = TimeSpan.FromSeconds(Math.Clamp(_settings.Settings.PollIntervalSeconds, 15, 3600));
            try { await Task.Delay(interval, ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var all = new List<TicketChange>();
        try
        {
            var s = _settings.Settings;

            if (s.NotifyAssignedToMe || s.NotifyMyTicketUpdated)
            {
                var mine = await _session.Tickets.GetTicketsAsync(TicketListKind.Mine, limit: 100, ct: ct);
                all.AddRange(_mineTracker.ProcessSnapshot(mine, TicketChangeKind.AssignedToMe, "My Tickets"));
            }

            if (s.NotifyNewMatching)
            {
                foreach (var filter in s.SavedFilters.Where(f => f.AlertOnNew))
                {
                    if (string.IsNullOrWhiteSpace(filter.Qualifier)) continue;
                    if (!_filterTrackers.TryGetValue(filter.Name, out var tracker))
                    {
                        tracker = new ListChangeTracker();
                        _filterTrackers[filter.Name] = tracker;
                    }
                    var matches = await _session.Tickets.SearchTicketsAsync(filter.Qualifier, limit: 50, ct: ct);
                    all.AddRange(tracker.ProcessSnapshot(matches, TicketChangeKind.NewMatchingTicket, filter.Name));
                }
            }

            if (all.Count > 0)
                ChangesDetected?.Invoke(this, all);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            PollFailed?.Invoke(this, ex.Message);
        }
    }
}
