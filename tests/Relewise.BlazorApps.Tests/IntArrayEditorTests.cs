using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.TypeEditors;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class IntArrayEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public void RegistryPreservesExactArrayTypes()
    {
        var handler = Settings.Editors.First(editor => editor.CanHandle(typeof(int[])));
        Assert.AreEqual(typeof(IntArrayEditor), handler.EditorType(typeof(int[])));
        Assert.IsInstanceOfType<int[]>(handler.InitValue(typeof(int[])));
        Assert.AreEqual(typeof(ArrayEditor<long>), Settings.Editors.First(editor => editor.CanHandle(typeof(long[]))).EditorType(typeof(long[])));
    }

    [TestMethod]
    public async Task CreateEditRemoveAndClearPreserveNullEmptyAndIntegerValues()
    {
        await using var context = CreateContext();
        int[]? result = null;
        var component = context.Render<IntArrayEditor>(parameters => parameters
            .Add(c => c.Value, null)
            .Add(c => c.Setter, value => result = (int[]?)value));

        component.Find(".integers-null").Click();
        Assert.AreEqual("[]", JsonConvert.SerializeObject(result));
        Assert.AreEqual("true", component.Find(".integers-box").GetAttribute("data-expanded"));
        component.Find(".integers-entries button").Click();
        component.Find("input").Input("-2147483648");
        Assert.AreEqual(int.MinValue, result![0]);
        component.Find(".integers-entries > button").Click();
        component.FindAll("input")[1].Input("2147483647");
        CollectionAssert.AreEqual(new[] { int.MinValue, int.MaxValue }, result);
        component.Find("button[aria-label='Remove value 1']").Click();
        CollectionAssert.AreEqual(new[] { int.MaxValue }, result);
        component.Find("button[aria-label='Remove value 1']").Click();
        Assert.AreEqual("[]", JsonConvert.SerializeObject(result));
        component.Find("button[aria-label='Set integer array to null']").Click();
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task EditingPreservesOrderDuplicatesOriginalArrayAndManualCollapse()
    {
        await using var context = CreateContext();
        int[] original = [7, 7, 0];
        int[]? result = null;
        var component = context.Render<IntArrayEditor>(parameters => parameters
            .Add(c => c.Value, original).Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, value => result = (int[]?)value));
        component.FindAll("input")[2].Input("-2");
        CollectionAssert.AreEqual(new[] { 7, 7, -2 }, result);
        CollectionAssert.AreEqual(new[] { 7, 7, 0 }, original);
        component.Find(".integers-title").Click();
        component.Render(parameters => parameters.Add(c => c.Value, result));
        Assert.AreEqual(0, component.FindAll("input").Count);
    }

    [TestMethod]
    public async Task ReadOnlyGuardsAllMutations()
    {
        await using var context = CreateContext();
        var component = context.Render<IntArrayEditor>(parameters => parameters
            .Add(c => c.Value, new[] { 0, -1, 2 }).Add(c => c.OpenDetails, true)
            .Add(c => c.ReadOnly, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only value changed.")));
        Assert.AreEqual(3, component.FindAll("input[disabled]").Count);
        Assert.AreEqual(0, component.FindAll("button[aria-label^='Remove value']").Count);
        await component.InvokeAsync(() =>
        {
            component.Instance.Create();
            component.Instance.Add();
            component.Instance.Update(0, 5);
            component.Instance.Remove(0);
            component.Instance.Clear();
        });
        CollectionAssert.AreEqual(new[] { 0, -1, 2 }, component.Instance.Value);
    }
}
