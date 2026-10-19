using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Relewise.BlazorApps.TypeEditors;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class LongEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task MudNumericFieldPreservesLargeLongValuesAndCannotSetNull()
    {
        await using var context = CreateContext();
        object? result = null;
        var component = context.Render<LongEditor>(p => p
            .Add(c => c.Value, 0L).Add(c => c.Setter, value => result = value));
        var field = component.FindComponent<MudNumericField<decimal?>>();
        Assert.IsFalse(field.Instance.HideSpinButtons);
        Assert.IsFalse(field.Instance.Clearable);
        component.Find("input").Input("9007199254740993");
        Assert.IsInstanceOfType<long>(result);
        Assert.AreEqual(9007199254740993L, result);
        await component.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(null));
        Assert.AreEqual(9007199254740993L, component.Instance.Value);
        Assert.AreEqual(9007199254740993L, result);
    }

    [TestMethod]
    public async Task ReadOnlyLongCannotChangeAndHidesSpinners()
    {
        await using var context = CreateContext();
        var component = context.Render<LongEditor>(p => p
            .Add(c => c.Value, 42L).Add(c => c.ReadOnly, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only value changed.")));
        var field = component.FindComponent<MudNumericField<decimal?>>();
        Assert.IsTrue(field.Instance.Disabled);
        Assert.IsTrue(field.Instance.HideSpinButtons);
        await component.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(99L));
        Assert.AreEqual(42L, component.Instance.Value);
    }

    [TestMethod]
    public async Task SpinnerPreservesLargeIntegersAndClampsAtLongBoundaries()
    {
        await using var context = CreateContext();
        object? result = null;
        var component = context.Render<LongEditor>(p => p
            .Add(c => c.Value, 9007199254740993L).Add(c => c.Setter, value => result = value));
        component.Find("button[aria-label='Increment']").Click();
        Assert.AreEqual(9007199254740994L, result);
        component.Find("button[aria-label='Decrement']").Click();
        Assert.AreEqual(9007199254740993L, result);
        component.Render(p => p.Add(c => c.Value, long.MaxValue));
        component.Find("button[aria-label='Increment']").Click();
        Assert.AreEqual(long.MaxValue, component.Instance.Value);
        component.Render(p => p.Add(c => c.Value, long.MinValue));
        component.Find("button[aria-label='Decrement']").Click();
        Assert.AreEqual(long.MinValue, component.Instance.Value);
        await component.InvokeAsync(() => component.FindComponent<MudNumericField<decimal?>>().Instance.ValueChanged.InvokeAsync(1.5m));
        Assert.AreEqual(long.MinValue, component.Instance.Value);
    }
}
