using WHDClient.Core.Models;
using Xunit;

namespace WHDClient.Core.Tests;

public class RequestTypeTreeTests
{
    private static RequestType Rt(int id, int? parentId = null, bool archived = false, bool deleted = false) =>
        new() { Id = id, ParentId = parentId, ProblemTypeName = $"Type {id}", Archived = archived, Deleted = deleted };

    // Hardware(1) > Chromebooks(2) > Repair(3); Hardware(1) > Printers(4); Software(5)
    private static List<RequestType> SampleTree() => new()
    {
        Rt(1), Rt(2, 1), Rt(3, 2), Rt(4, 1), Rt(5)
    };

    [Fact]
    public void Roots_ReturnsTypesWithoutParent_InOrder()
    {
        var roots = RequestTypeTree.Roots(SampleTree());
        Assert.Equal(new[] { 1, 5 }, roots.Select(r => r.Id));
    }

    [Fact]
    public void Roots_TreatsMissingParentAsRoot()
    {
        var list = new List<RequestType> { Rt(9, 999) };
        Assert.Single(RequestTypeTree.Roots(list));
    }

    [Fact]
    public void ChildrenOf_ReturnsDirectChildrenOnly()
    {
        var children = RequestTypeTree.ChildrenOf(SampleTree(), 1);
        Assert.Equal(new[] { 2, 4 }, children.Select(r => r.Id));
    }

    [Fact]
    public void HasChildren_DetectsLeafAndParent()
    {
        var tree = SampleTree();
        Assert.True(RequestTypeTree.HasChildren(tree, 2));
        Assert.False(RequestTypeTree.HasChildren(tree, 3));
    }

    [Fact]
    public void PathTo_ReturnsRootToNode()
    {
        var path = RequestTypeTree.PathTo(SampleTree(), 3);
        Assert.Equal(new[] { 1, 2, 3 }, path.Select(r => r.Id));
    }

    [Fact]
    public void PathTo_UnknownId_ReturnsEmpty()
    {
        Assert.Empty(RequestTypeTree.PathTo(SampleTree(), 42));
    }

    [Fact]
    public void PathTo_Cycle_DoesNotHang()
    {
        var list = new List<RequestType> { Rt(1, 2), Rt(2, 1) };
        var path = RequestTypeTree.PathTo(list, 1);
        Assert.Equal(2, path.Count);
    }

    [Fact]
    public void IsSelectable_FalseWhenArchivedOrDeleted()
    {
        Assert.True(Rt(1).IsSelectable);
        Assert.False(Rt(2, archived: true).IsSelectable);
        Assert.False(Rt(3, deleted: true).IsSelectable);
    }
}
