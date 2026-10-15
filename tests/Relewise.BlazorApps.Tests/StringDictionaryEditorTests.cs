using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class StringDictionaryEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task CreatingAndCompletingAnEntryKeepsOneUnusedDraftOutOfJson()
    {
        await using var context = CreateContext();
        var validation = new EditorValidationState();
        Dictionary<string, string>? result = null;
        var component = context.Render<StringDictionaryEditor>(parameters => parameters
            .AddCascadingValue(validation)
            .Add(c => c.Value, null)
            .Add(c => c.Setter, value => result = (Dictionary<string, string>?)value));

        component.Find(".dictionary-null").Click();
        Assert.IsNotNull(result);
        Assert.AreEqual("true", component.Find(".dictionary-box").GetAttribute("data-expanded"));
        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(1, component.FindAll("input[data-testid='dictionary-key']").Count);
        Assert.IsTrue(validation.IsValid);
        component.Find("input[data-testid='dictionary-key']").Input("example");
        Assert.IsFalse(validation.IsValid);
        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(1, component.FindAll("input[data-testid='dictionary-key']").Count);
        Assert.AreEqual(0, component.FindAll(".mud-input-helper-text").Count);
        component.Find("input[data-testid='dictionary-value']").Input("content");

        Assert.AreEqual("content", result["example"]);
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual(2, component.FindAll("input[data-testid='dictionary-key']").Count);
        Assert.AreEqual("{\"example\":\"content\"}", JsonConvert.SerializeObject(result));
        component.FindAll("input[data-testid='dictionary-key']")[0].Input("");
        component.FindAll("input[data-testid='dictionary-value']")[0].Input("");
        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(1, component.FindAll("input[data-testid='dictionary-key']").Count);
        Assert.IsTrue(validation.IsValid);
    }

    [TestMethod]
    public async Task DefaultEmptyDictionaryStartsCollapsedAndCanBeOpened()
    {
        await using var context = CreateContext();
        var component = context.Render<StringDictionaryEditor>(parameters => parameters
            .Add(c => c.Value, new Dictionary<string, string>())
            .Add(c => c.OpenDetails, false)
            .Add(c => c.Setter, _ => { }));

        Assert.AreEqual("false", component.Find(".dictionary-box").GetAttribute("data-expanded"));
        Assert.AreEqual(0, component.FindAll("input[data-testid='dictionary-key']").Count);
        component.Find(".dictionary-title").Click();
        Assert.AreEqual("true", component.Find(".dictionary-box").GetAttribute("data-expanded"));
        Assert.AreEqual(1, component.FindAll("input[data-testid='dictionary-key']").Count);
    }

    [TestMethod]
    public async Task DuplicateDraftDoesNotOverwriteAnExistingEntry()
    {
        await using var context = CreateContext();
        var validation = new EditorValidationState();
        var value = new Dictionary<string, string> { ["example"] = "original" };
        var component = context.Render<StringDictionaryEditor>(parameters => parameters
            .AddCascadingValue(validation)
            .Add(c => c.Value, value)
            .Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, _ => { }));

        component.FindAll("input[data-testid='dictionary-key']")[1].Input("example");
        component.FindAll("input[data-testid='dictionary-value']")[1].Input("duplicate");

        Assert.AreEqual(1, value.Count);
        Assert.AreEqual("original", value["example"]);
        Assert.IsFalse(validation.IsValid);
        Assert.AreEqual(2, component.FindAll(".dictionary-duplicate-key").Count);
        Assert.AreEqual(0, component.FindAll(".mud-input-helper-text").Count);
        component.FindAll("input[data-testid='dictionary-key']")[1].Input("other");
        Assert.AreEqual(2, value.Count);
        Assert.AreEqual("duplicate", value["other"]);
        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual(3, component.FindAll("input[data-testid='dictionary-key']").Count);
    }

    [TestMethod]
    public async Task ReadOnlyDictionaryPreservesEmptyValuesWithoutAnExtraDraft()
    {
        await using var context = CreateContext();
        var value = new Dictionary<string, string> { [""] = "", ["nullable"] = null! };
        var component = context.Render<StringDictionaryEditor>(parameters => parameters
            .Add(c => c.Value, value)
            .Add(c => c.ReadOnly, true)
            .Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only editor changed the dictionary.")));

        Assert.AreEqual(2, component.FindAll("input[data-testid='dictionary-key']").Count);
        Assert.IsTrue(component.FindAll("input").All(input => input.HasAttribute("disabled")));
        Assert.AreEqual("", value[""]);
        Assert.IsNull(value["nullable"]);
    }

    [TestMethod]
    public void StringDictionaryUsesSpecializedEditorWithoutChangingOtherDictionaries()
    {
        Assert.AreEqual(typeof(StringDictionaryEditor), Settings.Editors.First(editor => editor.CanHandle(typeof(Dictionary<string, string>))).EditorType(typeof(Dictionary<string, string>)));
        Assert.AreEqual(typeof(DictionaryEditor<string, int>), Settings.Editors.First(editor => editor.CanHandle(typeof(Dictionary<string, int>))).EditorType(typeof(Dictionary<string, int>)));
    }
}
