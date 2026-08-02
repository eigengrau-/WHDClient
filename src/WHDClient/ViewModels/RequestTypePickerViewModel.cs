using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WHDClient.Core.Models;

namespace WHDClient.ViewModels;

/// <summary>One level in the cascading request type picker (a single ComboBox).</summary>
public partial class RequestTypeLevelViewModel : ObservableObject
{
    public RequestTypeLevelViewModel(IReadOnlyList<RequestType> items) => Items = items;

    public IReadOnlyList<RequestType> Items { get; }

    [ObservableProperty] private RequestType? _selectedItem;
}

/// <summary>
/// Cascading request type selection: starts with the root types and appends a child
/// ComboBox for each selection that has children, so types can only be picked in
/// hierarchy order (e.g. Hardware &gt; Chromebooks &gt; Repair Request).
/// The effective selection is the deepest selected level.
/// </summary>
public partial class RequestTypePickerViewModel : ObservableObject
{
    private IReadOnlyList<RequestType> _all = Array.Empty<RequestType>();
    private bool _rebuilding;

    public ObservableCollection<RequestTypeLevelViewModel> Levels { get; } = new();

    [ObservableProperty] private RequestType? _selectedRequestType;

    /// <summary>Loads the (selectable) request types and shows the root level.</summary>
    public void SetRequestTypes(IEnumerable<RequestType> all)
    {
        _all = all as IReadOnlyList<RequestType> ?? all.ToList();
        Levels.Clear();
        AddLevel(RequestTypeTree.Roots(_all));
        UpdateSelection();
    }

    /// <summary>Pre-selects the full path down to the given request type (no-op if unknown).</summary>
    public void SetSelectedRequestType(int id)
    {
        var path = RequestTypeTree.PathTo(_all, id);
        if (path.Count == 0) return;

        _rebuilding = true;
        try
        {
            Levels.Clear();
            IReadOnlyList<RequestType> items = RequestTypeTree.Roots(_all);
            foreach (var node in path)
            {
                var level = AddLevel(items);
                level.SelectedItem = level.Items.FirstOrDefault(i => i.Id == node.Id);
                items = RequestTypeTree.ChildrenOf(_all, node.Id);
            }
            if (items.Count > 0) AddLevel(items);
        }
        finally
        {
            _rebuilding = false;
        }
        UpdateSelection();
    }

    public void Clear()
    {
        _rebuilding = true;
        try
        {
            while (Levels.Count > 1) RemoveLevel(Levels.Count - 1);
            if (Levels.Count > 0) Levels[0].SelectedItem = null;
        }
        finally
        {
            _rebuilding = false;
        }
        UpdateSelection();
    }

    private RequestTypeLevelViewModel AddLevel(IReadOnlyList<RequestType> items)
    {
        var level = new RequestTypeLevelViewModel(items);
        level.PropertyChanged += OnLevelPropertyChanged;
        Levels.Add(level);
        return level;
    }

    private void RemoveLevel(int index)
    {
        Levels[index].PropertyChanged -= OnLevelPropertyChanged;
        Levels.RemoveAt(index);
    }

    private void OnLevelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_rebuilding || e.PropertyName != nameof(RequestTypeLevelViewModel.SelectedItem) || sender is not RequestTypeLevelViewModel level)
            return;

        var index = Levels.IndexOf(level);
        if (index < 0) return;

        _rebuilding = true;
        try
        {
            while (Levels.Count > index + 1) RemoveLevel(Levels.Count - 1);
            if (level.SelectedItem != null)
            {
                var children = RequestTypeTree.ChildrenOf(_all, level.SelectedItem.Id);
                if (children.Count > 0) AddLevel(children);
            }
        }
        finally
        {
            _rebuilding = false;
        }
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        SelectedRequestType = Levels.LastOrDefault(l => l.SelectedItem != null)?.SelectedItem;
    }
}
