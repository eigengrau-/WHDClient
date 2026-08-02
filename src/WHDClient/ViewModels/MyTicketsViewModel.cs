using System.Windows.Threading;
using WHDClient.Core.Models;
using WHDClient.Core.Services;
using WHDClient.Services;

namespace WHDClient.ViewModels;

public class MyTicketsViewModel : TicketListViewModelBase
{
    private readonly DispatcherTimer _timer;

    public MyTicketsViewModel(WhdSessionContext session, SettingsService settings, Action<int> openTicket)
        : base(session, settings, openTicket)
    {
        Header = "My Tickets";
        IconSource = "pack://application:,,,/Assets/icons/my-tickets.png";
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(settings.Settings.PollIntervalSeconds) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    protected override Task<List<Ticket>> FetchAsync(int page, CancellationToken ct)
        => Session.Tickets.GetTicketsAsync(TicketListKind.Mine, page: page, limit: PageSize, ct: ct);

    protected override Task<int> CountAsync(CancellationToken ct)
        => Session.Tickets.CountTicketsAsync(TicketListKind.Mine, ct: ct);
}
