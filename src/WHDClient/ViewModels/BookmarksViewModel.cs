using CommunityToolkit.Mvvm.Input;
using WHDClient.Core.Models;
using WHDClient.Services;

namespace WHDClient.ViewModels;

/// <summary>Lists the tickets the user has bookmarked (ids stored in app settings).</summary>
public partial class BookmarksViewModel : TicketListViewModelBase
{
    public override bool IsClosable => true;

    public BookmarksViewModel(WhdSessionContext session, SettingsService settings, Action<int> openTicket)
        : base(session, settings, openTicket)
    {
        Header = "Bookmarks";
        IconSource = "pack://application:,,,/Assets/icons/bookmark.png";
    }

    protected override async Task<List<Ticket>> FetchAsync(int page, CancellationToken ct)
    {
        var result = new List<Ticket>();
        var pageIds = Settings.Settings.BookmarkedTicketIds.Skip((page - 1) * PageSize).Take(PageSize);
        foreach (var id in pageIds)
        {
            ct.ThrowIfCancellationRequested();
            var ticket = await Session.Tickets.GetTicketAsync(id, ct: ct);
            if (ticket != null) result.Add(ticket);
        }
        return result;
    }

    [RelayCommand]
    private void RemoveBookmark(TicketRow? row)
    {
        if (row == null) return;
        Settings.Settings.BookmarkedTicketIds.Remove(row.Id);
        Settings.Save();
        Tickets.Remove(row);
    }
}
