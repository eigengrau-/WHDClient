using System.Windows;
using System.Windows.Controls;
using WHDClient.Behaviors;

namespace WHDClient.Services;

/// <summary>
/// Persists ticket-grid column layouts: per-grid column order and widths (restored on
/// startup) plus a global hidden-column set toggled from Settings. Grids opt in via the
/// <see cref="GridColumnPersistence.Key"/> attached property; layouts live in settings.json.
/// </summary>
public class GridLayoutService
{
    /// <summary>All toggleable column headers across the ticket grids.</summary>
    public static readonly string[] KnownHeaders =
        { "#", "Subject", "Client", "Status", "Priority", "Tech", "Location", "Reported", "Updated" };

    private readonly SettingsService _settings;
    private static readonly List<WeakReference<DataGrid>> _live = new();

    public GridLayoutService(SettingsService settings) => _settings = settings;

    public bool IsColumnVisible(string header) => !_settings.Settings.HiddenGridColumns.Contains(header);

    /// <summary>Shows/hides a column header on all grids (live) and records the choice in settings.</summary>
    public void SetColumnVisible(string header, bool visible)
    {
        var hidden = _settings.Settings.HiddenGridColumns;
        if (visible) hidden.Remove(header); else hidden.Add(header);
        lock (_live)
        {
            foreach (var grid in LiveGrids()) ApplyVisibility(grid);
        }
    }

    /// <summary>Applies the saved layout (order + width) and column visibility to a grid.</summary>
    public void Apply(DataGrid grid, string? key)
    {
        ApplyVisibility(grid);
        if (key == null || !_settings.Settings.GridColumnLayouts.TryGetValue(key, out var saved)) return;

        foreach (var col in grid.Columns)
        {
            var state = saved.FirstOrDefault(s => s.Header == HeaderOf(col));
            if (state?.Width is { } w && TryParseLength(w, out var len)) col.Width = len;
        }
        // Assign in ascending saved order so WPF resolves collisions deterministically.
        foreach (var state in saved.OrderBy(s => s.DisplayIndex))
        {
            var col = grid.Columns.FirstOrDefault(c => HeaderOf(c) == state.Header);
            if (col != null) col.DisplayIndex = state.DisplayIndex;
        }
    }

    /// <summary>Records the grid's current column order and widths in settings (in memory; persisted on save).</summary>
    public void Capture(DataGrid grid, string? key)
    {
        if (key == null) return;
        _settings.Settings.GridColumnLayouts[key] = grid.Columns
            .Where(c => HeaderOf(c).Length > 0)
            .Select(c => new GridColumnState
            {
                Header = HeaderOf(c),
                DisplayIndex = c.DisplayIndex,
                Width = c.Width.ToString()
            })
            .ToList();
    }

    public void Register(DataGrid grid)
    {
        lock (_live)
        {
            _live.RemoveAll(r => !r.TryGetTarget(out _));
            if (!_live.Any(r => r.TryGetTarget(out var g) && ReferenceEquals(g, grid)))
                _live.Add(new WeakReference<DataGrid>(grid));
        }
    }

    public void Unregister(DataGrid grid)
    {
        lock (_live)
        {
            _live.RemoveAll(r => !r.TryGetTarget(out var g) || ReferenceEquals(g, grid));
        }
    }

    /// <summary>Captures every live grid's layout — called when the main window closes.</summary>
    public void CaptureAll()
    {
        lock (_live)
        {
            foreach (var grid in LiveGrids())
                Capture(grid, GridColumnPersistence.GetKey(grid));
        }
    }

    private IEnumerable<DataGrid> LiveGrids()
    {
        foreach (var r in _live)
            if (r.TryGetTarget(out var g)) yield return g;
    }

    private void ApplyVisibility(DataGrid grid)
    {
        foreach (var col in grid.Columns)
        {
            var header = HeaderOf(col);
            if (header.Length > 0)
                col.Visibility = IsColumnVisible(header) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static string HeaderOf(DataGridColumn col) => col.Header?.ToString() ?? "";

    private static bool TryParseLength(string s, out DataGridLength len)
    {
        try
        {
            len = (DataGridLength)new DataGridLengthConverter().ConvertFromString(s)!;
            return true;
        }
        catch
        {
            len = default;
            return false;
        }
    }
}
