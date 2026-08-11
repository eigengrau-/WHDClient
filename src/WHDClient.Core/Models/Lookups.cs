using System.Text.Json.Serialization;

namespace WHDClient.Core.Models;

public class Tech
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("lastName")] public string? LastName { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("displayName")] public string? ServerDisplayName { get; set; }
    /// <summary>WHD emits these as 0/1 ints; only present with style=details.</summary>
    [JsonPropertyName("inactive")] public bool Inactive { get; set; }
    [JsonPropertyName("deleted")] public bool Deleted { get; set; }

    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(ServerDisplayName)
            ? ServerDisplayName
            : string.Join(" ", new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } n
                ? n
                : Username ?? $"tech {Id}";

    /// <summary>"LastName, FirstName" — how WHD lists techs in its own dropdowns.</summary>
    [JsonIgnore]
    public string ListDisplayName =>
        !string.IsNullOrWhiteSpace(LastName) && !string.IsNullOrWhiteSpace(FirstName)
            ? $"{LastName}, {FirstName}"
            : DisplayName;

    /// <summary>Inactive/deleted techs stay on old tickets but must not be assignable.</summary>
    [JsonIgnore]
    public bool IsSelectable => !Inactive && !Deleted;

    /// <summary>Search sentinel for "Not Assigned" (tickets with no tech). Not a real tech.</summary>
    [JsonIgnore]
    public static Tech NotAssigned { get; } = new() { Id = -1, ServerDisplayName = "Not Assigned" };
}

public class Client
{
    [JsonPropertyName("id")] public object? Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("lastName")] public string? LastName { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }

    [JsonIgnore]
    public string DisplayName =>
        string.Join(" ", new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } n
            ? n
            : Username ?? Email ?? $"client {Id}";
}

public class StatusType
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("statusTypeName")] public string? StatusTypeName { get; set; }

    [JsonIgnore]
    public string DisplayName => StatusTypeName ?? $"status {Id}";
}

public class PriorityType
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("priorityTypeName")] public string? PriorityTypeName { get; set; }
    [JsonPropertyName("displayOrder")] public int? DisplayOrder { get; set; }

    [JsonIgnore]
    public string DisplayName => PriorityTypeName ?? $"priority {Id}";
}

public class RequestType
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("problemTypeName")] public string? ProblemTypeName { get; set; }
    [JsonPropertyName("detailDisplayName")] public string? DetailDisplayName { get; set; }
    [JsonPropertyName("parentId")] public int? ParentId { get; set; }
    [JsonPropertyName("fullName")] public string? FullName { get; set; }
    [JsonPropertyName("archived")] public bool Archived { get; set; }
    [JsonPropertyName("deleted")] public bool Deleted { get; set; }
    /// <summary>WHD emits this as 0/1 with style=details. When set, tickets of this type
    /// have no subject field — the subject is derived from the detail instead.</summary>
    [JsonPropertyName("hideSubject")] public bool HideSubject { get; set; }

    /// <summary>Archived/deleted request types are kept for display on old tickets but must not be selectable.</summary>
    [JsonIgnore]
    public bool IsSelectable => !Archived && !Deleted;

    [JsonIgnore]
    public string DisplayName => Clean(DetailDisplayName ?? FullName ?? ProblemTypeName) ?? $"request type {Id}";

    // WHD sometimes returns mojibake for UTF-8 punctuation (e.g. "â€¢" instead of "•").
    private static string? Clean(string? s) =>
        s?.Replace("â€¢", "•").Replace("Â", "").Replace("&nbsp;", " ").Trim();
}

public class Location
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("locationName")] public string? LocationName { get; set; }

    [JsonIgnore]
    public string DisplayName => LocationName ?? $"location {Id}";
}

public class Department
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("departmentName")] public string? DepartmentName { get; set; }

    [JsonIgnore]
    public string DisplayName => DepartmentName ?? $"department {Id}";
}

public class WhdSession
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("sessionKey")] public string? SessionKey { get; set; }
    [JsonPropertyName("instanceId")] public int InstanceId { get; set; } = -1;
    [JsonPropertyName("currentTechId")] public int? CurrentTechId { get; set; }
}
