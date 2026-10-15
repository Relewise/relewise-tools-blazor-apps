using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.Client.DataTypes.RetailMedia;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class DictionaryEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task CreateProvidesSpareRowAndCommitsOnlyAssignedEnumKeys()
    {
        await using var context = CreateContext();
        Dictionary<Advertiser.EntityState, int>? result = null;
        var component = context.Render<DictionaryEditor<Advertiser.EntityState, int>>(p => p
            .Add(c => c.Value, null).Add(c => c.Setter, value => result = (Dictionary<Advertiser.EntityState, int>?)value));
        component.Find(".dictionary-null").Click();
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
        Assert.AreEqual("true", component.Find(".dictionary-box").GetAttribute("data-expanded"));
        var state = Enum.GetValues<Advertiser.EntityState>()[0];
        await component.InvokeAsync(() => component.FindComponent<NullableEnumEditor<Advertiser.EntityState>>().Instance.Setter(state));
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(1, component.FindComponents<EnumEditor<Advertiser.EntityState>>().Count);
        Assert.AreEqual(1, component.FindComponents<NullableEnumEditor<Advertiser.EntityState>>().Count);
        component.Find(".dictionary-value input").Input("17");
        Assert.AreEqual(17, result[state]);
        Assert.AreEqual(1, result.Count);
        component.Find("button[aria-label='Remove entry']:not([disabled])").Click();
        Assert.AreEqual(0, result.Count);
        component.Find("button[aria-label='Remove dictionary']").Click();
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DuplicateAndUnchangedKeyEditsNeverOverwriteOrDeleteEntries()
    {
        await using var context = CreateContext();
        var validation = new EditorValidationState();
        var values = new Dictionary<int, int> { [1] = 10, [2] = 20 };
        var component = context.Render<DictionaryEditor<int, int>>(p => p
            .AddCascadingValue(validation).Add(c => c.Value, values).Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, _ => { }));
        await component.InvokeAsync(() => component.FindComponents<IntEditor>()[0].Instance.Setter(1));
        Assert.AreEqual(10, values[1]);
        await component.InvokeAsync(() => component.FindComponents<IntEditor>()[0].Instance.Setter(2));
        Assert.IsFalse(validation.IsValid);
        Assert.AreEqual(10, values[1]);
        Assert.AreEqual(20, values[2]);
        Assert.AreEqual(2, values.Count);
        await component.InvokeAsync(() => component.FindComponents<IntEditor>()[0].Instance.Setter(3));
        Assert.IsTrue(validation.IsValid);
        Assert.IsFalse(values.ContainsKey(1));
        Assert.AreEqual(10, values[3]);
        Assert.AreEqual(20, values[2]);
    }

    [TestMethod]
    public async Task ComparerAndManualCollapseSurviveParentRenders()
    {
        await using var context = CreateContext();
        var validation = new EditorValidationState();
        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["One"] = 1, ["Two"] = 2 };
        var component = context.Render<DictionaryEditor<string, int>>(p => p
            .AddCascadingValue(validation).Add(c => c.Value, values).Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, _ => { }));
        component.Find(".dictionary-key input").Input("TWO");
        Assert.IsFalse(validation.IsValid);
        Assert.AreEqual(1, values["One"]);
        Assert.AreEqual(2, values["Two"]);
        component.Find(".dictionary-title").Click();
        component.Render(p => p.Add(c => c.Value, values));
        Assert.AreEqual("false", component.Find(".dictionary-box").GetAttribute("data-expanded"));
        Assert.AreEqual(0, component.FindAll("input").Count);
        await context.DisposeAsync();
        Assert.IsTrue(validation.IsValid);
    }

    [TestMethod]
    public async Task ReadOnlyHasNoDraftRowsAndGuardsChildSettersAndPublicMutations()
    {
        await using var context = CreateContext();
        var values = new Dictionary<int, int> { [0] = 4 };
        var component = context.Render<DictionaryEditor<int, int>>(p => p
            .Add(c => c.Value, values).Add(c => c.OpenDetails, true).Add(c => c.ReadOnly, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only dictionary changed.")));
        Assert.AreEqual(2, component.FindAll("input").Count);
        Assert.AreEqual(0, component.FindAll("button[aria-label='Remove entry']").Count);
        await component.InvokeAsync(() =>
        {
            component.FindComponents<IntEditor>()[0].Instance.Setter(8);
            component.FindComponents<IntEditor>()[1].Instance.Setter(9);
            component.Instance.Create();
            component.Instance.Add();
            component.Instance.Remove(0);
        });
        Assert.AreEqual(4, values[0]);
        Assert.AreEqual(1, values.Count);
    }

    [TestMethod]
    public async Task SpareNumericKeyStartsNullAndAcceptsZeroWithoutSerializingDraftValues()
    {
        await using var context = CreateContext();
        var validation = new EditorValidationState();
        var values = new Dictionary<int, int>();
        var component = context.Render<DictionaryEditor<int, int>>(p => p
            .AddCascadingValue(validation).Add(c => c.Value, values).Add(c => c.Setter, _ => { }));
        Assert.IsNull(component.FindComponents<IntEditor>()[0].Instance.Value);
        component.Find(".dictionary-value input").Input("8");
        Assert.AreEqual(0, values.Count);
        Assert.IsFalse(validation.IsValid);
        component.Find(".dictionary-key input").Input("0");
        Assert.AreEqual(8, values[0]);
        Assert.IsTrue(validation.IsValid);
        component.Render(p => p.Add(c => c.ReadOnly, true));
        Assert.AreEqual(2, component.FindAll("input").Count);
    }

    [TestMethod]
    public async Task CopyExcludesSpareRowAndPreservesLoadedNullValues()
    {
        await using var context = CreateContext();
        var values = new Dictionary<int, string> { [1] = null!, [2] = "" };
        var component = context.Render<DictionaryEditor<int, string>>(p => p
            .Add(c => c.Value, values).Add(c => c.OpenDetails, true).Add(c => c.Setter, _ => { }));
        component.Find("button[aria-label='Copy dictionary as JSON']").Click();
        var copy = context.JSInterop.Invocations["navigator.clipboard.writeText"].Single();
        Assert.AreEqual(JsonConvert.SerializeObject(values, Formatting.Indented), copy.Arguments[0]);
        Assert.AreEqual(2, values.Count);
        Assert.IsNull(values[1]);
        Assert.AreEqual("", values[2]);
    }
}
