using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WHDClient.Core.Models;
using WHDClient.Core.Services;
using WHDClient.Services;

namespace WHDClient.ViewModels;

public partial class SearchViewModel : TicketListViewModelBase
{
    public override bool IsClosable => true;

    public ObservableCollection<SavedFilter> SavedFilters => new(Settings.Settings.SavedFilters);

    [ObservableProperty] private string _qualifierText = "";
    [ObservableProperty] private SavedFilter? _selectedFilter;
    [ObservableProperty] private string _filterName = "";

    // Basic-search filters (mirrors the WHD web UI basic search)
    [ObservableProperty] private string _ticketNumber = "";
    [ObservableProperty] private string _subjectContains = "";
    [ObservableProperty] private string _lastName = "";

    // Date range: which date field the range applies to (mutually exclusive radios)
    [ObservableProperty] private bool _dateOpened = true;
    [ObservableProperty] private bool _dateClosed;
    [ObservableProperty] private bool _dateUpdated;
    [ObservableProperty] private DateTime? _dateFrom;
    [ObservableProperty] private DateTime? _dateTo;

    // Lookup-backed dropdowns; a null selection means "any"
    public RequestTypePickerViewModel RequestTypePicker { get; } = new();
    public ObservableCollection<StatusType?> StatusTypes { get; } = new();
    public ObservableCollection<PriorityType?> PriorityTypes { get; } = new();
    public ObservableCollection<Location?> Locations { get; } = new();
    public ObservableCollection<Tech?> Techs { get; } = new();

    [ObservableProperty] private StatusType? _selectedStatus;
    [ObservableProperty] private PriorityType? _selectedPriority;
    [ObservableProperty] private Location? _selectedLocation;
    [ObservableProperty] private Tech? _selectedTech;

    public SearchViewModel(WhdSessionContext session, SettingsService settings, Action<int> openTicket)
        : base(session, settings, openTicket)
    {
        Header = "Search";
        IconSource = "pack://application:,,,/Assets/icons/search.png";
        _ = LoadLookupsAsync();
    }

    private async Task LoadLookupsAsync()
    {
        try
        {
            await Session.Lookups.EnsureLoadedAsync();
            StatusTypes.Add(null);
            foreach (var s in await Session.Lookups.GetStatusTypesAsync()) StatusTypes.Add(s);
            PriorityTypes.Add(null);
            foreach (var p in await Session.Lookups.GetPriorityTypesAsync()) PriorityTypes.Add(p);
            Locations.Add(null);
            foreach (var l in await Session.Lookups.GetLocationsAsync()) Locations.Add(l);
            Techs.Add(null);
            foreach (var t in await Session.Lookups.GetActiveTechsAsync()) Techs.Add(t);
            RequestTypePicker.SetRequestTypes(await Session.Lookups.GetSelectableRequestTypesAsync());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load lookups: {ex.Message}";
        }
    }

    partial void OnDateOpenedChanged(bool value) { if (value) { DateClosed = false; DateUpdated = false; } }
    partial void OnDateClosedChanged(bool value) { if (value) { DateOpened = false; DateUpdated = false; } }
    partial void OnDateUpdatedChanged(bool value) { if (value) { DateOpened = false; DateClosed = false; } }

    protected override async Task<List<Ticket>> FetchAsync(int page, CancellationToken ct)
    {
        var ticketNo = TicketNumber.Trim();
        var qualifier = EffectiveQualifier();

        if (string.IsNullOrEmpty(qualifier))
        {
            // Only a ticket number: fetch it directly instead of running a qualifier search.
            if (!int.TryParse(ticketNo, out var id)) return new List<Ticket>();
            var t = await Session.Tickets.GetTicketAsync(id, ct: ct);
            return t == null ? new List<Ticket>() : new List<Ticket> { t };
        }

        var tickets = await Session.Tickets.SearchTicketsAsync(qualifier, page: page, limit: PageSize, ct: ct);
        if (int.TryParse(ticketNo, out var tid))
            tickets = tickets.Where(t => t.Id == tid).ToList();
        return tickets;
    }

