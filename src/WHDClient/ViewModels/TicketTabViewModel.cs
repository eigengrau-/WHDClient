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

    public ObservableCollection<StatusType> StatusTypes { get; } = new();
    public ObservableCollection<PriorityType> PriorityTypes { get; } = new();
    public ObservableCollection<Tech> Techs { get; } = new();

    // Reply
    [ObservableProperty] private string _replyText = "";
    [ObservableProperty] private bool _replyHidden;
    [ObservableProperty] private bool _replyIsSolution;
    [ObservableProperty] private bool _replyEmailClient = true;
    [ObservableProperty] private bool _replyEmailTech;
    [ObservableProperty] private bool _replyAlsoSetStatus;

    /// <summary>Files staged in the reply panel; uploaded onto the note when it is posted.</summary>
    public ObservableCollection<string> PendingReplyAttachments { get; } = new();

    public TicketTabViewModel(WhdSessionContext session, SettingsService settings, int ticketId,
        Action? bookmarkChanged = null)
    {
        _session = session;
        _settings = settings;
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
            var ticket = await _session.Tickets.GetTicketAsync(TicketId);
            if (ticket == null)
            {
                ErrorMessage = $"Ticket {TicketId} not found.";
                return;
            }
            Ticket = ticket;
            Header = $"#{ticket.Id} {Truncate(ticket.DisplaySubject, 30)}";
            HasDistinctDetail = ComputeHasDistinctDetail(ticket);

            var notes = await _session.Tickets.GetNotesAsync(TicketId);
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

            SelectedStatus = StatusTypes.FirstOrDefault(s => s.Id == ticket.StatusType?.Id);
            SelectedPriority = PriorityTypes.FirstOrDefault(p => p.Id == ticket.PriorityType?.Id);
            SelectedTech = Techs.FirstOrDefault(t => t.Id == ticket.ClientTech?.Id);
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
            foreach (var t in await _session.Lookups.GetActiveTechsAsync()) Techs.Add(t);
        }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    /// <summary>True when the detail says something the subject doesn't already say.</summary>
    private static bool ComputeHasDistinctDetail(Ticket t)
    {
        var detail = Normalize(t.DisplayDetail);
        if (detail.Length == 0) return false;
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
            if (SelectedTech != null && SelectedTech.Id != Ticket.ClientTech?.Id)
                payload["clientTech"] = new EntityRef(SelectedTech.Id, "Tech");

            if (payload.Count == 0)
            {
                InfoMessage = "No changes to save.";
                return;
            }
            await _session.Tickets.UpdateTicketAsync(TicketId, payload);
            InfoMessage = "Saved.";
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
            InfoMessage = uploadFailures.Count == 0
                ? "Note added."
                : $"Note added, but {uploadFailures.Count} attachment(s) failed: {string.Join("; ", uploadFailures)}";
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
