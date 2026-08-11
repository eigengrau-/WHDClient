using System.Text.Json;
using WHDClient.Core.Api;
using WHDClient.Core.Models;
using Xunit;

namespace WHDClient.Core.Tests;

public class TicketEmbeddedNotesTests
{
    [Fact]
    public void EmbeddedNotes_DeserializeWithAttachments()
    {
        // Shape observed in a live style=details ticket response (ticket 301132).
        const string json = """
            {
              "id": 301132, "type": "Ticket", "subject": "x",
              "notes": [
                { "id": 423725, "type": "TechNote", "isTechNote": true,
                  "attachments": [ { "id": 52758, "type": "TicketAttachment", "fileName": "log.txt", "sizeString": "36.48 KB" } ] },
                { "id": 423711, "type": "TechNote" }
              ]
            }
            """;
        var t = JsonSerializer.Deserialize<Ticket>(json, WhdJson.Options)!;
        Assert.Equal(2, t.EmbeddedNotes!.Count);
        var att = Assert.Single(t.EmbeddedNotes[0].Attachments!);
        Assert.Equal(52758, att.Id);
        Assert.Equal("log.txt", att.FileName);
        Assert.Null(t.EmbeddedNotes[1].Attachments);
    }
}
