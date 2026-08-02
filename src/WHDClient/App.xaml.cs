using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WHDClient.Services;
using WHDClient.ViewModels;
using WHDClient.Views;

namespace WHDClient;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

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

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<WhdSessionContext>();
            services.AddSingleton<PollingService>();
            services.AddSingleton<NotificationService>();
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();
            Services = services.BuildServiceProvider();
            Log("DI built");

            var settings = Services.GetRequiredService<SettingsService>();
            settings.Load();
            Log($"settings loaded; hasKey={settings.GetApiKey() != null}");

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
        }
        catch (Exception ex)
        {
            Log($"STARTUP FAILURE: {ex}");
            MessageBox.Show(ex.ToString(), "WHDClient failed to start",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
