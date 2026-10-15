using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Relewise.BlazorApps.TypeEditors;
using Relewise.Client.Requests.RelevanceModifiers;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class DoubleEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task WeightMultiplierUsesOutlinedMudFieldAndPreservesDoubleValues()
    {
        await using var context = CreateContext();
        var propertyType = typeof(ProductAssortmentRelevanceModifier).GetProperty("MultiplyWeightBy")!.PropertyType;
        var parameters = Settings.WithNumericNullability(new Dictionary<string, object>(), propertyType);
        object? result = null;
        var component = context.Render<DoubleEditor>(p => p.Add(c => c.Value, 0L)
            .Add(c => c.AllowNull, (bool)parameters["AllowNull"])
            .Add(c => c.Setter, value => result = value));
        var field = component.FindComponent<MudNumericField<double?>>().Instance;
        Assert.AreEqual(Variant.Outlined, field.Variant);
        Assert.IsFalse(field.Clearable);
        Assert.IsFalse(field.HideSpinButtons);
        component.Find("input").Input("1.25");
        Assert.IsInstanceOfType<double>(result);
        Assert.AreEqual(1.25, result);
        component.Find("input").Input("");
        Assert.AreEqual(1.25, result);
    }

    [TestMethod]
    public async Task NullableDoubleStillSupportsClearing()
    {
        await using var context = CreateContext();
        object? result = 2.5;
        var component = context.Render<DoubleEditor>(p => p.Add(c => c.Value, result)
            .Add(c => c.AllowNull, true).Add(c => c.Setter, value => result = value));
        component.Find("input").Input("");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ReadOnlyMultiplierCannotChangeThroughFieldOrSet()
    {
        await using var context = CreateContext();
        var component = context.Render<DoubleEditor>(p => p.Add(c => c.Value, 1.5)
            .Add(c => c.AllowNull, false).Add(c => c.ReadOnly, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only multiplier changed.")));
        var field = component.FindComponent<MudNumericField<double?>>().Instance;
        Assert.IsTrue(field.Disabled);
        Assert.IsTrue(field.HideSpinButtons);
        await component.InvokeAsync(() => field.ValueChanged.InvokeAsync(5));
        await component.InvokeAsync(component.Instance.Set);
        Assert.AreEqual(1.5, component.Instance.Value);
    }
}
