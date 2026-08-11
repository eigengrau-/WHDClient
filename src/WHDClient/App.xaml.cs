using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Uwp.Notifications;
using WHDClient.Core.Api;
using WHDClient.Services;
using WHDClient.ViewModels;
using WHDClient.Views;

namespace WHDClient;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Ticket id from a toast clicked before the main window was ready (cold start).</summary>
    private static int? _pendingToastTicketId;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WHDClient", "startup.log");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { /* diagnostics must never crash the app */ }
    }

    public App()
    {
        // Blank-white windows on some multi-monitor / USB-dock (DisplayLink) setups
        // are a WPF hardware-rendering issue; software rendering avoids it.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try { File.Delete(LogPath); } catch { }
        Log("OnStartup entered");

        DispatcherUnhandledException += (_, args) =>
        {
            Log($"UNHANDLED: {args.Exception}");
            MessageBox.Show(args.Exception.ToString(), "WHDClient error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log($"FATAL: {args.ExceptionObject}");

        base.OnStartup(e);
        Log("base.OnStartup done");

        // Toast click activation: toasts carrying a "url" argument (e.g. update available)
        // open that link in the default browser; "ticketId" opens the ticket in the app.
        // Must be registered before any toast is shown.
        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            try
            {
                Log($"toast activated: {toastArgs.Argument}");
                var args = ToastArguments.Parse(toastArgs.Argument);
                if (args.TryGetValue("url", out var url) && !string.IsNullOrWhiteSpace(url))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (args.TryGetValue("ticketId", out var ticketId) && int.TryParse(ticketId, out var id))
                {
                    Application.Current?.Dispatcher.Invoke(() => OpenTicketFromToast(id));
                }
            }
            catch (Exception ex)
            {
                Log($"toast activation failed: {ex.Message}");
            }
        };

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<WhdSessionContext>();
            services.AddSingleton<PollingService>();
            services.AddSingleton<NotificationService>();
            services.AddSingleton<UpdateService>();
            services.AddSingleton<GridLayoutService>();
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();
            Services = services.BuildServiceProvider();
            Log("DI built");

            var settings = Services.GetRequiredService<SettingsService>();
            settings.Load();
            Log($"settings loaded; hasKey={settings.GetApiKey() != null}");

            // Apply the saved theme/font before any window is created so the first paint is correct.
            ThemeService.Apply(settings.Settings.Theme, settings.Settings.FontScale);
            Log($"theme={ThemeService.CurrentTheme} fontScale={ThemeService.CurrentFontScale}");

            // Demo mode: replace anything user-specific in memory (Save is a no-op in demo).
            if (WhdSessionContext.IsDemoMode)
            {
                settings.Settings.ServerUrl = DemoDataHandler.DemoServerUrl;
                settings.Settings.BookmarkedTicketIds = new List<int> { 1001, 1003, 1008 };
                settings.Settings.SavedFilters = new List<SavedFilter>
                {
                    new() { Name = "Chromebook repairs", Qualifier = "(problemType.id = 211)", AlertOnNew = true },
                    new() { Name = "Urgent tickets", Qualifier = "(prioritytype.id = 4)", AlertOnNew = true },
                };
            }

            var loginVm = Services.GetRequiredService<LoginViewModel>();
            // With a remembered key, sign in silently and open the main window directly —
            // the login window is only shown when there is no key or sign-in fails.
            if (!await loginVm.TryAutoSignInAsync())
            {
                Log("auto sign-in skipped/failed; showing LoginWindow");
                var login = new LoginWindow
                {
                    DataContext = loginVm
                };
                login.Show();
                Log("LoginWindow shown");
            }

            // A toast clicked while the app was cold-starting queued a ticket to open.
            if (_pendingToastTicketId is int pending)
            {
                _pendingToastTicketId = null;
                _ = Application.Current.Dispatcher.InvokeAsync(() => OpenTicketFromToast(pending));
            }
        }
        catch (Exception ex)
        {
            Log($"STARTUP FAILURE: {ex}");
            MessageBox.Show(ex.ToString(), "WHDClient failed to start",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>Opens a ticket requested via toast click, once the app is up.</summary>
    private static void OpenTicketFromToast(int ticketId)
    {
        try
        {
            if (Services?.GetService<MainViewModel>() is { } main && Current?.MainWindow != null)
                main.OpenTicket(ticketId);
            else
                _pendingToastTicketId = ticketId;
        }
        catch (Exception ex)
        {
            Log($"open ticket from toast failed: {ex.Message}");
        }
    }
}