    private string EffectiveQualifier()
    {
        if (!string.IsNullOrWhiteSpace(QualifierText))
            return QualifierText.Trim();

        var clauses = new List<string>();
        if (!string.IsNullOrWhiteSpace(SubjectContains))
            clauses.Add(QualifierBuilder.Clause("subject", QualifierBuilder.Op.CaseInsensitiveLike, $"*{SubjectContains.Trim()}*"));
        if (!string.IsNullOrWhiteSpace(LastName))
            clauses.Add(QualifierBuilder.Clause("clientReporter.lastName", QualifierBuilder.Op.CaseInsensitiveLike, $"*{LastName.Trim()}*"));
        if (SelectedStatus != null)
            clauses.Add(QualifierBuilder.Clause("statusTypeId", QualifierBuilder.Op.Eq, SelectedStatus.Id.ToString(), false));
        if (SelectedPriority != null)
            clauses.Add(QualifierBuilder.Clause("priorityTypeId", QualifierBuilder.Op.Eq, SelectedPriority.Id.ToString(), false));
        if (SelectedLocation != null)
            clauses.Add(QualifierBuilder.Clause("locationId", QualifierBuilder.Op.Eq, SelectedLocation.Id.ToString(), false));
        if (SelectedTech != null)
            clauses.Add(QualifierBuilder.Clause("clientTech.clientId", QualifierBuilder.Op.Eq, SelectedTech.Id.ToString(), false));
        if (RequestTypePicker.SelectedRequestType != null)
            clauses.Add(QualifierBuilder.Clause("problemTypeId", QualifierBuilder.Op.Eq, RequestTypePicker.SelectedRequestType.Id.ToString(), false));

        // WHD qualifiers accept ISO-8601 UTC dates; the range applies to the selected date field.
        var dateAttr = DateClosed ? "closeDate" : DateUpdated ? "lastUpdated" : "reportDate";
        if (DateFrom != null)
            clauses.Add(QualifierBuilder.Clause(dateAttr, QualifierBuilder.Op.GtEq, IsoUtc(DateFrom.Value)));
        if (DateTo != null)
            clauses.Add(QualifierBuilder.Clause(dateAttr, QualifierBuilder.Op.Lt, IsoUtc(DateTo.Value.Date.AddDays(1))));

        return clauses.Count == 0 ? "" : QualifierBuilder.And(clauses.ToArray());
    }

    private static string IsoUtc(DateTime local) =>
        DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    [RelayCommand]
    private async Task SearchAsync()
    {
        Page = 1;
        await RefreshAsync();
    }

    [RelayCommand]
    private void Clear()
    {
        TicketNumber = "";
        SubjectContains = "";
        LastName = "";
        DateOpened = true;
        DateFrom = null;
        DateTo = null;
        SelectedStatus = null;
        SelectedPriority = null;
        SelectedLocation = null;
        SelectedTech = null;
        RequestTypePicker.Clear();
        QualifierText = "";
        Page = 1;
        Tickets.Clear();
        ErrorMessage = null;
    }

    [RelayCommand]
    private void ApplyFilter(SavedFilter? filter)
    {
        if (filter == null) return;
        QualifierText = filter.Qualifier;
        Page = 1;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private void SaveFilter()
    {
        var q = EffectiveQualifier();
        if (string.IsNullOrWhiteSpace(q) || string.IsNullOrWhiteSpace(FilterName)) return;

        var existing = Settings.Settings.SavedFilters.FirstOrDefault(f => f.Name == FilterName.Trim());
        if (existing != null)
        {
            existing.Qualifier = q;
        }
        else
        {
            Settings.Settings.SavedFilters.Add(new SavedFilter { Name = FilterName.Trim(), Qualifier = q });
        }
        Settings.Save();
        OnPropertyChanged(nameof(SavedFilters));
    }

    [RelayCommand]
    private void DeleteFilter(SavedFilter? filter)
    {
        if (filter == null) return;
        Settings.Settings.SavedFilters.Remove(filter);
        Settings.Save();
        OnPropertyChanged(nameof(SavedFilters));
    }
}
