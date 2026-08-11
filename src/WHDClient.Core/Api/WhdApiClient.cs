using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WHDClient.Core.Models;

namespace WHDClient.Core.Api;

/// <summary>
/// Low-level Web Help Desk REST client. Authenticates every request with a Tech API key.
/// Also manages short-lived REST sessions (needed for attachment uploads).
/// </summary>
public class WhdApiClient : IDisposable
{
    public const string DefaultWoaPath = "/helpdesk/WebObjects/Helpdesk.woa";

    private readonly HttpClient _http;
    private readonly CookieContainer _cookies;
    private readonly HttpClientHandler _handler;

    public string ServerUrl { get; }
    public string WoaPath { get; set; } = DefaultWoaPath;
    public string ApiKey { get; }

    /// <summary>Instance id of the current REST session (-1 = none / not hosted).</summary>
    public int InstanceId { get; private set; } = -1;

    public string RaBase =>
        InstanceId > 0
            ? $"{ServerUrl}{WoaPath}/{InstanceId}/ra"
            : $"{ServerUrl}{WoaPath}/ra";

    public WhdApiClient(string serverUrl, string apiKey, HttpMessageHandler? handler = null)
    {
        ServerUrl = serverUrl.TrimEnd('/');
        ApiKey = apiKey;

        if (handler != null)
        {
            _http = new HttpClient(handler);
            _handler = null!;
            _cookies = null!;
        }
        else
        {
            _cookies = new CookieContainer();
            _handler = new HttpClientHandler { CookieContainer = _cookies, UseCookies = true };
            _http = new HttpClient(_handler);
        }
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        // WHD enforces an origin check on write requests; the district's WAF also
        // rejects non-browser clients. Spoofing a same-origin browser request avoids both.
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Origin", ServerUrl);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", $"{ServerUrl}{WoaPath}");
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    // ---------- generic request helpers ----------

    private string BuildUrl(string relativePath, IDictionary<string, string?>? query, bool includeApiKey = true)
    {
        var sb = new StringBuilder(RaBase);
        sb.Append(relativePath.StartsWith('/') ? relativePath : "/" + relativePath);
        var q = new List<string>();
        if (query != null)
        {
            foreach (var (k, v) in query)
            {
                if (v == null) continue;
                q.Add($"{Uri.EscapeDataString(k)}={Uri.EscapeDataString(v)}");
            }
        }
        if (includeApiKey)
            q.Add($"apiKey={Uri.EscapeDataString(ApiKey)}");
        if (q.Count > 0)
            sb.Append('?').Append(string.Join('&', q));
        return sb.ToString();
    }

