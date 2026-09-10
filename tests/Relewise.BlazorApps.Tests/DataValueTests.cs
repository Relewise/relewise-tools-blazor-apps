using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Relewise.BlazorApps.Shared;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class DataValueTests
{
    [TestMethod]
    public void NormalizesNumericDataInsideCollectionsAndCycles()
    {
        var single = new DataValue(1.0) { Value = 42L };
        var list = new DataValue(new List<double> { 1.0 }) { Value = new object?[] { 1L, 2.5, null } };
        var node = new Node { Values = new() { ["single"] = single, ["list"] = list } };
        node.Next = node;
        node.EnsureDoubleDataValues();
        Assert.AreEqual(42.0, single.Value);
        CollectionAssert.AreEqual(new[] { 1.0, 2.5 }, ((IEnumerable<double>)list.Value).ToArray());
    }

    [TestMethod]
    public void JsonRoundTripPreservesNumericDataAfterNormalization()
    {
        var original = new Dictionary<string, DataValue> { ["price"] = new(42.0), ["name"] = new("fixture") };
        var restored = JsonConvert.DeserializeObject<Dictionary<string, DataValue>>(JsonConvert.SerializeObject(original))!;
        restored.EnsureDoubleDataValues();
        Assert.AreEqual(42.0, restored["price"].Value);
        Assert.AreEqual("fixture", restored["name"].Value);
    }

    private sealed class Node
    {
        public Dictionary<string, DataValue> Values { get; init; } = new();
        public Node? Next { get; set; }
    }
}
