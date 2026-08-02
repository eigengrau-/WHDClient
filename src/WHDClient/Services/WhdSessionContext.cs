using WHDClient.Core.Api;
using WHDClient.Core.Models;
using WHDClient.Core.Services;

namespace WHDClient.Services;

/// <summary>
/// Holds the authenticated API client and shared services for the app lifetime of one sign-in.
/// </summary>
public class WhdSessionContext
{
    public WhdApiClient Api { get; private set; } = null!;
    public TicketService Tickets { get; private set; } = null!;
    public LookupService Lookups { get; private set; } = null!;
    public Tech CurrentTech { get; private set; } = null!;
    public string ServerUrl { get; private set; } = AppSettings.DefaultServerUrl;

    public bool IsSignedIn { get; private set; }

    /// <summary>Demo mode (WHD_DEMO=1): the API client serves fabricated data instead of real calls.</summary>
    public static bool IsDemoMode { get; } =
        string.Equals(Environment.GetEnvironmentVariable("WHD_DEMO"), "1", StringComparison.OrdinalIgnoreCase);

    public event EventHandler? SignedIn;
    public event EventHandler? SignedOut;

    /// <summary>Validates the API key and establishes the session. Throws on failure.</summary>
    public async Task SignInAsync(string serverUrl, string apiKey, CancellationToken ct = default)
    {
        SignOut();
        var api = IsDemoMode
            ? new WhdApiClient(serverUrl, apiKey, new DemoDataHandler())
            : new WhdApiClient(serverUrl, apiKey);
        var tech = await api.GetCurrentTechAsync(ct); // throws WhdAuthenticationException on bad key

        Api = api;
        Tickets = new TicketService(api);
        Lookups = new LookupService(api);
        CurrentTech = tech;
        ServerUrl = IsDemoMode ? DemoDataHandler.DemoServerUrl : serverUrl;
        IsSignedIn = true;

        // Warm the lookup cache in the background; failures are non-fatal.
        _ = Task.Run(async () =>
        {
            try { await Lookups.EnsureLoadedAsync(ct: ct); } catch { /* lookups retried on demand */ }
        }, ct);

        SignedIn?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        IsSignedIn = false;
        Api?.Dispose();
        Api = null!;
        Tickets = null!;
        Lookups = null!;
        CurrentTech = null!;
        SignedOut?.Invoke(this, EventArgs.Empty);
    }
}
