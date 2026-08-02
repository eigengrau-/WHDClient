using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WHDClient.Core.Models;
using WHDClient.Services;

namespace WHDClient.ViewModels;

public partial class CreateTicketViewModel : TabViewModelBase
{
    private readonly WhdSessionContext _session;
    private readonly Action<int> _onCreated;

    public override bool IsClosable => true;

    [ObservableProperty] private string _subject = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private string _clientSearchText = "";
    [ObservableProperty] private Client? _selectedClient;
    [ObservableProperty] private bool _isSearchingClients;
    public ObservableCollection<Client> ClientResults { get; } = new();

    /// <summary>Raised (on the UI thread) after a client search completes; the view opens the results dropdown.</summary>
    public event Action? ClientSearchCompleted;

    /// <summary>Files queued for upload once the ticket is created.</summary>
    public ObservableCollection<string> PendingAttachments { get; } = new();

    public RequestTypePickerViewModel RequestTypePicker { get; } = new();
    public ObservableCollection<PriorityType> PriorityTypes { get; } = new();
    public ObservableCollection<Location> Locations { get; } = new();
    public ObservableCollection<Tech> Techs { get; } = new();

    [ObservableProperty] private PriorityType? _selectedPriority;
    [ObservableProperty] private Location? _selectedLocation;
    [ObservableProperty] private Tech? _selectedTech;
    [ObservableProperty] private bool _assignToMe = true;

    // Guards re-entrancy while "Assign to me" and the Tech dropdown keep each other in sync.
    private bool _syncingTech;

    partial void OnAssignToMeChanged(bool value)
    {
        if (_syncingTech) return;
        _syncingTech = true;
        try
        {
            if (value)
                SelectedTech = Techs.FirstOrDefault(t => t.Id == _session.CurrentTech.Id) ?? SelectedTech;
            else if (SelectedTech?.Id == _session.CurrentTech.Id)
                SelectedTech = null;
        }
        finally { _syncingTech = false; }
    }

    partial void OnSelectedTechChanged(Tech? value)
    {
        if (_syncingTech) return;
        _syncingTech = true;
        try { AssignToMe = value != null && value.Id == _session.CurrentTech.Id; }
        finally { _syncingTech = false; }
    }

    public CreateTicketViewModel(WhdSessionContext session, Action<int> onCreated)
    {
        _session = session;
        _onCreated = onCreated;
        Header = "New Ticket";
        IconSource = "pack://application:,,,/Assets/icons/new-ticket.png";
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await _session.Lookups.EnsureLoadedAsync();
            RequestTypePicker.SetRequestTypes(await _session.Lookups.GetSelectableRequestTypesAsync());
            foreach (var p in await _session.Lookups.GetPriorityTypesAsync()) PriorityTypes.Add(p);
            foreach (var l in await _session.Lookups.GetLocationsAsync()) Locations.Add(l);
            foreach (var t in await _session.Lookups.GetActiveTechsAsync()) Techs.Add(t);

            // "Assign to me" defaults on — reflect it in the Tech dropdown.
            if (AssignToMe) OnAssignToMeChanged(true);

            // The reporter defaults to the current user (techs are clients too);
            // a ticket cannot be created without a client.
            await DefaultClientToCurrentUserAsync();

            // Dev/test hook: WHD_NEW_TICKET_REQUEST_TYPE=<id> pre-selects a request type path.
            var preselect = Environment.GetEnvironmentVariable("WHD_NEW_TICKET_REQUEST_TYPE");
            if (int.TryParse(preselect, out var rtId)) RequestTypePicker.SetSelectedRequestType(rtId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load lookups: {ex.Message}";
        }
    }

