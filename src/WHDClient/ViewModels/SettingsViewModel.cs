using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WHDClient.Core.Api;
using WHDClient.Services;

namespace WHDClient.ViewModels;

public partial class SettingsViewModel : TabViewModelBase
{
    private readonly SettingsService _settings;
    private readonly UpdateService _updates;
    private readonly Action _signOut;

    public override bool IsClosable => true;

    public SettingsViewModel(SettingsService settings, WhdSessionContext session, UpdateService updates, Action signOut,
        GridLayoutService gridLayout)
    {
        Header = "Settings";
        IconSource = "pack://application:,,,/Assets/icons/settings.png";
        _settings = settings;
        _updates = updates;
        _signOut = signOut;

        foreach (var h in GridLayoutService.KnownHeaders)
            ColumnToggles.Add(new ColumnToggleItem(h, gridLayout.IsColumnVisible(h), gridLayout));

        ServerUrl = WhdSessionContext.IsDemoMode ? DemoDataHandler.DemoServerUrl : settings.Settings.ServerUrl;
        PollIntervalSeconds = settings.Settings.PollIntervalSeconds;
        PageSize = settings.Settings.PageSize;
        NotificationsEnabled = settings.Settings.NotificationsEnabled;
        NotifyAssignedToMe = settings.Settings.NotifyAssignedToMe;
        NotifyMyTicketUpdated = settings.Settings.NotifyMyTicketUpdated;
        NotifyNewMatching = settings.Settings.NotifyNewMatching;
        FontScale = settings.Settings.FontScale;
        Theme = settings.Settings.Theme;
        HasRememberedKey = settings.GetApiKey() != null;
        CurrentUserText = $"Signed in as {session.CurrentTech?.DisplayName} ({session.CurrentTech?.Email ?? session.CurrentTech?.Username})";
    }

    [ObservableProperty] private string _serverUrl;
    [ObservableProperty] private int _pollIntervalSeconds;
    [ObservableProperty] private int _pageSize;
    [ObservableProperty] private bool _notificationsEnabled;
    [ObservableProperty] private bool _notifyAssignedToMe;
    [ObservableProperty] private bool _notifyMyTicketUpdated;
    [ObservableProperty] private bool _notifyNewMatching;
    [ObservableProperty] private string _theme;
    [ObservableProperty] private string _fontScale;
    [ObservableProperty] private bool _hasRememberedKey;
    [ObservableProperty] private string _currentUserText = "";
    [ObservableProperty] private string _savedMessage = "";
    [ObservableProperty] private string _updateStatus = "";
    [ObservableProperty] private string? _updateUrl;
    [ObservableProperty] private bool _updateCheckInProgress;

    public string CurrentVersionText => $"Version {UpdateService.CurrentVersion}";

    public ObservableCollection<SavedFilter> AlertFilters => new(_settings.Settings.SavedFilters);

    /// <summary>One checkbox per ticket-grid column (Settings > Ticket list columns).</summary>
    public ObservableCollection<ColumnToggleItem> ColumnToggles { get; } = new();

    /// <summary>Available theme names, shown in the Appearance section.</summary>
    public string[] ThemeOptions { get; } = { ThemeService.DarkTheme, ThemeService.LightTheme };

    /// <summary>Available font-size presets, shown in the Appearance section.</summary>
    public string[] FontScaleOptions { get; } = { ThemeService.SmallScale, ThemeService.MediumScale, ThemeService.LargeScale };

    partial void OnThemeChanged(string value)
    {
        if (!WhdSessionContext.IsDemoMode) _settings.Settings.Theme = value;
        ThemeService.Apply(value, FontScale);
    }

    partial void OnFontScaleChanged(string value)
    {
        if (!WhdSessionContext.IsDemoMode) _settings.Settings.FontScale = value;
        ThemeService.Apply(Theme, value);
    }

    partial void OnServerUrlChanged(string value)
    {
        if (!WhdSessionContext.IsDemoMode) _settings.Settings.ServerUrl = value;
    }
    partial void OnPollIntervalSecondsChanged(int value) => _settings.Settings.PollIntervalSeconds = value;
    partial void OnPageSizeChanged(int value) => _settings.Settings.PageSize = value;

    /// <summary>Allowed tickets-per-page choices.</summary>
    public int[] PageSizeOptions { get; } = { 25, 50, 100 };
    partial void OnNotificationsEnabledChanged(bool value) => _settings.Settings.NotificationsEnabled = value;
    partial void OnNotifyAssignedToMeChanged(bool value) => _settings.Settings.NotifyAssignedToMe = value;
    partial void OnNotifyMyTicketUpdatedChanged(bool value) => _settings.Settings.NotifyMyTicketUpdated = value;
    partial void OnNotifyNewMatchingChanged(bool value) => _settings.Settings.NotifyNewMatching = value;

    [RelayCommand]
    private void Save()
    {
        _settings.Save();
        SavedMessage = $"Saved {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    private void SaveAlertFilters()
    {
        _settings.Save();
        OnPropertyChanged(nameof(AlertFilters));
    }

    [RelayCommand]
    private void ForgetApiKey()
    {
        if (WhdSessionContext.IsDemoMode) return; // never touch the real saved key in demo mode
        _settings.ClearApiKey();
        _settings.Save();
        HasRememberedKey = false;
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (UpdateCheckInProgress) return;
        UpdateCheckInProgress = true;
        UpdateStatus = "Checking for updates…";
        UpdateUrl = null;
        try
        {
            var (update, succeeded) = await _updates.CheckForUpdateAsync();
            if (!succeeded)
            {
                UpdateStatus = "Update check failed — GitHub unreachable.";
            }
            else if (update != null)
            {
                UpdateStatus = $"Version {update.Version} is available.";
                UpdateUrl = update.Url;
            }
            else
            {
                UpdateStatus = "You're up to date.";
            }
        }
        finally
        {
            UpdateCheckInProgress = false;
        }
    }

    [RelayCommand]
    private void OpenUpdateDownload()
    {
        if (UpdateUrl != null)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UpdateUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void SignOut() => _signOut();
}

/// <summary>One row in Settings > Ticket list columns: a column header with a visibility checkbox.</summary>
public partial class ColumnToggleItem : ObservableObject
{
    private readonly GridLayoutService _layout;

    public string Name { get; }

    [ObservableProperty] private bool _isVisible;

    public ColumnToggleItem(string name, bool isVisible, GridLayoutService layout)
    {
        Name = name;
        _layout = layout;
        _isVisible = isVisible; // field assignment: don't fire the setter during init
    }

    partial void OnIsVisibleChanged(bool value) => _layout.SetColumnVisible(Name, value);
}
