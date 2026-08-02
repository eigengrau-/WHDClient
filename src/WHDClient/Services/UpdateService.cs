using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace WHDClient.Services;

public record UpdateInfo(Version Version, string Url);

/// <summary>
/// Checks the project's GitHub releases for a newer version of the app.
/// </summary>
public class UpdateService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/eigengrau-/WHDClient/releases/latest";
    public const string ReleasesPageUrl = "https://github.com/eigengrau-/WHDClient/releases";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub's API requires a User-Agent header.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WHDClient");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>The running app's version (Major.Minor.Build), from the assembly.</summary>
    public static Version CurrentVersion
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }

    /// <summary>
    /// Checks GitHub for the latest release. <c>Succeeded</c> is false when the check itself
    /// failed (offline, rate-limited, repo private…); <c>Update</c> is non-null only when a
    /// newer version exists. An update check must never break the app, so errors are caught.
    /// </summary>
    public async Task<(UpdateInfo? Update, bool Succeeded)> CheckForUpdateAsync()
    {
        // Dev/test hook: WHD_FAKE_UPDATE=1.2.3 simulates an available update.
        var fake = Environment.GetEnvironmentVariable("WHD_FAKE_UPDATE");
        if (!string.IsNullOrEmpty(fake) && Version.TryParse(fake, out var fakeVersion))
        {
            await Task.Delay(300); // keep the async path realistic
            return (new UpdateInfo(fakeVersion, ReleasesPageUrl), true);
        }

        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(LatestReleaseApi));
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var url = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed)) return (null, true);
            var latest = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
            return (latest > CurrentVersion ? new UpdateInfo(latest, url ?? ReleasesPageUrl) : null, true);
        }
        catch
        {
            return (null, false);
        }
    }
}
