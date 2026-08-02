using System.Windows;
using WHDClient.ViewModels;

namespace WHDClient.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            // Pre-fill (masked) when a remembered key exists.
            if (DataContext is LoginViewModel vm && !string.IsNullOrEmpty(vm.ApiKey))
                ApiKeyBox.Password = vm.ApiKey;
        };
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm) vm.ApiKey = ApiKeyBox.Password;
    }
}
