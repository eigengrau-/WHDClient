using System.Net.Http;
using WHDClient.Core.Api;
using WHDClient.Core.Models;
using Xunit;

namespace WHDClient.Core.Tests;

public class DemoDataHandlerTests
{
    private static async Task<List<Ticket>> SearchAsync(string? qualifier)
    {
        using var handler = new DemoDataHandler();
        using var api = new WhdApiClient(DemoDataHandler.DemoServerUrl, "DEMO", handler);
        return await api.GetListAsync<Ticket>("/Tickets", new Dictionary<string, string?>
        {
            ["qualifier"] = qualifier
        });
    }

    [Fact]
    public async Task Search_ClientTechNull_ReturnsOnlyUnassignedTickets()
    {
        var unassigned = await SearchAsync("(clientTech = null)");

        Assert.NotEmpty(unassigned);
        Assert.All(unassigned, t => Assert.Null(t.ClientTech));
    }

    [Fact]
    public async Task Search_NoQualifier_ReturnsAllTickets()
    {
        var all = await SearchAsync(null);
        var allUnassigned = all.Count(t => t.ClientTech == null);

        // The demo pool has unassigned tickets, so the unfiltered result must include them.
        Assert.NotEqual(all.Count, allUnassigned);
        Assert.NotEmpty(all);
    }

    [Fact]
    public async Task UpdateTicket_ClientTechNull_UnassignsTech()
    {
        using var handler = new DemoDataHandler();
        using var api = new WhdApiClient(DemoDataHandler.DemoServerUrl, "DEMO", handler);

        // #1001 starts assigned to tech 1.
        var before = await api.GetAsync<Ticket>("/Tickets/1001");
        Assert.NotNull(before!.ClientTech);

        await api.PutAsync<Ticket>("/Tickets/1001", new Dictionary<string, object?> { ["clientTech"] = null });

        var after = await api.GetAsync<Ticket>("/Tickets/1001");
        Assert.Null(after!.ClientTech);
    }
}