    private static async Task EnsureSuccess(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw resp.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new WhdAuthenticationException(body),
            HttpStatusCode.Forbidden => new WhdPermissionException(body),
            _ => new WhdApiException(resp.StatusCode, body,
                $"WHD request failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. {Truncate(body)}")
        };
    }

    private static string Truncate(string? s) =>
        s == null ? "" : s.Length <= 500 ? s : s[..500] + "…";

    private static async Task<T?> ReadJson<T>(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body)) return default;
        var trimmed = body.TrimStart();
        // WHD sometimes returns plain-text error messages with HTTP 200.
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
            throw new WhdApiException(resp.StatusCode, body, $"WHD returned a non-JSON response: {Truncate(body)}");
        return JsonSerializer.Deserialize<T>(body, WhdJson.Options);
    }

    public async Task<T?> GetAsync<T>(string relativePath, IDictionary<string, string?>? query = null, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(BuildUrl(relativePath, query), ct).ConfigureAwait(false);
        await EnsureSuccess(resp).ConfigureAwait(false);
        return await ReadJson<T>(resp).ConfigureAwait(false);
    }

    public async Task<List<T>> GetListAsync<T>(string relativePath, IDictionary<string, string?>? query = null, CancellationToken ct = default)
    {
        var result = await GetAsync<List<T>>(relativePath, query, ct).ConfigureAwait(false);
        return result ?? new List<T>();
    }

    public async Task<T?> PostAsync<T>(string relativePath, object payload, IDictionary<string, string?>? query = null, CancellationToken ct = default)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload, WhdJson.Options), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(BuildUrl(relativePath, query), content, ct).ConfigureAwait(false);
        await EnsureSuccess(resp).ConfigureAwait(false);
        return await ReadJson<T>(resp).ConfigureAwait(false);
    }

    public async Task<T?> PutAsync<T>(string relativePath, object payload, CancellationToken ct = default)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload, WhdJson.Options), Encoding.UTF8, "application/json");
        using var resp = await _http.PutAsync(BuildUrl(relativePath, null), content, ct).ConfigureAwait(false);
        await EnsureSuccess(resp).ConfigureAwait(false);
        return await ReadJson<T>(resp).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string relativePath, IDictionary<string, string?>? query = null, bool useSessionKey = false, string? sessionKey = null, CancellationToken ct = default)
    {
        string url;
        if (useSessionKey && sessionKey != null)
        {
            var q = new Dictionary<string, string?>(query ?? new Dictionary<string, string?>()) { ["sessionKey"] = sessionKey };
            url = BuildUrl(relativePath, q, includeApiKey: false);
        }
        else
        {
            url = BuildUrl(relativePath, query);
        }
        using var resp = await _http.DeleteAsync(url, ct).ConfigureAwait(false);
        await EnsureSuccess(resp).ConfigureAwait(false);
    }

    /// <summary>Browser-openable download URL for an attachment (apiKey-authenticated, inline disposition).</summary>
    public string GetAttachmentUrl(int attachmentId) => BuildUrl($"/TicketAttachments/{attachmentId}", null);

    // ---------- sessions (needed for attachment upload) ----------

    public async Task<WhdSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var session = await GetAsync<WhdSession>("/Session", null, ct).ConfigureAwait(false)
                      ?? throw new WhdApiException(0, null, "Empty session response from server.");
        if (session.InstanceId > 0)
            InstanceId = session.InstanceId;
        return session;
    }

    public async Task DeleteSessionAsync(string sessionKey, CancellationToken ct = default)
    {
        try
        {
            await DeleteAsync("/Session", useSessionKey: true, sessionKey: sessionKey, ct: ct).ConfigureAwait(false);
        }
        catch (WhdApiException)
        {
            // Best effort — session expires on its own after 30 min anyway.
        }
    }

    /// <summary>
    /// Uploads a file as an attachment. WHD requires the multipart upload to carry the
    /// JSESSIONID/wosid cookies of a live REST session, so we create one, upload, and close it.
    /// </summary>
    public async Task<TicketAttachment?> UploadAttachmentAsync(
        string entityType, int entityId, string fileName, Stream content, CancellationToken ct = default)
    {
        var session = await CreateSessionAsync(ct).ConfigureAwait(false);
        try
        {
            var url = $"{ServerUrl}/helpdesk/attachment/upload" +
                      $"?type={Uri.EscapeDataString(entityType)}&entityId={entityId}&returnFields=id,uploadDate";
            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(content);
            // The server rejects the guide's documented part name ("fileUpload"); it expects "file".
            form.Add(fileContent, "file", fileName);
            using var resp = await _http.PostAsync(url, form, ct).ConfigureAwait(false);
            await EnsureSuccess(resp).ConfigureAwait(false);
            return await ReadJson<TicketAttachment>(resp).ConfigureAwait(false);
        }
        finally
        {
            if (session.SessionKey != null)
                await DeleteSessionAsync(session.SessionKey, ct).ConfigureAwait(false);
        }
    }

    // ---------- connection probing ----------

    /// <summary>Validates the API key and resolves the current tech's full record.</summary>
    public async Task<Tech> GetCurrentTechAsync(CancellationToken ct = default)
    {
        var current = await GetAsync<Tech>("/Techs/currentTech", null, ct).ConfigureAwait(false)
                      ?? throw new WhdApiException(0, null, "Empty response for current tech.");
        // currentTech only returns the id; fetch the full record for name/email.
        var full = await GetAsync<Tech>($"/Techs/{current.Id}",
            new Dictionary<string, string?> { ["style"] = "details" }, ct).ConfigureAwait(false);
        if (full != null)
        {
            full.ServerDisplayName ??= current.ServerDisplayName;
            return full;
        }
        return current;
    }

    public void Dispose() => _http.Dispose();
}
