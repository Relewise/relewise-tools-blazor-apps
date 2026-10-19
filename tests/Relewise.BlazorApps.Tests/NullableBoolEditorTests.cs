using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Relewise.BlazorApps.Components;
using Relewise.BlazorApps.TypeEditors;
using static Relewise.BlazorApps.TypeEditors.NullableBoolEditor;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class NullableBoolEditorTests
{
    [TestMethod]
    public async Task MenuSelectionsRoundTripThroughTheRenderedControl()
    {
        await using var context = CreateContext();
        var popovers = context.Render<MudBlazor.MudPopoverProvider>();
        var results = new List<object?>();
        var component = context.Render<NullableBoolEditor>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.Setter, results.Add));

        foreach (var text in new[] { "True", "False", "<NULL>" })
        {
            await component.InvokeAsync(() => component.FindComponent<MudBlazor.MudSelect<BooleanOption>>().Instance.OpenMenu());
            popovers.FindAll("[role='option']").Single(item => item.TextContent.Trim() == text).Click();
            var select = component.FindComponent<RSelect<BooleanOption>>().Instance;
            Assert.AreEqual(text, select.ToStringFunc!(select.Value));
        }

        CollectionAssert.AreEqual(new object?[] { true, false, null }, results);
    }

    [TestMethod]
    [DataRow(null, BooleanOption.Null)]
    [DataRow(true, BooleanOption.True)]
    [DataRow(false, BooleanOption.False)]
    public async Task DisplaysAllThreeStatesAndFollowsExternalChanges(bool? value, BooleanOption option)
    {
        await using var context = CreateContext();
        var component = context.Render<NullableBoolEditor>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.AriaLabel, "AllowProductsCurrentlyInCart")
            .Add(c => c.Setter, _ => { }));
        var select = component.FindComponent<RSelect<BooleanOption>>();

        Assert.AreEqual(option, select.Instance.Value);
        CollectionAssert.AreEqual(new[] { BooleanOption.Null, BooleanOption.True, BooleanOption.False }, select.Instance.Items.ToArray());
        StringAssert.Contains(component.Markup, select.Instance.ToStringFunc!(option));
        Assert.HasCount(1, component.FindAll("[aria-label='AllowProductsCurrentlyInCart']"));
        Assert.IsFalse(select.Instance.Clearable);

        component.Render(p => p.Add(c => c.Value, !value));
        Assert.AreEqual(value is null ? BooleanOption.Null : value.Value ? BooleanOption.False : BooleanOption.True, select.Instance.Value);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SelectionPreservesNullableBooleanValuesAndReadOnlyGuards(bool readOnly)
    {
        await using var context = CreateContext();
        var results = new List<object?>();
        var component = context.Render<NullableBoolEditor>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.ReadOnly, readOnly)
            .Add(c => c.Setter, results.Add));
        var select = component.FindComponent<RSelect<BooleanOption>>();

        Assert.AreEqual(readOnly, select.Instance.Disabled);
        foreach (var option in new[] { BooleanOption.True, BooleanOption.False, BooleanOption.Null })
            await component.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(option));

        if (readOnly)
        {
            Assert.HasCount(0, results);
            Assert.IsNull(component.Instance.Value);
        }
        else
        {
            CollectionAssert.AreEqual(new object?[] { true, false, null }, results);
            Assert.IsNull(component.Instance.Value);
        }
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
