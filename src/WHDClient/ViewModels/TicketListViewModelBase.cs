using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WHDClient.Core.Models;
using WHDClient.Core.Services;
using WHDClient.Services;

namespace WHDClient.ViewModels;

/// <summary>Flattened ticket row for list grids.</summary>
public partial class TicketRow : ObservableObject
{
    public int Id { get; init; }
    public string Subject { get; init; } = "";
    public string Client { get; init; } = "";
    public string Status { get; init; } = "";
    public string Priority { get; init; } = "";
    public string Location { get; init; } = "";
    public string Tech { get; init; } = "";
    public string LastUpdated { get; init; } = "";
    public DateTimeOffset? LastUpdatedStamp { get; init; }
    public string ReportDate { get; init; } = "";
    public DateTimeOffset? ReportDateStamp { get; init; }

    [ObservableProperty]
    private bool _recentlyChanged;

    public static TicketRow From(Ticket t) => new()
    {
        Id = t.Id,
        Subject = t.DisplaySubject,
        Client = t.DisplayClient ?? t.ClientReporter?.DisplayName ?? "",
        Status = t.StatusType?.DisplayName ?? "",
        Priority = t.PriorityType?.DisplayName ?? "",
        Location = t.Location?.DisplayName ?? "",
        Tech = t.ClientTech?.DisplayName ?? "",
        LastUpdatedStamp = t.EffectiveLastUpdated,
        LastUpdated = t.EffectiveLastUpdated?.ToLocalTime().ToString("yy-MM-dd HH:mm") ?? t.PrettyLastUpdated ?? "",
        ReportDateStamp = t.ReportDateUtc,
        ReportDate = t.ReportDateUtc?.ToLocalTime().ToString("yy-MM-dd HH:mm") ?? ""
    };
}

/// <summary>Shared base for ticket-list pages (auto-refresh, paging, open-on-double-click).</summary>
public abstract partial class TicketListViewModelBase : TabViewModelBase
{
    /// <summary>Tickets per page — live from settings (25/50/100), applies on the next refresh.</summary>
    protected int PageSize => Settings.Settings.PageSize;

    protected readonly WhdSessionContext Session;
    protected readonly SettingsService Settings;
    private readonly Action<int> _openTicket;
    private CancellationTokenSource? _refreshCts;

    public ObservableCollection<TicketRow> Tickets { get; } = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _lastRefreshText = "never";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(PageText))]
    private int _page = 1;

    [ObservableProperty]
    private bool _hasNextPage;

    public bool CanGoPrevious => Page > 1;
    public string PageText => $"Page {Page}";

    protected TicketListViewModelBase(WhdSessionContext session, SettingsService settings, Action<int> openTicket)
    {
        Session = session;
        Settings = settings;
        _openTicket = openTicket;
    }

    protected abstract Task<List<Ticket>> FetchAsync(int page, CancellationToken ct);

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy || !Session.IsSignedIn) return;
        IsBusy = true;
        ErrorMessage = null;
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        try
        {
            var tickets = await FetchAsync(Page, _refreshCts.Token);
            HasNextPage = tickets.Count >= PageSize;
            var knownStamps = Tickets.ToDictionary(r => r.Id, r => r.LastUpdatedStamp);
            Tickets.Clear();
            // Default order: newest reported first (the grids show the Reported header arrow to match).
            foreach (var t in tickets.OrderByDescending(t => t.ReportDateUtc ?? DateTimeOffset.MinValue))
            {
                var row = TicketRow.From(t);
                row.RecentlyChanged = knownStamps.TryGetValue(row.Id, out var old)
                                      && row.LastUpdatedStamp.HasValue && old.HasValue
                                      && row.LastUpdatedStamp > old;
                Tickets.Add(row);
            }
            LastRefreshText = DateTime.Now.ToString("HH:mm:ss");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!HasNextPage || IsBusy) return;
        Page++;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page <= 1 || IsBusy) return;
        Page--;
        await RefreshAsync();
    }

    [RelayCommand]
    private void OpenTicket(TicketRow? row)
    {
        if (row != null) _openTicket(row.Id);
    }
}
