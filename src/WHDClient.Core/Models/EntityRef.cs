using System.Text.Json.Serialization;

namespace WHDClient.Core.Models;

/// <summary>Reference to another WHD entity, as used in ticket create/update payloads.</summary>
public class EntityRef
{
    [JsonPropertyName("id")] public object? Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }

    public EntityRef() { }

    public EntityRef(object id, string type)
    {
        Id = id;
        Type = type;
    }
}
