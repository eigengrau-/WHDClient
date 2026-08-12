using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WHDClient.Services;
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

    /// <summary>
    /// Discards a Cc search that was never converted into a recipient: if the typed
    /// username/email is still in the box when focus leaves, clear it. Clicking Find or
    /// the results dropdown keeps the text (the search/add flow is still in progress).
    /// </summary>
    private void CcSearchBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is not TicketTabViewModel vm) return;
        if (ReferenceEquals(e.NewFocus, FindCcButton) || ReferenceEquals(e.NewFocus, CcCombo)) return;
        if (e.NewFocus is DependencyObject d && CcCombo.IsAncestorOf(d)) return;
        vm.CcSearchText = "";
    }

    /// <summary>Deletes a template from the dropdown. Handled on mouse-down so the ✕ click
    /// does not also select the item (which would load the template into the reply box).</summary>
    private void TemplateDelete_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: ReplyTemplate template } && DataContext is TicketTabViewModel vm)
            vm.DeleteTemplateCommand.Execute(template);
    }
}
