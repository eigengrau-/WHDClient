using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using WHDClient.Services;

namespace WHDClient.Behaviors;

/// <summary>
/// Attached property that opts a ticket DataGrid into column-layout persistence
/// (see <see cref="GridLayoutService"/>): saved order/widths are applied on load and
/// re-captured on unload and after every column reorder.
/// </summary>
public static class GridColumnPersistence
{
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.RegisterAttached("Key", typeof(string), typeof(GridColumnPersistence),
            new PropertyMetadata(null, OnKeyChanged));

    public static void SetKey(DependencyObject element, string? value) =>
        element.SetValue(KeyProperty, value);

    public static string? GetKey(DependencyObject element) =>
        (string?)element.GetValue(KeyProperty);

    private static GridLayoutService Service => App.Services.GetRequiredService<GridLayoutService>();

    private static void OnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        grid.Loaded += OnLoaded;
        grid.Unloaded += OnUnloaded;
        grid.ColumnReordered += OnColumnReordered;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        Service.Apply(grid, GetKey(grid));
        Service.Register(grid);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        Service.Capture(grid, GetKey(grid));
        Service.Unregister(grid);
    }

    private static void OnColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (sender is DataGrid grid) Service.Capture(grid, GetKey(grid));
    }
}
