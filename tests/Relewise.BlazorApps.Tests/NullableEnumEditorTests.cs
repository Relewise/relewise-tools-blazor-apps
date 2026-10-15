using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Relewise.BlazorApps.Components;
using Relewise.BlazorApps.TypeEditors;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class NullableEnumEditorTests
{
    [TestMethod]
    public async Task MenuRoundTripsNullAndEnumValuesIncludingDefinedZero()
    {
        await using var context = CreateContext();
        var popovers = context.Render<MudPopoverProvider>();
        var results = new List<object?>();
        var component = context.Render<NullableEnumEditor<DayOfWeek>>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.Setter, results.Add));

        Assert.AreEqual(0, component.FindAll("select").Count);
        foreach (var text in new[] { "Sunday", "Wednesday", "<NULL>" })
        {
            await component.InvokeAsync(() => component.FindComponent<MudSelect<NullableEnumEditor<DayOfWeek>.EnumOption>>().Instance.OpenMenu());
            popovers.FindAll("[role='option']").Single(item => item.TextContent.Trim() == text).Click();
            Assert.AreEqual(text, component.Find("input[role='combobox']").GetAttribute("value"));
        }

        CollectionAssert.AreEqual(new object?[] { DayOfWeek.Sunday, DayOfWeek.Wednesday, null }, results);
        Assert.IsNull(component.Instance.Value);
    }

    [TestMethod]
    public async Task FollowsExternalValuesAndGuardsReadOnlyUpdates()
    {
        await using var context = CreateContext();
        var component = context.Render<NullableEnumEditor<DayOfWeek>>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.Setter, _ => Assert.Fail("Unexpected setter invocation.")));
        var select = component.FindComponent<RSelect<NullableEnumEditor<DayOfWeek>.EnumOption>>();
        Assert.IsNull(select.Instance.Value.Value);
        Assert.AreEqual(8, select.Instance.Items.Count());
        Assert.IsFalse(select.Instance.Clearable);

        component.Render(p => p.Add(c => c.Value, DayOfWeek.Friday));
        Assert.AreEqual(DayOfWeek.Friday, select.Instance.Value.Value);
        component.Render(p => p.Add(c => c.Value, null));
        Assert.IsNull(select.Instance.Value.Value);

        var callback = select.Instance.ValueChanged;
        component.Render(p => p.Add(c => c.ReadOnly, true));
        Assert.AreEqual(0, component.FindAll("select, input, button").Count);
        await component.InvokeAsync(() => callback.InvokeAsync(new(DayOfWeek.Monday)));
        Assert.IsNull(component.Instance.Value);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
