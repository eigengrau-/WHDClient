using System.Windows.Threading;
using WHDClient.Core.Models;
using WHDClient.Core.Services;
using WHDClient.Services;

namespace WHDClient.ViewModels;

public class QueueViewModel : TicketListViewModelBase
{
    private readonly DispatcherTimer _timer;
    private string? _openQualifier;

    public override bool IsClosable => true;

    public QueueViewModel(WhdSessionContext session, SettingsService settings, Action<int> openTicket)
        : base(session, settings, openTicket)
    {
        Header = "Ticket Queue";
        IconSource = "pack://application:,,,/Assets/icons/ticket-queue.png";
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(settings.Settings.PollIntervalSeconds) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    /// <summary>Auto-refresh only runs while the queue tab is open.</summary>
    public void StopAutoRefresh() => _timer.Stop();
    public void StartAutoRefresh() => _timer.Start();

    /// <summary>ALL open tickets (every status except Closed/Cancelled), not just the tech group.</summary>
    protected override async Task<List<Ticket>> FetchAsync(int page, CancellationToken ct)
    {
        _openQualifier ??= await BuildOpenQualifierAsync(ct);
        return await Session.Tickets.SearchTicketsAsync(_openQualifier, page: page, limit: PageSize, ct: ct);
    }

    private async Task<string> BuildOpenQualifierAsync(CancellationToken ct)
    {
        var closedIds = await Session.Lookups.GetClosedStatusIdsAsync(ct);
        return QualifierBuilder.And(closedIds.Select(id =>
            QualifierBuilder.Clause("statusTypeId", QualifierBuilder.Op.NotEq, id.ToString(), false)).ToArray());
    }
}
