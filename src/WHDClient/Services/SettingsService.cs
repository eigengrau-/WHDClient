using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WHDClient.Services;

public class SavedFilter
{
    public string Name { get; set; } = "";
    public string Qualifier { get; set; } = "";
    /// <summary>When true, new tickets matching this filter raise a notification.</summary>
    public bool AlertOnNew { get; set; }
}

/// <summary>One column's saved layout on a ticket grid (order + width; visibility is global).</summary>
public class GridColumnState
{
    public string Header { get; set; } = "";
    public int DisplayIndex { get; set; }
    /// <summary>DataGridLength as text ("60", "3*", "SizeToHeader", ...).</summary>
    public string? Width { get; set; }
}

/// <summary>A saved reply template: a name and the BBCode text it expands to.</summary>
public class ReplyTemplate
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
}

public class AppSettings
{
    public const string DefaultServerUrl = "";

    public string ServerUrl { get; set; } = DefaultServerUrl;
    /// <summary>DPAPI-encrypted (CurrentUser) API key, base64.</summary>
    public string? ProtectedApiKey { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;
    /// <summary>Tickets fetched per page on list pages (25/50/100).</summary>
    public int PageSize { get; set; } = 25;
    public bool NotificationsEnabled { get; set; } = true;
    public bool NotifyAssignedToMe { get; set; } = true;
    public bool NotifyMyTicketUpdated { get; set; } = true;
    public bool NotifyNewMatching { get; set; } = true;
    /// <summary>Theme name ("Dark" or "Light").</summary>
    public string Theme { get; set; } = "Dark";
    /// <summary>Font size preset: "Small", "Medium", or "Large".</summary>
    public string FontScale { get; set; } = ThemeService.MediumScale;
    public List<SavedFilter> SavedFilters { get; set; } = new();
    /// <summary>Ticket ids the user has bookmarked.</summary>
    public List<int> BookmarkedTicketIds { get; set; } = new();
    /// <summary>Column headers hidden on all ticket grids (Settings > Ticket list columns).</summary>
    public HashSet<string> HiddenGridColumns { get; set; } = new();
    /// <summary>Saved per-grid column layouts (order + width), keyed by grid name.</summary>
    public Dictionary<string, List<GridColumnState>> GridColumnLayouts { get; set; } = new();
    /// <summary>Tabs open when the app was closed ("search"/"queue"/"bookmarks"/"settings"/"newticket"/"ticket:&lt;id&gt;"), in display order.</summary>
    public List<string> OpenTabs { get; set; } = new();
    /// <summary>The tab selected when the app was closed (same key format as OpenTabs).</summary>
    public string? SelectedTab { get; set; }
    /// <summary>Saved reply templates (name + BBCode text).</summary>
    public List<ReplyTemplate> ReplyTemplates { get; set; } = new();
}

/// <summary>Loads/saves settings JSON in %APPDATA%\WHDClient; API key is DPAPI-protected.</summary>
public class SettingsService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WHDClient");
    private static readonly string SettingsPath = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        if (WhdSessionContext.IsDemoMode) return; // demo sessions never touch real settings
        Directory.CreateDirectory(Dir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, JsonOpts));
    }

    public void SetApiKey(string apiKey)
    {
        var bytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey), null, DataProtectionScope.CurrentUser);
        Settings.ProtectedApiKey = Convert.ToBase64String(bytes);
    }

    public string? GetApiKey()
    {
        if (string.IsNullOrEmpty(Settings.ProtectedApiKey)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(Settings.ProtectedApiKey), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    public void ClearApiKey() => Settings.ProtectedApiKey = null;
}
