using Bunit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Relewise.BlazorApps.TypeEditors;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class DictionaryEditorTests
{
    [TestMethod]
    public void StringKeysCannotBeClearedButValuesCan()
    {
        using var context = new BunitContext();
        var values = new Dictionary<string, string> { ["key"] = "value" };
        var component = context.Render<DictionaryEditor<string, string>>(parameters => parameters
            .Add(p => p.Value, values)
            .Add(p => p.Setter, _ => { }));
        var keyEditor = component.FindComponents<StringEditor>().Single(editor => editor.Instance.Value == "key");
        var valueEditor = component.FindComponents<StringEditor>().Single(editor => editor.Instance.Value == "value");

        Assert.HasCount(0, keyEditor.FindAll("button"));
        valueEditor.Find("button").Click();

        Assert.IsTrue(values.ContainsKey("key"));
        Assert.IsNull(values["key"]);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("key")]
    [DataRow("other")]
    public async Task InvalidOrUnchangedKeysPreserveExistingEntries(string? newKey)
    {
        using var context = new BunitContext();
        var values = new Dictionary<string, string> { ["key"] = "value", ["other"] = "preserved" };
        int updates = 0;
        var component = context.Render<DictionaryEditor<string, string>>(parameters => parameters
            .Add(p => p.Value, values)
            .Add(p => p.Setter, _ => updates++));
        var keyEditor = component.FindComponents<StringEditor>().Single(editor => editor.Instance.Value == "key");

        await component.InvokeAsync(() => keyEditor.Instance.Setter(newKey));

        Assert.AreEqual("value", values["key"]);
        Assert.AreEqual("preserved", values["other"]);
        Assert.HasCount(2, values);
        Assert.AreEqual(0, updates);
        Assert.AreEqual("key", keyEditor.Find("input").GetAttribute("value"));
    }

    [TestMethod]
    public void RenamingAKeyPreservesItsValueAndNotifiesTheParent()
    {
        using var context = new BunitContext();
        var values = new Dictionary<string, string> { ["key"] = "value", ["other"] = "preserved" };
        object? updated = null;
        var component = context.Render<DictionaryEditor<string, string>>(parameters => parameters
            .Add(p => p.Value, values)
            .Add(p => p.Setter, value => updated = value));
        var keyEditor = component.FindComponents<StringEditor>().Single(editor => editor.Instance.Value == "key");

        keyEditor.Find("input").Input("renamed");

        Assert.IsFalse(values.ContainsKey("key"));
        Assert.AreEqual("value", values["renamed"]);
        Assert.AreEqual("preserved", values["other"]);
        Assert.AreSame(values, updated);
    }
}
