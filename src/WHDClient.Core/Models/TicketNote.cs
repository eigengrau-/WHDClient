using System.Text.Json.Serialization;

namespace WHDClient.Core.Models;

public class TicketNote
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("noteText")] public string? NoteText { get; set; }
    [JsonPropertyName("date")] public DateTimeOffset? Date { get; set; }
    [JsonPropertyName("dateUtc")] public DateTimeOffset? DateUtc { get; set; }
    [JsonPropertyName("prettyDate")] public string? PrettyDate { get; set; }

    [JsonPropertyName("isHidden")] public bool IsHidden { get; set; }
    [JsonPropertyName("isSolution")] public bool IsSolution { get; set; }
    [JsonPropertyName("workTime")] public string? WorkTime { get; set; }

    [JsonPropertyName("jobticket")] public EntityRef? JobTicket { get; set; }
    [JsonPropertyName("tech")] public Tech? Tech { get; set; }
    [JsonPropertyName("clientTech")] public Tech? ClientTech { get; set; }
    [JsonPropertyName("clientNoteBy")] public Client? Client { get; set; }
    [JsonPropertyName("mobileTechNote")] public Tech? MobileTech { get; set; }

    [JsonPropertyName("mobileNoteText")] public string? MobileNoteText { get; set; }
    [JsonPropertyName("prettyUpdatedString")] public string? PrettyUpdatedString { get; set; }

    [JsonPropertyName("attachments")] public List<TicketAttachment>? Attachments { get; set; }

    [JsonIgnore]
    public bool IsTechNote => string.Equals(Type, "TechNote", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(Type, "MobileTechNote", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Text for rich display: the BBCode noteText when present, otherwise the raw HTML
    /// mobile variant. The renderer auto-detects the dialect, so no stripping happens here.
    /// </summary>
    [JsonIgnore]
    public string? DisplayText => !string.IsNullOrWhiteSpace(NoteText) ? NoteText : MobileNoteText;

    [JsonIgnore]
    public string AuthorDisplay =>
        ClientTech?.DisplayName is { Length: > 0 } ct && !ct.StartsWith("tech ")
            ? ct
            : ExtractAuthor(PrettyUpdatedString)
              ?? Tech?.DisplayName ?? Client?.DisplayName ?? MobileTech?.DisplayName ?? "Unknown";

    // "1 day ago <strong>Riley Janzen</strong> said" -> "Riley Janzen"
    private static string? ExtractAuthor(string? pretty)
    {
        if (string.IsNullOrEmpty(pretty)) return null;
        var start = pretty.IndexOf("<strong>", StringComparison.OrdinalIgnoreCase);
        var end = pretty.IndexOf("</strong>", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end <= start) return null;
        return pretty[(start + 8)..end].Trim();
    }

    [JsonIgnore]
    public DateTimeOffset? EffectiveDate => DateUtc ?? Date;
}

/// <summary>Payload for POST /ra/TechNotes.</summary>
public class TechNotePayload
{
    [JsonPropertyName("noteText")] public string NoteText { get; set; } = "";
    [JsonPropertyName("jobticket")] public EntityRef JobTicket { get; set; } = new();
    [JsonPropertyName("workTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkTime { get; set; }
    [JsonPropertyName("isHidden")] public bool IsHidden { get; set; }
    [JsonPropertyName("isSolution")] public bool IsSolution { get; set; }
    [JsonPropertyName("statusTypeId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StatusTypeId { get; set; }
    [JsonPropertyName("emailClient")] public bool EmailClient { get; set; }
    [JsonPropertyName("emailTech")] public bool EmailTech { get; set; }
    [JsonPropertyName("emailTechGroupLevel")] public bool EmailTechGroupLevel { get; set; }
    [JsonPropertyName("emailGroupManager")] public bool EmailGroupManager { get; set; }
    [JsonPropertyName("emailCc")] public bool EmailCc { get; set; }
    [JsonPropertyName("ccAddressesForTech"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CcAddressesForTech { get; set; }
}
