using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.TypeEditors;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class StringArrayEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public void RegistrySpecializesOnlyStringArrays()
    {
        var strings = Settings.Editors.First(editor => editor.CanHandle(typeof(string[])));
        Assert.AreEqual(typeof(StringArrayEditor), strings.EditorType(typeof(string[])));
        Assert.IsInstanceOfType<string[]>(strings.InitValue(typeof(string[])));
        var integers = Settings.Editors.First(editor => editor.CanHandle(typeof(int[])));
        Assert.AreEqual(typeof(IntArrayEditor), integers.EditorType(typeof(int[])));
    }

    [TestMethod]
    public async Task CreateEditRemoveAndClearPreserveDistinctArrayStates()
    {
        await using var context = CreateContext();
        string[]? result = null;
        var component = context.Render<StringArrayEditor>(parameters => parameters
            .Add(c => c.Value, null)
            .Add(c => c.Setter, value => result = (string[]?)value));

        component.Find(".strings-null").Click();
        Assert.IsNotNull(result);
        Assert.AreEqual("[]", JsonConvert.SerializeObject(result));
        Assert.AreEqual("true", component.Find(".strings-box").GetAttribute("data-expanded"));
        component.Find(".strings-entries button").Click();
        Assert.AreEqual("[\"\"]", JsonConvert.SerializeObject(result));
        component.Find("input").Input("a,b second line");
        Assert.AreEqual("a,b second line", result[0]);
        component.Find("input").Input("");
        Assert.AreEqual("", result[0]);
        component.Find("button[aria-label='Remove value 1']").Click();
        Assert.AreEqual("[]", JsonConvert.SerializeObject(result));
        component.Find("button[aria-label='Set string array to null']").Click();
        Assert.IsNull(result);
        Assert.AreEqual(1, component.FindAll(".strings-null").Count);
    }

    [TestMethod]
    public async Task EditingPreservesOrderDuplicatesNullsAndOriginalArray()
    {
        await using var context = CreateContext();
        string[] original = ["same", "same", "", null!];
        string[]? result = null;
        var component = context.Render<StringArrayEditor>(parameters => parameters
            .Add(c => c.Value, original)
            .Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, value => result = (string[]?)value));

        component.FindAll("input")[1].Input("edited");
        CollectionAssert.AreEqual(new string[] { "same", "edited", "", null! }, result);
        CollectionAssert.AreEqual(new string[] { "same", "same", "", null! }, original);
        component.Find(".strings-title").Click();
        component.Render(parameters => parameters.Add(c => c.Value, result));
        Assert.AreEqual("false", component.Find(".strings-box").GetAttribute("data-expanded"));
        Assert.AreEqual(0, component.FindAll("input").Count);
    }

    [TestMethod]
    public async Task ReadOnlyGuardsAllMutationsAndPreservesLoadedValues()
    {
        await using var context = CreateContext();
        var component = context.Render<StringArrayEditor>(parameters => parameters
            .Add(c => c.Value, new string[] { "", null!, "same", "same" })
            .Add(c => c.OpenDetails, true)
            .Add(c => c.ReadOnly, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only editor changed its value.")));

        Assert.AreEqual(4, component.FindAll("input").Count);
        Assert.AreEqual(0, component.FindAll("button[aria-label^='Remove value']").Count);
        Assert.AreEqual(0, component.FindAll("button[aria-label='Set string array to null']").Count);
        await component.InvokeAsync(() =>
        {
            component.Instance.Create();
            component.Instance.Add();
            component.Instance.Update(0, "changed");
            component.Instance.Remove(0);
            component.Instance.Clear();
        });
        Assert.AreEqual("[\"\",null,\"same\",\"same\"]", JsonConvert.SerializeObject(component.Instance.Value));
    }
}
