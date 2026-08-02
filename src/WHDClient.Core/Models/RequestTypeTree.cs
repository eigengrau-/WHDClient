namespace WHDClient.Core.Models;

/// <summary>
/// Helpers for working with the request type hierarchy (parent/child via <see cref="RequestType.ParentId"/>).
/// All methods preserve the order in which the server returned the items.
/// </summary>
public static class RequestTypeTree
{
    /// <summary>Top-level request types (no parent, or parent missing from the list), in server order.</summary>
    public static IReadOnlyList<RequestType> Roots(IEnumerable<RequestType> all)
    {
        var list = all as List<RequestType> ?? all.ToList();
        var ids = new HashSet<int>(list.Select(r => r.Id));
        return list.Where(r => r.ParentId == null || !ids.Contains(r.ParentId.Value)).ToList();
    }

    /// <summary>Direct children of the given request type, in server order.</summary>
    public static IReadOnlyList<RequestType> ChildrenOf(IEnumerable<RequestType> all, int parentId) =>
        all.Where(r => r.ParentId == parentId).ToList();

    public static bool HasChildren(IEnumerable<RequestType> all, int id) =>
        all.Any(r => r.ParentId == id);

    /// <summary>
    /// Path from the root down to (and including) the request type with the given id.
    /// Returns an empty list when the id is not present. Cycle-safe.
    /// </summary>
    public static IReadOnlyList<RequestType> PathTo(IEnumerable<RequestType> all, int id)
    {
        var byId = all.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
        if (!byId.TryGetValue(id, out var node)) return Array.Empty<RequestType>();

        var path = new List<RequestType>();
        var seen = new HashSet<int>();
        while (seen.Add(node.Id))
        {
            path.Insert(0, node);
            if (node.ParentId == null || !byId.TryGetValue(node.ParentId.Value, out var parent))
                break;
            node = parent;
        }
        return path;
    }
}
