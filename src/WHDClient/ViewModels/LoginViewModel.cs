using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WHDClient.Core.Api;
using WHDClient.Services;
using WHDClient.Views;

namespace WHDClient.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly WhdSessionContext _session;

    [ObservableProperty]
    private string _serverUrl;

    [ObservableProperty]
    private string _apiKey = "";

    [ObservableProperty]
    private bool _rememberKey = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public LoginViewModel(SettingsService settings, WhdSessionContext session)
    {
        _settings = settings;
        _session = session;
        _serverUrl = WhdSessionContext.IsDemoMode ? DemoDataHandler.DemoServerUrl : settings.Settings.ServerUrl;
    }

    /// <summary>
    /// Attempts a silent sign-in with the remembered key (no login window shown).
    /// Returns true when the session is established; the caller shows the login window otherwise.
    /// </summary>
    public async Task<bool> TryAutoSignInAsync()
    {
        var saved = _settings.GetApiKey();
        if (string.IsNullOrEmpty(saved)) return false;
        ApiKey = saved;
        await SignInAsync();
        return _session.IsSignedIn;
    }

    [RelayCommand]
    private void OpenApiKeyPage()
    {
        var server = ServerUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ErrorMessage = "Enter your Web Help Desk URL first (e.g. https://webhelpdesk.example.com).";
            return;
        }
        ErrorMessage = null;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            $"{server}/helpdesk/WebObjects/Helpdesk.woa/wa/Nav?path=setup-techs-myaccount") { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            ErrorMessage = "Enter your Tech API key.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _session.SignInAsync(ServerUrl.Trim(), ApiKey.Trim());

            // Demo mode must never overwrite the real saved URL/key.
            if (!WhdSessionContext.IsDemoMode)
            {
                _settings.Settings.ServerUrl = ServerUrl.Trim();
                if (RememberKey) _settings.SetApiKey(ApiKey.Trim());
                else _settings.ClearApiKey();
                _settings.Save();
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var main = new MainWindow
                {
                    DataContext = App.Services.GetRequiredService<MainViewModel>()
                };
                Application.Current.MainWindow = main;
                main.Show();
                foreach (Window w in Application.Current.Windows)
                {
                    if (w is LoginWindow) w.Close();
                }
            });
        }
        catch (WhdAuthenticationException)
        {
            // Shown when the login window appears after a failed silent attempt too.
            ErrorMessage = "Authentication failed. Check your API key.";
            _settings.ClearApiKey();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or WhdApiException)
        {
            ErrorMessage = $"Could not connect: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
