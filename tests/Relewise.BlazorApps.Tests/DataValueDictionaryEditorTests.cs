using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class DataValueDictionaryEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task CreateAndCompleteStringEntryExcludesDraftsFromSerialization()
    {
        await using var context = CreateContext();
        var validation = new EditorValidationState();
        Dictionary<string, DataValue>? result = null;
        var component = context.Render<DataValueDictionaryEditor>(parameters => parameters
            .AddCascadingValue(validation)
            .Add(c => c.Value, null)
            .Add(c => c.Setter, value => result = (Dictionary<string, DataValue>?)value));

        component.Find(".dictionary-null").Click();
        Assert.IsNotNull(result);
        Assert.AreEqual("true", component.Find(".dictionary-box").GetAttribute("data-expanded"));
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual(0, component.FindAll("details").Count);
        component.Find("input[data-testid='dictionary-key']").Input("name");
        Assert.IsFalse(validation.IsValid);
        Assert.AreEqual(0, result.Count);
        component.FindComponent<DataValueEditor>().FindComponent<StringEditor>().Find("input").Input("fixture");

        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual(2, component.FindAll("input[data-testid='dictionary-key']").Count);
        var restored = JsonConvert.DeserializeObject<Dictionary<string, DataValue>>(JsonConvert.SerializeObject(result))!;
        Assert.AreEqual(1, restored.Count);
        Assert.AreEqual(DataValue.DataValueTypes.String, restored["name"].Type);
        Assert.AreEqual("fixture", restored["name"].Value);

        component.FindAll("input[data-testid='dictionary-key']")[0].Input("");
        component.FindComponents<DataValueEditor>()[0].FindComponent<StringEditor>().Find("input").Input("");
        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(1, component.FindAll("input[data-testid='dictionary-key']").Count);
        Assert.IsTrue(validation.IsValid);
    }

    [TestMethod]
    public async Task TypeSwitchPreservesFalseAndZeroAsCompleteValues()
    {
        await using var context = CreateContext();
        var validation = new EditorValidationState();
        var value = new Dictionary<string, DataValue>();
        var component = context.Render<DataValueDictionaryEditor>(parameters => parameters
            .AddCascadingValue(validation)
            .Add(c => c.Value, value)
            .Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, _ => { }));
        component.Find("input[data-testid='dictionary-key']").Input("typed");
        await component.InvokeAsync(() => component.FindComponent<DataValueEditor>().Instance.SetType(DataValue.DataValueTypes.Boolean));
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual(false, value["typed"].Value);
        Assert.AreEqual(2, component.FindAll("input[data-testid='dictionary-key']").Count);

        await component.InvokeAsync(() => component.FindComponents<DataValueEditor>()[0].Instance.SetType(DataValue.DataValueTypes.Double));
        component.FindComponent<DoubleEditor>().Find("input").Input("0");
        Assert.IsTrue(validation.IsValid);
        Assert.IsInstanceOfType<double>(value["typed"].Value);
        Assert.AreEqual(0.0, value["typed"].Value);
        Assert.AreEqual(DataValue.DataValueTypes.Double, value["typed"].Type);
    }

    [TestMethod]
    public async Task DuplicateKeysUseTheComparerAndNeverOverwriteTheOriginal()
    {
        await using var context = CreateContext();
        var validation = new EditorValidationState();
        var original = new DataValue("original");
        var value = new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase) { ["name"] = original };
        var component = context.Render<DataValueDictionaryEditor>(parameters => parameters
            .AddCascadingValue(validation)
            .Add(c => c.Value, value)
            .Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, _ => { }));
        component.FindAll("input[data-testid='dictionary-key']")[1].Input("NAME");
        component.FindComponents<DataValueEditor>()[1].FindComponent<StringEditor>().Find("input").Input("duplicate");
        Assert.IsFalse(validation.IsValid);
        Assert.AreEqual(1, value.Count);
        Assert.AreSame(original, value["name"]);
        Assert.AreEqual(2, component.FindAll(".dictionary-duplicate-key").Count);

        component.FindAll("input[data-testid='dictionary-key']")[1].Input("other");
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual("duplicate", value["other"].Value);
        component.FindAll("button[aria-label='Remove entry']")[1].Click();
        Assert.AreEqual(1, value.Count);
        Assert.AreEqual(2, component.FindAll("input[data-testid='dictionary-key']").Count);
    }

    [TestMethod]
    public async Task ReadOnlyPreservesLoadedEmptyAndNullValuesAndRejectsTypeChanges()
    {
        await using var context = CreateContext();
        var empty = new DataValue("");
        var value = new Dictionary<string, DataValue> { [""] = empty, ["null"] = null! };
        var component = context.Render<DataValueDictionaryEditor>(parameters => parameters
            .Add(c => c.Value, value)
            .Add(c => c.OpenDetails, true)
            .Add(c => c.ReadOnly, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only editor changed the dictionary.")));
        Assert.AreEqual(2, component.FindAll("input[data-testid='dictionary-key']").Count);
        Assert.AreEqual(0, component.FindAll("button[aria-label='Remove entry']").Count);
        await component.InvokeAsync(() => component.FindComponents<DataValueEditor>()[0].Instance.SetType(DataValue.DataValueTypes.Boolean));
        Assert.AreSame(empty, value[""]);
        Assert.AreEqual("", empty.Value);
        Assert.IsNull(value["null"]);
    }

    [TestMethod]
    public async Task ParentRerenderPreservesManualCollapseAndDrafts()
    {
        await using var context = CreateContext();
        var value = new Dictionary<string, DataValue>();
        var component = context.Render<DataValueDictionaryEditor>(parameters => parameters
            .Add(c => c.Value, value)
            .Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, _ => { }));
        component.Find("input[data-testid='dictionary-key']").Input("draft");
        component.Find(".dictionary-title").Click();
        component.Render(parameters => parameters.Add(c => c.Value, value));
        Assert.AreEqual("false", component.Find(".dictionary-box").GetAttribute("data-expanded"));
        component.Find(".dictionary-title").Click();
        Assert.AreEqual("draft", component.Find("input[data-testid='dictionary-key']").GetAttribute("value"));
        Assert.AreEqual(0, value.Count);
    }

    [TestMethod]
    public void RegistrySelectsOnlyTheExactDataValueDictionarySpecialization()
    {
        var type = typeof(Dictionary<string, DataValue>);
        var editor = Settings.Editors.First(editor => editor.CanHandle(type));
        Assert.AreEqual(typeof(DataValueDictionaryEditor), editor.EditorType(type));
        Assert.IsNull(editor.InitValue(type));
        Assert.AreEqual(typeof(StringDictionaryEditor), Settings.Editors.First(editor => editor.CanHandle(typeof(Dictionary<string, string>))).EditorType(typeof(Dictionary<string, string>)));
        Assert.AreEqual(typeof(DictionaryEditor<string, int>), Settings.Editors.First(editor => editor.CanHandle(typeof(Dictionary<string, int>))).EditorType(typeof(Dictionary<string, int>)));
    }
}
