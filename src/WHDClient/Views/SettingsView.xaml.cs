using System.Windows;
using System.Windows.Controls;
using WHDClient.Services;
using WHDClient.ViewModels;

namespace WHDClient.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void AlertFilter_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.SaveAlertFiltersCommand.Execute(null);
    }
}
