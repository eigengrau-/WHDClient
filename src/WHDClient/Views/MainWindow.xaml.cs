using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WHDClient.ViewModels;

namespace WHDClient.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
        if (sender is FrameworkElement el && el.Tag is int ticketId && DataContext is MainViewModel vm)
        {
            vm.Notifications.MarkAllRead();
            vm.OpenTicket(ticketId);
            NotifyToggle.IsChecked = false;
        }
    }
}
