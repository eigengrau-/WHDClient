using System.Net;
using System.Text;
using WHDClient.Core.Api;
using WHDClient.Core.Models;
using Xunit;

namespace WHDClient.Core.Tests;

public class WhdApiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public HttpRequestMessage? LastRequest;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responder(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetAsync_AppendsApiKeyAndParsesJson()
    {
        var handler = new StubHandler(_ => Json("{\"type\":\"Tech\",\"id\":7,\"firstName\":\"Ada\",\"lastName\":\"Lovelace\"}"));
        using var api = new WhdApiClient("https://whd.example.com", "KEY123", handler);

        var tech = await api.GetAsync<Tech>("/Techs/currentTech");

        Assert.Equal(7, tech!.Id);
        Assert.Equal("Ada Lovelace", tech.DisplayName);
        Assert.Contains("apiKey=KEY123", handler.LastRequest!.RequestUri!.Query);
        Assert.StartsWith("https://whd.example.com/helpdesk/WebObjects/Helpdesk.woa/ra/Techs/currentTech",
            handler.LastRequest.RequestUri!.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public async Task GetListAsync_ParsesArray()
    {
        var handler = new StubHandler(_ => Json("[{\"id\":1,\"subject\":\"a\"},{\"id\":2,\"subject\":\"b\"}]"));
        using var api = new WhdApiClient("https://whd.example.com", "K", handler);

        var tickets = await api.GetListAsync<Ticket>("/Tickets/mine");

        Assert.Equal(2, tickets.Count);
        Assert.Equal("b", tickets[1].Subject);
    }

    [Fact]
    public async Task Unauthorized_ThrowsWhdAuthenticationException()
    {
        var handler = new StubHandler(_ => Json("Authentication failure.", HttpStatusCode.Unauthorized));
        using var api = new WhdApiClient("https://whd.example.com", "BAD", handler);

        await Assert.ThrowsAsync<WhdAuthenticationException>(() => api.GetAsync<Tech>("/Techs/currentTech"));
    }

    [Fact]
    public async Task Forbidden_ThrowsWhdPermissionException()
    {
        var handler = new StubHandler(_ => Json("Forbidden.", HttpStatusCode.Forbidden));
        using var api = new WhdApiClient("https://whd.example.com", "K", handler);

        await Assert.ThrowsAsync<WhdPermissionException>(() => api.GetAsync<Tech>("/Techs/1"));
    }

    [Fact]
    public async Task QualifierValueIsUrlEncoded()
    {
        var handler = new StubHandler(_ => Json("[]"));
        using var api = new WhdApiClient("https://whd.example.com", "K", handler);

        await api.GetListAsync<Ticket>("/Tickets", new Dictionary<string, string?>
        {
            ["qualifier"] = "(statustype.statusTypeName = 'Open')"
        });

        var q = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("qualifier=", q);
        Assert.DoesNotContain("'Open'", q); // must be encoded
        Assert.Contains("Open", Uri.UnescapeDataString(q));
    }

    [Fact]
    public async Task ParsesWhdDateFormats()
    {
        var handler = new StubHandler(_ => Json(
            "{\"id\":5,\"lastUpdatedUtc\":\"2026-07-30T18:22:10Z\",\"reportDateUtc\":\"2026-07-30 18:22:10.123+00\"}"));
        using var api = new WhdApiClient("https://whd.example.com", "K", handler);

        var t = await api.GetAsync<Ticket>("/Tickets/5");

        Assert.Equal(new DateTimeOffset(2026, 7, 30, 18, 22, 10, TimeSpan.Zero), t!.LastUpdatedUtc);
        Assert.NotNull(t.ReportDateUtc);
    }
}
