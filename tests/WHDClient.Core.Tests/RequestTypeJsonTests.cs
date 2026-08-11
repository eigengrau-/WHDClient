using System.Text.Json;
using WHDClient.Core.Api;
using WHDClient.Core.Models;
using Xunit;

namespace WHDClient.Core.Tests;

public class RequestTypeJsonTests
{
    [Fact]
    public void HideSubject_DeserializesFromWhdIntFlag()
    {
        // WHD emits booleans as 0/1 with style=details.
        var shown = JsonSerializer.Deserialize<RequestType>(
            """{ "id": 517, "type": "RequestType", "problemTypeName": "Email", "hideSubject": 0 }""", WhdJson.Options)!;
        var hidden = JsonSerializer.Deserialize<RequestType>(
            """{ "id": 122, "type": "RequestType", "problemTypeName": "Password", "hideSubject": 1 }""", WhdJson.Options)!;
        Assert.False(shown.HideSubject);
        Assert.True(hidden.HideSubject);
    }
}
