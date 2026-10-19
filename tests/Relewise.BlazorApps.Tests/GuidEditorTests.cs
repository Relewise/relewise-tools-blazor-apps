using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Relewise.BlazorApps.Components;
using Relewise.BlazorApps.TypeEditors;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class GuidEditorTests
{
    [TestMethod]
    public async Task EditingPreservesGuidTypeAndRejectsInvalidDraftsAcrossRerenders()
    {
        await using var context = CreateContext();
        var initial = Guid.Empty;
        var expected = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        object? result = initial;
        var component = context.Render<GuidEditor>(p => p
            .Add(c => c.Value, initial)
            .Add(c => c.Setter, value => result = value));

        component.Find("input").Input(expected.ToString());
        Assert.IsInstanceOfType<Guid>(result);
        Assert.AreEqual(expected, result);

        component.Find("input").Input("invalid");
        component.Render(p => p.Add(c => c.Value, expected));
        Assert.AreEqual(expected, result);
        Assert.AreEqual("invalid", component.Find("input").GetAttribute("value"));
        Assert.IsTrue(component.FindComponent<RTextField>().Instance.Error);

        component.Render(p => p.Add(c => c.Value, initial));
        Assert.AreEqual(initial.ToString(), component.Find("input").GetAttribute("value"));
        Assert.IsFalse(component.FindComponent<RTextField>().Instance.Error);
    }

    [TestMethod]
    public async Task ClearingAndCreatingPreserveNullAndEmptyGuid()
    {
        await using var context = CreateContext();
        object? result = Guid.Empty;
        var component = context.Render<GuidEditor>(p => p
            .Add(c => c.Value, Guid.Empty)
            .Add(c => c.Setter, value => result = value));

        component.Find("input").Input("");
        Assert.IsNull(result);
        component.Find("input").Input(Guid.Empty.ToString());
        Assert.AreEqual(Guid.Empty, result);
        Assert.AreEqual(Guid.Empty.ToString(), component.Find("input").GetAttribute("value"));
    }

    [TestMethod]
    public async Task ReadOnlyGuardsEditingAndCreation()
    {
        await using var context = CreateContext();
        var calls = 0;
        var component = context.Render<GuidEditor>(p => p
            .Add(c => c.Value, Guid.Empty)
            .Add(c => c.ReadOnly, true)
            .Add(c => c.Setter, _ => calls++));
        var field = component.FindComponent<RTextField>().Instance;
        Assert.IsTrue(field.Disabled);
        Assert.IsTrue(field.ReadOnly);
        await component.InvokeAsync(() => field.ValueChanged.InvokeAsync(Guid.NewGuid().ToString()));
        await component.InvokeAsync(component.Instance.Create);
        Assert.AreEqual(0, calls);

        component.Render(p => p.Add(c => c.Value, null));
        Assert.AreEqual(0, component.FindAll("button").Count);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
