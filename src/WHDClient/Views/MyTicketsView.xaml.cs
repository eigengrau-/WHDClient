using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WHDClient.ViewModels;

namespace WHDClient.Views;

public partial class MyTicketsView : UserControl
{
    private bool _defaultSortApplied;

    public MyTicketsView()
    {
        InitializeComponent();
    }

    private void Row_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is TicketRow row
            && DataContext is TicketListViewModelBase vm)
        {
            vm.OpenTicketCommand.Execute(row);
        }
    }

    /// <summary>Default sort: newest reported first; also shows the header arrow.</summary>
    private void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_defaultSortApplied || sender is not DataGrid grid) return;
        _defaultSortApplied = true;
        var col = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "ReportDate");
        if (col != null) col.SortDirection = ListSortDirection.Descending;
    }
}
