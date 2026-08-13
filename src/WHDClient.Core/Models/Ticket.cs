using System.Text.Json.Serialization;

namespace WHDClient.Core.Models;

public class Ticket
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("subject")] public string? Subject { get; set; }
    [JsonPropertyName("shortSubject")] public string? ShortSubject { get; set; }
    [JsonPropertyName("detail")] public string? Detail { get; set; }
    [JsonPropertyName("shortDetail")] public string? ShortDetail { get; set; }
    [JsonPropertyName("displayClient")] public string? DisplayClient { get; set; }
    [JsonPropertyName("prettyLastUpdated")] public string? PrettyLastUpdated { get; set; }
    [JsonPropertyName("bookmarkableLink")] public string? BookmarkableLink { get; set; }

    [JsonPropertyName("reportDateUtc")] public DateTimeOffset? ReportDateUtc { get; set; }
    [JsonPropertyName("lastUpdated")] public DateTimeOffset? LastUpdated { get; set; }
    [JsonPropertyName("lastUpdatedUtc")] public DateTimeOffset? LastUpdatedUtc { get; set; }
    [JsonPropertyName("closeDateUtc")] public DateTimeOffset? CloseDateUtc { get; set; }
    [JsonPropertyName("displayDueDateUtc")] public DateTimeOffset? DisplayDueDateUtc { get; set; }

    [JsonPropertyName("statusTypeId")] public int? StatusTypeId { get; set; }
    [JsonPropertyName("locationId")] public int? LocationId { get; set; }
    [JsonPropertyName("room")] public string? Room { get; set; }

    [JsonPropertyName("statustype")] public StatusType? StatusType { get; set; }
    // Statuses valid for this ticket's process — includes ones (e.g. approval-process
    // statuses like "Approval Pending") that the global /StatusTypes list omits.
    [JsonPropertyName("enabledStatusTypes")] public List<StatusType>? EnabledStatusTypes { get; set; }
    [JsonPropertyName("prioritytype")] public PriorityType? PriorityType { get; set; }
    [JsonPropertyName("problemtype")] public RequestType? ProblemType { get; set; }
    [JsonPropertyName("location")] public Location? Location { get; set; }
    [JsonPropertyName("department")] public Department? Department { get; set; }
    [JsonPropertyName("clientReporter")] public Client? ClientReporter { get; set; }
    [JsonPropertyName("clientTech")] public Tech? ClientTech { get; set; }

    [JsonPropertyName("latestNote")] public TicketNote? LatestNote { get; set; }
    [JsonPropertyName("attachments")] public List<TicketAttachment>? Attachments { get; set; }

    /// <summary>Comma-joined email addresses Cc'd on this ticket (settable via update).</summary>
    [JsonPropertyName("ccAddressesForTech")] public string? CcAddressesForTech { get; set; }

    /// <summary>
    /// Note stubs embedded in the style=details ticket response. Unlike the /TicketNotes
    /// endpoint (which omits attachments entirely), these carry their attachments — the
    /// only way the REST API exposes note attachments. Merged into the full notes by the
    /// ticket tab after load.
    /// </summary>
    [JsonPropertyName("notes")] public List<TicketNote>? EmbeddedNotes { get; set; }

    [JsonPropertyName("deleted")] public int? Deleted { get; set; }

    /// <summary>Best-effort UTC last-updated stamp used for change detection.</summary>
    [JsonIgnore]
    public DateTimeOffset? EffectiveLastUpdated => LastUpdatedUtc ?? LastUpdated;

    /// <summary>True when the ticket carries a real subject (as opposed to a detail-derived fallback).</summary>
    [JsonIgnore]
    public bool HasSubject =>
        !string.IsNullOrWhiteSpace(Subject) || !string.IsNullOrWhiteSpace(ShortSubject);

    [JsonIgnore]
    public string DisplaySubject
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Subject)) return OneLine(Subject);
            if (!string.IsNullOrWhiteSpace(ShortSubject)) return OneLine(ShortSubject);
            // Some tickets have no subject at all — fall back to a detail snippet.
            // Prefer the server's shortDetail (already plain + shortened); the full detail may contain HTML.
            var detail = !string.IsNullOrWhiteSpace(ShortDetail) ? ShortDetail! : DisplayDetail;
            detail = OneLine(detail);
            if (!string.IsNullOrWhiteSpace(detail))
                return detail.Length <= 60 ? detail : detail[..60] + "…";
            return $"(ticket {Id})";
        }
    }

    /// <summary>Collapses all whitespace runs (incl. newlines) so list rows stay single-line.</summary>
    internal static string OneLine(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();

    /// <summary>
    /// Raw detail for rich rendering. WHD stores request details as HTML while notes
    /// are BBCode; the renderer auto-detects the dialect, so no stripping happens here.
    /// </summary>
    [JsonIgnore]
    public string RenderableDetail => Detail ?? ShortDetail ?? "";

    /// <summary>Detail with HTML markup stripped for plain-text display.</summary>
    [JsonIgnore]
    public string DisplayDetail
    {
        get
        {
            var s = Detail ?? ShortDetail ?? "";
            if (string.IsNullOrEmpty(s)) return "";
            s = System.Text.RegularExpressions.Regex.Replace(s, "<br\\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");
            return System.Net.WebUtility.HtmlDecode(s).Trim();
        }
    }
}

public class TicketAttachment
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("fileName")] public string? FileName { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("uploadDate")] public DateTimeOffset? UploadDate { get; set; }
    [JsonPropertyName("size")] public long? Size { get; set; }

    [JsonIgnore]
    public string DisplayName => FileName ?? Name ?? $"attachment {Id}";
}
