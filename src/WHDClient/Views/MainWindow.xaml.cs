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
        // Keep the toggle's visual state in sync whenever the popup closes.
        Popup.Closed += (_, _) => NotifyToggle.IsChecked = false;
        // StaysOpen=False closes the popup on any outside click (including a click on the
        // toggle, which the popup's mouse capture routes through PopupRoot). Capture the
        // open state here, before that close, so the toggle's Click can tell "close" from
        // "open" without reopening.
        PreviewMouseLeftButtonDown += (_, _) => _notifyPopupWasOpen = Popup.IsOpen;
    }

    private bool _notifyPopupWasOpen;

    private void SidebarToggle_Checked(object sender, RoutedEventArgs e)
    {
        SidebarColumn.Width = new GridLength(SidebarCollapsedWidth);
        SidebarHeaderContent.Visibility = Visibility.Collapsed;
        SidebarNav.Visibility = Visibility.Collapsed;
        SidebarBottom.Visibility = Visibility.Collapsed;
        SidebarToggleGlyph.Text = "❯";
        SidebarToggle.ToolTip = "Expand sidebar";
        AutomationProperties.SetName(SidebarToggle, "Expand sidebar");
        // Notifications move into the collapsed bell; close any open popup anchored at the
        // now-hidden expanded toggle.
        CollapsedBell.Visibility = Visibility.Visible;
        if (Popup.IsOpen)
        {
            Popup.IsOpen = false;
            NotifyToggle.IsChecked = false;
        }
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
        CollapsedBell.Visibility = Visibility.Collapsed;
        Popup.PlacementTarget = NotifyToggle;
        Popup.HorizontalOffset = 0;
    }

    private void TicketNumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel vm && sender is TextBox box)
        {
            vm.OpenTicketByNumberCommand.Execute(box.Text);
            box.Text = "";
        }
    }

    /// <summary>Persist grid column layouts, open tabs, and any unsaved settings before the window goes away.</summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (App.Services.GetService(typeof(GridLayoutService)) is GridLayoutService layout)
            layout.CaptureAll();
        if (App.Services.GetService(typeof(SettingsService)) is SettingsService settings)
        {
            if (DataContext is MainViewModel vm)
            {
                settings.Settings.OpenTabs = vm.GetOpenTabKeys();
                settings.Settings.SelectedTab = vm.GetSelectedTabKey();
            }
            settings.Save();
        }
        base.OnClosing(e);
    }

    /// <summary>Opens/closes the notification popup anchored at the expanded toggle.</summary>
    private void NotifyToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_notifyPopupWasOpen)
        {
            // This click closed the popup (StaysOpen ran on mouse-down); keep the toggle off.
            NotifyToggle.IsChecked = false;
        }
        else
        {
            Popup.PlacementTarget = NotifyToggle;
            Popup.HorizontalOffset = 0;
            Popup.IsOpen = true;
        }
    }

    /// <summary>Expands the sidebar and opens the notification feed the normal way — the bell is
    /// just an indicator that unseen notifications exist while the sidebar is collapsed.</summary>
    private void CollapsedBell_Click(object sender, RoutedEventArgs e)
    {
        SidebarToggle.IsChecked = false; // expand; SidebarToggle_Unchecked restores the toggle and popup anchor
        Popup.PlacementTarget = NotifyToggle;
        Popup.HorizontalOffset = 0;
        Popup.IsOpen = true;
        NotifyToggle.IsChecked = true;
    }

    /// <summary>Removes every notification from the feed.</summary>
    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.Notifications.ClearAll();
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