    /// <summary>Finds the client record matching the signed-in tech (by email, then name) and selects it.</summary>
    private async Task DefaultClientToCurrentUserAsync()
    {
        try
        {
            var tech = _session.CurrentTech;
            var matches = await _session.Lookups.SearchClientsAsync(
                !string.IsNullOrWhiteSpace(tech.Email) ? tech.Email : tech.DisplayName);
            SelectedClient = matches.FirstOrDefault(c =>
                                 string.Equals(c.Email, tech.Email, StringComparison.OrdinalIgnoreCase))
                             ?? matches.FirstOrDefault(c =>
                                 string.Equals(c.DisplayName, tech.DisplayName, StringComparison.OrdinalIgnoreCase))
                             ?? matches.FirstOrDefault();
            // The combo only renders selections that exist in its ItemsSource.
            if (SelectedClient != null && !ClientResults.Contains(SelectedClient))
                ClientResults.Insert(0, SelectedClient);
        }
        catch
        {
            // Non-fatal — the user can still pick a client manually.
        }
    }

    [RelayCommand]
    private async Task SearchClientsAsync()
    {
        if (string.IsNullOrWhiteSpace(ClientSearchText) || IsSearchingClients) return;
        IsSearchingClients = true;
        try
        {
            var clients = await _session.Lookups.SearchClientsAsync(ClientSearchText.Trim());
            ClientResults.Clear();
            foreach (var c in clients) ClientResults.Add(c);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Client search failed: {ex.Message}";
        }
        finally
        {
            IsSearchingClients = false;
            ClientSearchCompleted?.Invoke();
        }
    }

    [RelayCommand]
    private void AddAttachments()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        foreach (var path in dlg.FileNames)
        {
            if (!PendingAttachments.Contains(path)) PendingAttachments.Add(path);
        }
    }

    [RelayCommand]
    private void RemoveAttachment(string? path)
    {
        if (path != null) PendingAttachments.Remove(path);
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Subject)) { ErrorMessage = "Subject is required."; return; }
        if (RequestTypePicker.SelectedRequestType == null) { ErrorMessage = "Request type is required."; return; }
        if (SelectedClient?.Id == null) { ErrorMessage = "Client is required — search and select a reporter."; return; }

        IsBusy = true;
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["subject"] = Subject.Trim(),
                ["detail"] = Detail,
                ["problemtype"] = new EntityRef(RequestTypePicker.SelectedRequestType!.Id, "ProblemType"),
                ["reportDateUtc"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                ["sendEmail"] = false,
                // An explicitly chosen tech wins over the "assign to me" checkbox.
                ["assignToCreatingTech"] = SelectedTech == null && AssignToMe
            };
            if (SelectedTech != null) payload["clientTech"] = new EntityRef(SelectedTech.Id, "Tech");
            if (SelectedClient?.Id != null) payload["clientReporter"] = new EntityRef(SelectedClient.Id, "Client");
            if (SelectedPriority != null) payload["prioritytype"] = new EntityRef(SelectedPriority.Id, "PriorityType");
            if (SelectedLocation != null) payload["location"] = new EntityRef(SelectedLocation.Id, "Location");

            var created = await _session.Tickets.CreateTicketAsync(payload);
            if (created == null || created.Id <= 0)
            {
                ErrorMessage = "Server did not return a ticket id.";
                return;
            }

            var uploadFailures = new List<string>();
            foreach (var path in PendingAttachments)
            {
                try
                {
                    await using var fs = System.IO.File.OpenRead(path);
                    await _session.Api.UploadAttachmentAsync("jobTicket", created.Id, System.IO.Path.GetFileName(path), fs);
                }
                catch (Exception ex)
                {
                    uploadFailures.Add($"{System.IO.Path.GetFileName(path)}: {ex.Message}");
                }
            }

            if (uploadFailures.Count > 0)
            {
                // Keep this tab open so the error is visible; the ticket itself was created.
                ErrorMessage = $"Ticket #{created.Id} created, but {uploadFailures.Count} attachment(s) failed: " +
                               string.Join("; ", uploadFailures);
                return;
            }
            _onCreated(created.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
