using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using WHDClient.Services;
using WHDClient.ViewModels;
namespace WHDClient.Views;

public partial class MainWindow : Window
{
    /// <summary>Sidebar width when expanded (must match the first ColumnDefinition in MainWindow.xaml).</summary>
    private const double SidebarExpandedWidth = 210;
    /// <summary>Sidebar width when collapsed — just enough for the expand button.</summary>
    private const double SidebarCollapsedWidth = 40;

    public MainWindow()
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
    }

    private void SidebarToggle_Checked(object sender, RoutedEventArgs e)
    {
        SidebarColumn.Width = new GridLength(SidebarCollapsedWidth);
        SidebarHeaderContent.Visibility = Visibility.Collapsed;
        SidebarNav.Visibility = Visibility.Collapsed;
        SidebarBottom.Visibility = Visibility.Collapsed;
        SidebarToggleGlyph.Text = "❯";
        SidebarToggle.ToolTip = "Expand sidebar";
        AutomationProperties.SetName(SidebarToggle, "Expand sidebar");
    }

    private void SidebarToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        SidebarColumn.Width = new GridLength(SidebarExpandedWidth);
        SidebarHeaderContent.Visibility = Visibility.Visible;
        SidebarNav.Visibility = Visibility.Visible;
        SidebarBottom.Visibility = Visibility.Visible;
        SidebarToggleGlyph.Text = "❮";
        SidebarToggle.ToolTip = "Collapse sidebar";
        AutomationProperties.SetName(SidebarToggle, "Collapse sidebar");
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
            // Only the clicked notification is dismissed; the rest of the feed stays.
            vm.Notifications.Dismiss(notification);
            if (notification.Url != null)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(notification.Url) { UseShellExecute = true });
            else if (notification.TicketId is int ticketId)
                vm.OpenTicket(ticketId);
        }
    }
}
