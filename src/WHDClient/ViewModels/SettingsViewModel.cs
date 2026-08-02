using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WHDClient.Services;

namespace WHDClient.ViewModels;

public partial class SettingsViewModel : TabViewModelBase
{
    private readonly SettingsService _settings;
    private readonly UpdateService _updates;
    private readonly Action _signOut;

    public override bool IsClosable => true;

    public SettingsViewModel(SettingsService settings, WhdSessionContext session, UpdateService updates, Action signOut)
    {
        Header = "Settings";
        IconSource = "pack://application:,,,/Assets/icons/settings.png";
        _settings = settings;
        _updates = updates;
        _signOut = signOut;

        ServerUrl = settings.Settings.ServerUrl;
        PollIntervalSeconds = settings.Settings.PollIntervalSeconds;
        PageSize = settings.Settings.PageSize;
        NotificationsEnabled = settings.Settings.NotificationsEnabled;
        NotifyAssignedToMe = settings.Settings.NotifyAssignedToMe;
        NotifyMyTicketUpdated = settings.Settings.NotifyMyTicketUpdated;
        NotifyNewMatching = settings.Settings.NotifyNewMatching;
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
    [ObservableProperty] private bool _hasRememberedKey;
    [ObservableProperty] private string _currentUserText = "";
    [ObservableProperty] private string _savedMessage = "";
    [ObservableProperty] private string _updateStatus = "";
    [ObservableProperty] private string? _updateUrl;
    [ObservableProperty] private bool _updateCheckInProgress;

    public string CurrentVersionText => $"Version {UpdateService.CurrentVersion}";

    public ObservableCollection<SavedFilter> AlertFilters => new(_settings.Settings.SavedFilters);

    partial void OnServerUrlChanged(string value) => _settings.Settings.ServerUrl = value;
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
