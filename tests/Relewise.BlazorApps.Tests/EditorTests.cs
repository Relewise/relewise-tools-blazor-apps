using MudBlazor.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Search.Settings;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Shared;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class EditorTests
{
    private enum NonZeroOptions { First = 1, Second = 2 }

    [TestMethod]
    public async Task EnumSelectIncludesTheUndefinedNumericDefault()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<EnumEditor<NonZeroOptions>>(parameters => parameters
            .Add(c => c.Value, (NonZeroOptions)0)
            .Add(c => c.Setter, _ => { }));
        var select = component.FindComponent<Relewise.BlazorApps.Components.RSelect<NonZeroOptions>>();

        Assert.IsTrue(select.Instance.Items.Contains((NonZeroOptions)0));
        Assert.AreEqual("Select…", select.Instance.ToStringFunc!((NonZeroOptions)0));
        Assert.IsFalse(select.Instance.Clearable);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EnumSelectPreservesTheEnumTypeAndReadOnlyBehavior(bool readOnly)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        object? result = null;
        var component = context.Render<EnumEditor<DayOfWeek>>(parameters => parameters
            .Add(c => c.Value, DayOfWeek.Monday)
            .Add(c => c.ReadOnly, readOnly)
            .Add(c => c.Setter, value => result = value));
        var select = component.FindComponent<Relewise.BlazorApps.Components.RSelect<DayOfWeek>>();

        Assert.AreEqual(DayOfWeek.Monday, select.Instance.Value);
        Assert.AreEqual(7, select.Instance.Items.Count());
        Assert.AreEqual(readOnly, select.Instance.Disabled);
        Assert.IsTrue(select.Instance.Required);
        Assert.IsFalse(select.Instance.Clearable);
        await component.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(DayOfWeek.Wednesday));

        if (readOnly)
            Assert.IsNull(result);
        else
        {
            Assert.IsInstanceOfType<DayOfWeek>(result);
            Assert.AreEqual(DayOfWeek.Wednesday, result);
        }
    }

    [TestMethod]
    public async Task LanguageCanBeEnteredAndClearedWithoutCreatingAnObjectFirst()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        object? result = null;
        var component = context.Render<ObjectEditor<Relewise.Client.DataTypes.Language>>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.Setter, value => result = value));

        Assert.AreEqual(0, component.FindAll("details").Count);
        Assert.AreEqual(1, component.FindAll("button[aria-label='Copy Language as JSON']").Count);
        component.Find("input").Input("en-gb");
        Assert.AreEqual("en-gb", ((Relewise.Client.DataTypes.Language)result!).Value);
        Assert.AreEqual(2, component.FindAll(".mud-input button").Count);
        component.Find("button[aria-label='Clear']").Click();
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CurrencyCanBeEnteredAndClearedWithoutCreatingAnObjectFirst()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        object? result = null;
        var component = context.Render<ObjectEditor<Relewise.Client.DataTypes.Currency>>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.Setter, value => result = value));

        Assert.AreEqual(0, component.FindAll("details").Count);
        Assert.AreEqual(1, component.FindAll("button[aria-label='Copy Currency as JSON']").Count);
        component.Find("input").Input("EUR");
        Assert.AreEqual("EUR", ((Relewise.Client.DataTypes.Currency)result!).Value);
        Assert.AreEqual(2, component.FindAll(".mud-input button").Count);
        component.Find("button[aria-label='Clear']").Click();
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CreatingComplexObjectOpensOnlyTheCreatedLevel()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var component = context.Render<ObjectEditor<ProductSearchSettings>>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.AncestorTypes, [typeof(SearchRequest)])
            .Add(c => c.Setter, _ => { }));

        component.Find(".null-object-field").Click();
        Assert.IsTrue(component.Find("details").HasAttribute("open"));
        Assert.IsTrue(component.FindAll("details details").All(details => !details.HasAttribute("open")));
        Assert.IsTrue(component.FindAll("input").Count > 0);
    }

    [TestMethod]
    [DataRow(typeof(VariantSearchRequestSettings))]
    [DataRow(typeof(VariantRecommendationRequestSettings))]
    public async Task VariantSettingsCanBeCreatedEditedAndCleared(Type type)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        object? result = null;
        var handler = Settings.Editors.First(editor => editor.CanHandle(type));
        var component = context.Render<DynamicComponent>(parameters => parameters
            .Add(p => p.Type, handler.EditorType(type))
            .Add(p => p.Parameters, new Dictionary<string, object>
            {
                ["Value"] = null!,
                ["OpenDetails"] = true,
                ["Setter"] = (Action<object?>)(value => result = value)
            }));
        component.WaitForAssertion(() => Assert.HasCount(1, component.FindAll("button")));
        component.Find("button").Click();
        var settings = (VariantRequestSettings)result!;
        Assert.IsNotNull(settings);
        Assert.IsNull(settings.MaxVariantsPerProduct);
        Assert.IsNull(settings.Sorting);
        component.Find("button").Click();
        component.Find("input").Input("3");
        Assert.AreEqual(3, settings.MaxVariantsPerProduct);
        var sorting = component.FindComponent<Relewise.BlazorApps.Components.RSelect<NullableEnumEditor<VariantSorting>.EnumOption>>();
        await component.InvokeAsync(() => sorting.Instance.ValueChanged.InvokeAsync(new(VariantSorting.ByRelevance)));
        Assert.AreEqual(VariantSorting.ByRelevance, settings.Sorting);
        await component.InvokeAsync(() => sorting.Instance.ValueChanged.InvokeAsync(new(null)));
        Assert.IsNull(settings.Sorting);
        component.Find("input").Input("");
        Assert.IsNull(settings.MaxVariantsPerProduct);
    }

    [TestMethod]
    [DataRow(typeof(ProductSearchSettings), typeof(VariantSearchRequestSettings))]
    [DataRow(typeof(ProductRecommendationRequestSettings), typeof(VariantRecommendationRequestSettings))]
    public void RequestSettingsExposeVariantSettings(Type parent, Type child)
    {
        Assert.AreEqual(child, Settings.GetProperties(parent).Single(p => p.Name == "VariantRequestSettings").PropertyType);
    }

    [TestMethod]
    public async Task ReadOnlyNullableEnumPreservesNullAndHasNoMutationControls()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        var component = context.Render<NullableEnumEditor<VariantSorting>>(parameters => parameters
            .Add(p => p.Value, null)
            .Add(p => p.ReadOnly, true)
            .Add(p => p.Setter, _ => Assert.Fail("Read-only editor invoked setter.")));
        Assert.IsNull(component.Instance.Value);
        Assert.HasCount(0, component.FindAll("select, button, input"));
    }

    [TestMethod]
    [DataRow(typeof(byte))]
    [DataRow(typeof(ushort))]
    [DataRow(typeof(long))]
    [DataRow(typeof(int))]
    [DataRow(typeof(double))]
    public async Task NumericDefaultsCanBePassedToTheSelectedComponent(Type type)
    {
        var handler = Settings.Editors.First(editor => editor.CanHandle(type));
        var value = handler.InitValue(type);
        Assert.IsNotNull(value);
        Assert.AreEqual(type, value.GetType());
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<DynamicComponent>(parameters => parameters
            .Add(p => p.Type, handler.EditorType(type))
            .Add(p => p.Parameters, new Dictionary<string, object>
            {
                ["Value"] = value,
                ["Setter"] = (Action<object?>)(_ => { })
            }));
        Assert.AreEqual("0", component.Find("input").GetAttribute("value"));
    }

    [TestMethod]
    public async Task NullableByteCanBeClearedAndRecreated()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        object? result = (byte)7;
        var component = context.Render<ByteEditor>(parameters => parameters
            .Add(p => p.Value, (byte?)7)
            .Add(p => p.AllowNull, true)
            .Add(p => p.Setter, value => result = value));
        component.Find("input").Input("");
        Assert.IsNull(result);
        Assert.AreEqual(1, component.FindAll("input").Count);
        component.Find("input").Input("0");
        Assert.AreEqual((byte)0, result);
        Assert.AreEqual("0", component.Find("input").GetAttribute("value"));
    }

    [TestMethod]
    public async Task ReadOnlyByteDoesNotOfferMutationControls()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<ByteEditor>(parameters => parameters
            .Add(p => p.Value, (byte?)7)
            .Add(p => p.AllowNull, true)
            .Add(p => p.ReadOnly, true)
            .Add(p => p.Setter, _ => Assert.Fail("Read-only editor invoked setter.")));
        Assert.IsTrue(component.FindComponent<MudBlazor.MudNumericField<byte?>>().Instance.Disabled);
        Assert.IsTrue(component.Find("input").HasAttribute("disabled"));
    }

    [TestMethod]
    public async Task DoubleEditorAcceptsIntegralJsonValues()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        object? result = null;
        var component = context.Render<DoubleEditor>(parameters => parameters
            .Add(p => p.Value, 42L)
            .Add(p => p.Setter, value => result = value));
        component.Find("input").Input("42.5");
        Assert.AreEqual(42.5, result);
    }

    [TestMethod]
    [DataRow(typeof(int))]
    [DataRow(typeof(double))]
    [DataRow(typeof(decimal))]
    [DataRow(typeof(ushort))]
    [DataRow(typeof(byte))]
    public async Task OptionalNumericFieldsCanBeEnteredClearedAndSetToZero(Type type)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        object? result = null;
        var editor = Settings.Editors.First(e => e.CanHandle(type));
        var parameters = Settings.WithNumericNullability(new Dictionary<string, object>
        {
            ["Value"] = null!,
            ["Setter"] = (Action<object?>)(value => result = value)
        }, typeof(Nullable<>).MakeGenericType(type));
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, editor.EditorType(type))
            .Add(c => c.Parameters, parameters));

        component.Find("input").Input("3");
        Assert.AreEqual(Convert.ChangeType(3, type), result);
        component.Find("input").Input("");
        Assert.IsNull(result);
        component.Find("input").Input("0");
        Assert.AreEqual(Convert.ChangeType(0, type), result);
    }

    [TestMethod]
    public async Task RequiredIntegerUsesNumericFieldWithoutNullControls()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        object? result = 2;
        var component = context.Render<IntEditor>(p => p
            .Add(c => c.Value, 2)
            .Add(c => c.AllowNull, false)
            .Add(c => c.Setter, value => result = value));

        var field = component.FindComponent<MudBlazor.MudNumericField<int>>().Instance;
        Assert.IsFalse(field.HideSpinButtons);
        Assert.IsFalse(field.Clearable);
        Assert.IsNull(field.Placeholder);
        component.Find("input").Input("3");
        Assert.AreEqual(3, result);
        component.Find("input").Input("");
        Assert.IsInstanceOfType<int>(result);
    }

    [TestMethod]
    public async Task StringFieldCanBeEditedFromNullAndClearedBackToNull()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        object? result = null;
        var component = context.Render<StringEditor>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.Setter, value => result = value));

        Assert.AreEqual("<NULL>", component.Find("input").GetAttribute("placeholder"));
        Assert.IsTrue(string.IsNullOrEmpty(component.Find("input").GetAttribute("value")));
        component.Find("input").Input("hello");
        Assert.AreEqual("hello", result);
        component.Find("input").Input(" ");
        Assert.AreEqual(" ", result);
        component.Find("input").Input("");
        Assert.IsNull(result);
        component.Find("input").Input("again");
        Assert.AreEqual("again", result);
        component.Find("button").Click();
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task StringFieldDisplaysTheSuppliedValue()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StringEditor>(p => p
            .Add(c => c.Value, "existing text")
            .Add(c => c.Setter, _ => { }));

        Assert.AreEqual("existing text", component.Find("input").GetAttribute("value"));
    }

    [TestMethod]
    public async Task ReadOnlyStringHasNoClearControl()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StringEditor>(p => p
            .Add(c => c.Value, "hello")
            .Add(c => c.ReadOnly, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only editor invoked setter.")));

        Assert.IsTrue(component.Find("input").HasAttribute("disabled"));
        Assert.AreEqual(0, component.FindAll("button").Count);
    }

    [TestMethod]
    public void RequestPropertiesExposeCustomButExcludeCredentials()
    {
        var names = Settings.GetProperties(typeof(ProductSearchRequest)).Select(p => p.Name).ToArray();
        CollectionAssert.Contains(names, "Custom");
        CollectionAssert.DoesNotContain(names, "DatasetId");
        CollectionAssert.DoesNotContain(names, "APIKeySecret");
    }

    [TestMethod]
    public async Task FeedRequestsAppearInRecommendationDiscovery()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<Shared.RecommendationConstructor>(parameters => parameters.Get(p => p.RequestType).Name);
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var page = context.Render<Pages.Recommendations>();
        page.FindAll(".mud-nav-link").Single(link => link.TextContent.Trim() == "Feed Recommendation Initialization").Click();
        page.WaitForAssertion(() => StringAssert.Contains(page.Markup, "FeedRecommendationInitializationRequest"));
        page.FindAll(".mud-nav-link").Single(link => link.TextContent.Trim() == "Feed Recommendation Next Items").Click();
        page.WaitForAssertion(() => StringAssert.Contains(page.Markup, "FeedRecommendationNextItemsRequest"));
    }
}
