using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WHDClient.Services;
using WHDClient.ViewModels;
namespace WHDClient.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
    }

    private void TicketNumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel vm && sender is TextBox box)
        {
            vm.OpenTicketByNumberCommand.Execute(box.Text);
            box.Text = "";
        }
    }

    private void Notification_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is AppNotification notification && DataContext is MainViewModel vm)
        {
            vm.Notifications.MarkAllRead();
            NotifyToggle.IsChecked = false;
            if (notification.Url != null)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(notification.Url) { UseShellExecute = true });
            else if (notification.TicketId is int ticketId)
                vm.OpenTicket(ticketId);
        }
    }
}
