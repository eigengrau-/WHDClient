using System.Windows;
using System.Windows.Controls;
using WHDClient.ViewModels;

namespace WHDClient.Views;

public partial class TicketTabView : UserControl
{
    public TicketTabView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TicketTabViewModel oldVm) oldVm.CcSearchCompleted -= OnCcSearchCompleted;
        if (e.NewValue is TicketTabViewModel newVm) newVm.CcSearchCompleted += OnCcSearchCompleted;
    }

    private void OnCcSearchCompleted()
    {
        // Focus the results dropdown and pop it open so the user can pick immediately.
        if (CcCombo.Items.Count == 0) return;
        CcCombo.Focus();
        CcCombo.IsDropDownOpen = true;
    }
}
