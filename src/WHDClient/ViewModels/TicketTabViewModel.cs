using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WHDClient.Core.Models;
using WHDClient.Services;

namespace WHDClient.ViewModels;

public partial class TicketTabViewModel : TabViewModelBase
{
    private readonly WhdSessionContext _session;
    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;
    private readonly Action? _bookmarkChanged;

    public override bool IsClosable => true;

    public int TicketId { get; }

    [ObservableProperty] private Ticket? _ticket;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _infoMessage;

    /// <summary>False when the detail is empty or merely repeats the subject — then the detail block is hidden.</summary>
    [ObservableProperty] private bool _hasDistinctDetail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BookmarkIconSource))]
    [NotifyPropertyChangedFor(nameof(BookmarkLabel))]
    private bool _isBookmarked;

    public string BookmarkIconSource => IsBookmarked
        ? "pack://application:,,,/Assets/icons/remove-bookmark.png"
        : "pack://application:,,,/Assets/icons/add-bookmark.png";
    public string BookmarkLabel => IsBookmarked ? "Remove bookmark" : "Add bookmark";

    public ObservableCollection<TicketNote> Notes { get; } = new();

    // Editable fields
    [ObservableProperty] private StatusType? _selectedStatus;
    [ObservableProperty] private PriorityType? _selectedPriority;
    [ObservableProperty] private Tech? _selectedTech;

    /// <summary>
    /// When unchecked, saving field changes sends no update email to the client/tech
    /// (the update payload carries sendEmail=false).
    /// </summary>
    [ObservableProperty] private bool _sendUpdateEmail = true;

    public ObservableCollection<StatusType> StatusTypes { get; } = new();
    public ObservableCollection<PriorityType> PriorityTypes { get; } = new();
    public ObservableCollection<Tech> Techs { get; } = new();

    // Request type: cascading picker. The full list is slow (~3s) so it loads lazily
    // in the background, after the ticket renders, and pre-selects the current type.
    public RequestTypePickerViewModel RequestTypePicker { get; } = new();
    private bool _requestTypesLoading;

    // Reply
    [ObservableProperty] private string _replyText = "";
    [ObservableProperty] private bool _replyHidden;
    [ObservableProperty] private bool _replyIsSolution;
    [ObservableProperty] private bool _replyEmailClient = true;
    [ObservableProperty] private bool _replyEmailTech;
    [ObservableProperty] private bool _replyAlsoSetStatus;

    // Cc recipients on the reply (searched from techs + clients, sent as email addresses)
    [ObservableProperty] private string _ccSearchText = "";
    [ObservableProperty] private bool _isSearchingCc;
    [ObservableProperty] private CcRecipient? _selectedCcResult;
    public ObservableCollection<CcRecipient> CcResults { get; } = new();
    public ObservableCollection<CcRecipient> CcRecipients { get; } = new();

    /// <summary>Raised (on the UI thread) after a Cc search completes; the view opens the results dropdown.</summary>
    public event Action? CcSearchCompleted;

    // Picking a search result moves it into the recipient list.
    partial void OnSelectedCcResultChanged(CcRecipient? value)
    {
        if (value == null) return;
        if (!CcRecipients.Any(r => r.Email.Equals(value.Email, StringComparison.OrdinalIgnoreCase)))
            CcRecipients.Add(value);
        SelectedCcResult = null;
        CcSearchText = "";
        CcResults.Clear();
    }

    /// <summary>Files staged in the reply panel; uploaded onto the note when it is posted.</summary>
    public ObservableCollection<string> PendingReplyAttachments { get; } = new();

    public TicketTabViewModel(WhdSessionContext session, SettingsService settings, NotificationService notifications,
        int ticketId, Action? bookmarkChanged = null)
    {
        _session = session;
        _settings = settings;
        _notifications = notifications;
        _bookmarkChanged = bookmarkChanged;
        TicketId = ticketId;
        Header = $"#{ticketId}";
        IsBookmarked = settings.Settings.BookmarkedTicketIds.Contains(ticketId);
        _ = RefreshCoreAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        await RefreshCoreAsync();
    }

    /// <summary>Reloads ticket, notes, and lookups. Callers that already set IsBusy use this directly.</summary>
    private async Task RefreshCoreAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            // Ticket and notes are independent fetches — run them concurrently.
            var ticketTask = _session.Tickets.GetTicketAsync(TicketId);
            var notesTask = _session.Tickets.GetNotesAsync(TicketId);
            var ticket = await ticketTask;
            if (ticket == null)
            {
                ErrorMessage = $"Ticket {TicketId} not found.";
                return;
            }
            Ticket = ticket;
            Header = $"#{ticket.Id} {Truncate(ticket.DisplaySubject, 30)}";
            HasDistinctDetail = ComputeHasDistinctDetail(ticket);

            var notes = await notesTask;
            Notes.Clear();
            foreach (var n in notes.OrderByDescending(n => n.EffectiveDate))
                Notes.Add(n);

            // Dev/test hook: WHD_FAKE_NOTE=<bbcode> injects a local-only note for rendering tests.
            var fake = Environment.GetEnvironmentVariable("WHD_FAKE_NOTE");
            if (!string.IsNullOrEmpty(fake))
                Notes.Insert(0, new TicketNote
                {
                    Id = 0,
                    NoteText = fake,
                    DateUtc = DateTimeOffset.Now,
                    Tech = new Tech { FirstName = "Test", LastName = "Note" }
                });

            await LoadLookupsAsync();
            _ = LoadRequestTypesAsync();

            // Prefer the statuses valid for this ticket's process (enabledStatusTypes) — the
            // global /StatusTypes list omits approval-process statuses like "Approval Pending".
            if (ticket.EnabledStatusTypes?.Count > 0)
            {
                StatusTypes.Clear();
                foreach (var s in ticket.EnabledStatusTypes) StatusTypes.Add(s);
            }
            if (ticket.StatusType != null && StatusTypes.All(s => s.Id != ticket.StatusType.Id))
                StatusTypes.Add(ticket.StatusType);

            SelectedStatus = StatusTypes.FirstOrDefault(s => s.Id == ticket.StatusType?.Id);
            SelectedPriority = PriorityTypes.FirstOrDefault(p => p.Id == ticket.PriorityType?.Id);
            if (!Techs.Contains(Tech.NotAssigned))
                Techs.Insert(0, Tech.NotAssigned);
            SelectedTech = ticket.ClientTech == null
                ? Tech.NotAssigned
                : Techs.FirstOrDefault(t => t.Id == ticket.ClientTech.Id);
            if (SelectedTech == null && ticket.ClientTech != null)
            {
                // The assigned tech may be inactive — keep them visible and selected anyway.
                Techs.Add(ticket.ClientTech);
                SelectedTech = ticket.ClientTech;
            }
            IsBookmarked = _settings.Settings.BookmarkedTicketIds.Contains(TicketId);
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

    private async Task LoadLookupsAsync()
    {
        if (StatusTypes.Count == 0)
        {
            foreach (var s in await _session.Lookups.GetStatusTypesAsync()) StatusTypes.Add(s);
            foreach (var p in await _session.Lookups.GetPriorityTypesAsync()) PriorityTypes.Add(p);
            // "Not Assigned" (clears clientTech) at the top, then the real techs.
            Techs.Add(Tech.NotAssigned);
            foreach (var t in await _session.Lookups.GetActiveTechsAsync()) Techs.Add(t);
        }
    }

    /// <summary>
    /// Loads the selectable request types (slow) and pre-selects the ticket's current type.
    /// Runs in the background so opening a ticket never waits on the ~3s RequestTypes list.
    /// The ticket's current type may be archived — then it is simply not in the picker and
    /// the dropdown starts unselected (the type only changes when the user picks one).
    /// </summary>
    private async Task LoadRequestTypesAsync()
    {
        if (_requestTypesLoading || RequestTypePicker.IsLoaded) return;
        _requestTypesLoading = true;
        try
        {
            var types = await _session.Lookups.GetSelectableRequestTypesAsync();
            RequestTypePicker.SetRequestTypes(types);
            if (Ticket?.ProblemType != null)
                RequestTypePicker.SetSelectedRequestType(Ticket.ProblemType.Id);
        }
        catch
        {
            // Non-fatal — the picker simply stays empty; saving still works for the other fields.
        }
        finally
        {
            _requestTypesLoading = false;
        }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    /// <summary>True when the detail says something the subject doesn't already say.</summary>
    private static bool ComputeHasDistinctDetail(Ticket t)
    {
        var detail = Normalize(t.DisplayDetail);
        if (detail.Length == 0) return false;
        // No real subject: the header shows a truncated detail snippet, so always show the full detail.
        if (!t.HasSubject) return true;
        var subject = Normalize(t.DisplaySubject).TrimEnd('.', '…', ' ');
        if (subject.Length == 0) return true;
        return !detail.StartsWith(subject, StringComparison.OrdinalIgnoreCase)
            && !subject.StartsWith(detail, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (Ticket == null) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var payload = new Dictionary<string, object?>();
            if (SelectedStatus != null && SelectedStatus.Id != Ticket.StatusType?.Id)
                payload["statustype"] = new EntityRef(SelectedStatus.Id, "StatusType");
            if (SelectedPriority != null && SelectedPriority.Id != Ticket.PriorityType?.Id)
                payload["prioritytype"] = new EntityRef(SelectedPriority.Id, "PriorityType");
            // Request type: only sent when the user picked a (selectable) type different from
            // the current one. The ticket's current type may be archived — the picker starts
            // unselected then and leaves the request type untouched.
            if (RequestTypePicker.SelectedRequestType != null
                && Ticket.ProblemType?.Id != RequestTypePicker.SelectedRequestType.Id)
                payload["problemtype"] = new EntityRef(RequestTypePicker.SelectedRequestType.Id, "ProblemType");
            if (ReferenceEquals(SelectedTech, Tech.NotAssigned))
            {
                if (Ticket.ClientTech != null)
                    payload["clientTech"] = null; // unassign: clears the ticket's tech
            }
            else if (SelectedTech != null && SelectedTech.Id != Ticket.ClientTech?.Id)
                payload["clientTech"] = new EntityRef(SelectedTech.Id, "Tech");

            // "Send update email" unchecked suppresses the client/tech notification on this save.
            // sendEmail only appears in the payload when emails are NOT wanted, so an ordinary
            // save keeps the server's default behavior exactly as before.
            if (!SendUpdateEmail)
                payload["sendEmail"] = false;

            if (payload.Count == 0)
            {
                InfoMessage = "No changes to save.";
                return;
            }
            await _session.Tickets.UpdateTicketAsync(TicketId, payload);
            InfoMessage = "Saved.";
            _notifications.MarkSelfModified(TicketId);
            await RefreshCoreAsync();
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

    [RelayCommand]
    private async Task ReplyAsync()
    {
        if (Ticket == null || string.IsNullOrWhiteSpace(ReplyText)) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var note = new TechNotePayload
            {
                NoteText = ReplyText,
                JobTicket = new EntityRef(TicketId, "JobTicket"),
                IsHidden = ReplyHidden,
                IsSolution = ReplyIsSolution,
                EmailClient = ReplyEmailClient,
                EmailTech = ReplyEmailTech,
                EmailCc = CcRecipients.Count > 0,
                CcAddressesForTech = CcRecipients.Count > 0
                    ? string.Join(",", CcRecipients.Select(r => r.Email))
                    : null,
                StatusTypeId = ReplyAlsoSetStatus && SelectedStatus != null ? SelectedStatus.Id : null
            };
            var created = await _session.Tickets.AddTechNoteAsync(note);

            // Attach staged files to the new note (type=techNote requires the note's id).
            var uploadFailures = new List<string>();
            if (PendingReplyAttachments.Count > 0)
            {
                if (created == null)
                {
                    uploadFailures.Add("server did not return a note id");
                }
                else
                {
                    foreach (var path in PendingReplyAttachments)
                    {
                        try
                        {
                            await using var fs = File.OpenRead(path);
                            await _session.Api.UploadAttachmentAsync("techNote", created.Id, Path.GetFileName(path), fs);
                        }
                        catch (Exception ex)
                        {
                            uploadFailures.Add($"{Path.GetFileName(path)}: {ex.Message}");
                        }
                    }
                }
            }

            ReplyText = "";
            PendingReplyAttachments.Clear();
            CcRecipients.Clear();
            InfoMessage = uploadFailures.Count == 0
                ? "Note added."
                : $"Note added, but {uploadFailures.Count} attachment(s) failed: {string.Join("; ", uploadFailures)}";
            _notifications.MarkSelfModified(TicketId);
            await RefreshCoreAsync();
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

    [RelayCommand]
    private async Task SearchCcAsync()
    {
        var fragment = CcSearchText.Trim();
        if (fragment.Length < 2 || IsSearchingCc) return;
        IsSearchingCc = true;
        try
        {
            // The /Techs endpoint ignores name qualifiers (returns the whole list,
            // including inactive accounts), so use the cached tech list and filter here.
            var techs = await _session.Lookups.GetTechsAsync();
            var clients = await _session.Lookups.SearchClientsAsync(fragment);
            CcResults.Clear();
            foreach (var t in techs.Where(t => t.IsSelectable && !string.IsNullOrEmpty(t.Email) && MatchesFragment(t, fragment)))
                CcResults.Add(new CcRecipient { Email = t.Email!, Name = t.DisplayName, Kind = "Tech" });
            foreach (var c in clients.Where(c => !string.IsNullOrEmpty(c.Email)
                     && CcResults.All(r => !r.Email.Equals(c.Email, StringComparison.OrdinalIgnoreCase))))
                CcResults.Add(new CcRecipient { Email = c.Email!, Name = c.DisplayName, Kind = "Client" });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Cc search failed: {ex.Message}";
        }
        finally
        {
            IsSearchingCc = false;
            CcSearchCompleted?.Invoke();
        }
    }

    private static bool MatchesFragment(Tech t, string fragment) =>
        (t.FirstName?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false) ||
        (t.LastName?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false) ||
        (t.ServerDisplayName?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false) ||
        (t.Email?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false);

    [RelayCommand]
    private void RemoveCcRecipient(CcRecipient? recipient)
    {
        if (recipient != null) CcRecipients.Remove(recipient);
    }

    [RelayCommand]
    private void AddReplyAttachments()
    {
        var dlg = new OpenFileDialog { Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        foreach (var path in dlg.FileNames)
        {
            if (!PendingReplyAttachments.Contains(path)) PendingReplyAttachments.Add(path);
        }
    }

    [RelayCommand]
    private void RemoveReplyAttachment(string? path)
    {
        if (path != null) PendingReplyAttachments.Remove(path);
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        var url = Ticket?.BookmarkableLink;
        if (!string.IsNullOrEmpty(url))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task DownloadAttachmentAsync(TicketAttachment? attachment)
    {
        if (attachment == null) return;
        try
        {
            var (bytes, fileName) = await _session.Api.GetAttachmentAsync(attachment.Id);
            var name = fileName ?? attachment.DisplayName;
            var dlg = new SaveFileDialog { FileName = name };
            if (dlg.ShowDialog() == true)
            {
                await File.WriteAllBytesAsync(dlg.FileName, bytes);
                InfoMessage = $"Saved {name}.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Download failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleBookmark()
    {
        var ids = _settings.Settings.BookmarkedTicketIds;
        if (ids.Contains(TicketId)) ids.Remove(TicketId);
        else ids.Add(TicketId);
        _settings.Save();
        IsBookmarked = ids.Contains(TicketId);
        _bookmarkChanged?.Invoke();
    }
}

/// <summary>A Cc recipient picked from techs/clients; WHD wants the email address.</summary>
public class CcRecipient
{
    public required string Email { get; init; }
    public required string Name { get; init; }
    /// <summary>"Tech" or "Client".</summary>
    public required string Kind { get; init; }

    /// <summary>Matches the web UI's format: "email (Name)".</summary>
    public string Label => $"{Email} ({Name})";
}
