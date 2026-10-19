using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Relewise.BlazorApps.Components;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class NumericSpinnerTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NumericSpinnersFollowReadOnlyAndDisabledState(bool disabled)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var number = context.Render<RNullableNumberField<double>>(p => p
            .Add(c => c.Value, 12.75).Add(c => c.AllowNull, false).Add(c => c.ReadOnly, disabled));
        var integer = context.Render<RNumericField>(p => p
            .Add(c => c.Value, 10).Add(c => c.Required, true).Add(c => c.Disabled, disabled));
        var nullableInteger = context.Render<RNullableNumericField>(p => p
            .Add(c => c.Value, 10).Add(c => c.Required, true).Add(c => c.Disabled, disabled));

        Assert.AreEqual(disabled, number.FindComponent<MudNumericField<double?>>().Instance.HideSpinButtons);
        Assert.AreEqual(disabled, integer.FindComponent<MudNumericField<int>>().Instance.HideSpinButtons);
        Assert.AreEqual(disabled, nullableInteger.FindComponent<MudNumericField<int?>>().Instance.HideSpinButtons);
        Assert.AreEqual(!disabled, number.FindComponent<MudNumericField<double?>>().Instance.Required);
        Assert.AreEqual(!disabled, integer.FindComponent<MudNumericField<int>>().Instance.Required);
        Assert.AreEqual(!disabled, nullableInteger.FindComponent<MudNumericField<int?>>().Instance.Required);

        number.Render(p => p.Add(c => c.ReadOnly, !disabled));
        integer.Render(p => p.Add(c => c.Disabled, !disabled));
        nullableInteger.Render(p => p.Add(c => c.Disabled, !disabled));
        Assert.AreEqual(!disabled, number.FindComponent<MudNumericField<double?>>().Instance.HideSpinButtons);
        Assert.AreEqual(!disabled, integer.FindComponent<MudNumericField<int>>().Instance.HideSpinButtons);
        Assert.AreEqual(!disabled, nullableInteger.FindComponent<MudNumericField<int?>>().Instance.HideSpinButtons);
    }

    [TestMethod]
    [DataRow(false, false, false, false)]
    [DataRow(true, false, false, true)]
    [DataRow(false, true, false, true)]
    [DataRow(false, false, true, true)]
    public async Task DecimalSpinnersRespectDisabledReadOnlyAndExplicitHiding(bool disabled, bool readOnly, bool hide, bool expected)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<RDecimalField>(p => p
            .Add(c => c.Value, 5.5m).Add(c => c.Required, true).Add(c => c.Disabled, disabled)
            .Add(c => c.ReadOnly, readOnly).Add(c => c.HideSpinButtons, hide));

        Assert.AreEqual(expected, component.FindComponent<MudNumericField<decimal>>().Instance.HideSpinButtons);
        Assert.AreEqual(!disabled && !readOnly, component.FindComponent<MudNumericField<decimal>>().Instance.Required);
        var integer = context.Render<RNumericField>(p => p.Add(c => c.HideSpinButtons, true));
        Assert.IsTrue(integer.FindComponent<MudNumericField<int>>().Instance.HideSpinButtons);
    }
}
