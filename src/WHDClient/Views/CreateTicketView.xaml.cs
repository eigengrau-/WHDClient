using System.Windows;
using System.Windows.Controls;
using WHDClient.ViewModels;

namespace WHDClient.Views;

public partial class CreateTicketView : UserControl
{
    public CreateTicketView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CreateTicketViewModel oldVm) oldVm.ClientSearchCompleted -= OnClientSearchCompleted;
        if (e.NewValue is CreateTicketViewModel newVm) newVm.ClientSearchCompleted += OnClientSearchCompleted;
    }

    private void OnClientSearchCompleted()
    {
        // Focus the results dropdown and pop it open so the user can pick immediately.
        if (ClientCombo.Items.Count == 0) return;
        ClientCombo.Focus();
        ClientCombo.IsDropDownOpen = true;
    }

    private void DetailResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        DetailBox.Height = Math.Max(DetailBox.MinHeight, DetailBox.ActualHeight + e.VerticalChange);
    }
}
