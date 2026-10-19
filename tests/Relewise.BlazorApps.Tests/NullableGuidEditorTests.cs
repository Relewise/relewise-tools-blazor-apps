using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.Components;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.DataTypes.RetailMedia;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class NullableGuidEditorTests
{
    private static readonly Guid ExampleId = Guid.Parse("12345678-1234-1234-1234-123456789abc");

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task AdvertiserIdCanBeEnteredAndClearedInlineWithoutLosingNullSerialization()
    {
        await using var context = CreateContext();
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var advertiser = JsonConvert.DeserializeObject<Advertiser>("{}")!;
        string before = JsonConvert.SerializeObject(advertiser);
        var component = context.Render<ObjectEditor<Advertiser>>(p => p
            .Add(c => c.Value, advertiser).Add(c => c.Setter, _ => { }));
        var editor = component.FindComponent<GuidEditor>();

        Assert.IsTrue(editor.Instance.AllowNull);
        Assert.HasCount(1, editor.FindAll("input"));
        Assert.AreEqual("<NULL>", editor.Find("input").GetAttribute("placeholder"));
        editor.Find("input").Input(ExampleId.ToString());
        Assert.AreEqual(ExampleId, advertiser.Id);
        editor.Find("button[aria-label='Clear']").Click();
        Assert.IsNull(advertiser.Id);
        Assert.AreEqual(before, JsonConvert.SerializeObject(advertiser));
        Assert.HasCount(1, editor.FindAll("input"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ClearingOnlyChangesNullableGuidValues(bool allowNull)
    {
        await using var context = CreateContext();
        object? saved = ExampleId;
        int calls = 0;
        var component = context.Render<GuidEditor>(p => p
            .Add(c => c.Value, ExampleId).Add(c => c.AllowNull, allowNull)
            .Add(c => c.Setter, value => { saved = value; calls++; }));
        component.Find("input").Input("");

        Assert.AreEqual(allowNull ? null : (object)ExampleId, saved);
        Assert.AreEqual(allowNull ? 1 : 0, calls);
        Assert.AreEqual(!allowNull, component.FindComponent<RTextField>().Instance.Error);
        Assert.AreEqual(allowNull, component.FindComponent<RTextField>().Instance.Clearable);
        component.Find("input").Input(Guid.Empty.ToString());
        Assert.IsInstanceOfType<Guid>(saved);
        Assert.AreEqual(Guid.Empty, saved);
        Assert.IsFalse(component.FindComponent<RTextField>().Instance.Error);
    }

    [TestMethod]
    public async Task InvalidGuidDraftSurvivesRerenderWithoutChangingTheUnderlyingValue()
    {
        await using var context = CreateContext();
        object? saved = ExampleId;
        var component = context.Render<GuidEditor>(p => p
            .Add(c => c.Value, ExampleId).Add(c => c.AllowNull, true).Add(c => c.Setter, value => saved = value));
        component.Find("input").Input("not-a-guid");
        component.Render(p => p.Add(c => c.Value, ExampleId));
        Assert.AreEqual(ExampleId, saved);
        Assert.AreEqual("not-a-guid", component.Find("input").GetAttribute("value"));
        Assert.IsTrue(component.FindComponent<RTextField>().Instance.Error);
        component.Render(p => p.Add(c => c.Value, null));
        Assert.AreEqual("", component.Find("input").GetAttribute("value") ?? "");
        Assert.IsFalse(component.FindComponent<RTextField>().Instance.Error);
    }

    [TestMethod]
    public async Task ReadOnlyNullGuidIsVisibleAndCannotBeChangedOrCreated()
    {
        await using var context = CreateContext();
        int calls = 0;
        var component = context.Render<GuidEditor>(p => p
            .Add(c => c.Value, null).Add(c => c.ReadOnly, true).Add(c => c.AllowNull, true).Add(c => c.Setter, _ => calls++));
        var field = component.FindComponent<RTextField>().Instance;
        Assert.IsTrue(field.Disabled && field.ReadOnly);
        Assert.IsFalse(field.Clearable);
        Assert.AreEqual("<NULL>", component.Find("input").GetAttribute("placeholder"));
        await component.InvokeAsync(() => field.ValueChanged.InvokeAsync(ExampleId.ToString()));
        await component.InvokeAsync(component.Instance.Create);
        Assert.AreEqual(0, calls);
        Assert.IsNull(component.Instance.Value);
    }

    [TestMethod]
    public async Task NullableGuidCollectionsKeepNullItemsAndRequiredGuidItemsStartWithExactType()
    {
        await using var context = CreateContext();
        Guid?[] values = [null, Guid.Empty];
        Guid?[]? savedValues = null;
        var optional = context.Render<ArrayEditor<Guid?>>(p => p
            .Add(c => c.Value, values).Add(c => c.OpenDetails, true).Add(c => c.Setter, value => savedValues = (Guid?[]?)value));
        Assert.IsTrue(optional.FindComponents<GuidEditor>().All(editor => editor.Instance.AllowNull));
        optional.FindComponents<GuidEditor>()[0].Find("input").Input(ExampleId.ToString());
        Assert.IsNotNull(savedValues);
        Assert.AreEqual(ExampleId, savedValues[0]);
        optional.FindComponents<GuidEditor>()[1].Find("input").Input("");
        Assert.IsNull(savedValues[1]);
        object? saved = null;
        var required = context.Render<ArrayEditor<Guid>>(p => p
            .Add(c => c.Value, []).Add(c => c.OpenDetails, true).Add(c => c.Setter, value => saved = value));
        await required.Find(".array-entries button").ClickAsync(new());
        Assert.IsInstanceOfType<Guid[]>(saved);
        Assert.AreEqual(Guid.Empty, ((Guid[])saved!)[0]);
        Assert.IsFalse(required.FindComponent<GuidEditor>().Instance.AllowNull);

        var handler = Settings.Editors.First(editor => editor.CanHandle(typeof(Guid?)));
        Assert.AreEqual(typeof(GuidEditor), handler.EditorType(typeof(Guid?)));
        Assert.IsNull(handler.InitValue(typeof(Guid?)));
    }
}
