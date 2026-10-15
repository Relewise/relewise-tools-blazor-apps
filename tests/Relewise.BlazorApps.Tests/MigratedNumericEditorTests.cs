using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Relewise.BlazorApps.TypeEditors;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class MigratedNumericEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task RequiredBytePreservesTypeAndRejectsNull()
    {
        await using var context = CreateContext();
        object? result = null;
        var component = context.Render<ByteEditor>(p => p.Add(c => c.Value, (byte)0).Add(c => c.AllowNull, false).Add(c => c.Setter, value => result = value));
        component.Find("input").Input("255");
        Assert.IsInstanceOfType<byte>(result);
        Assert.AreEqual((byte)255, result);
        await component.InvokeAsync(() => component.FindComponent<MudNumericField<byte?>>().Instance.ValueChanged.InvokeAsync(null));
        Assert.AreEqual((byte)255, component.Instance.Value);
    }

    [TestMethod]
    public async Task RequiredUshortPreservesUnsignedRange()
    {
        await using var context = CreateContext();
        object? result = null;
        var component = context.Render<UshortEditor>(p => p.Add(c => c.Value, (ushort)0).Add(c => c.AllowNull, false).Add(c => c.Setter, value => result = value));
        component.Find("input").Input("65535");
        Assert.IsInstanceOfType<ushort>(result);
        Assert.AreEqual(ushort.MaxValue, result);
        await component.InvokeAsync(() => component.FindComponent<MudNumericField<ushort?>>().Instance.ValueChanged.InvokeAsync(null));
        Assert.AreEqual(ushort.MaxValue, component.Instance.Value);
    }

    [TestMethod]
    public async Task DecimalRetainsPrecisionAndNullableClearing()
    {
        await using var context = CreateContext();
        object? result = null;
        var component = context.Render<DecimalEditor>(p => p.Add(c => c.Value, 0m).Add(c => c.AllowNull, true).Add(c => c.Setter, value => result = value));
        component.Find("input").Input("123456789.123456789");
        Assert.AreEqual(123456789.123456789m, result);
        await component.InvokeAsync(() => component.FindComponent<MudNumericField<decimal?>>().Instance.ValueChanged.InvokeAsync(null));
        Assert.IsNull(result);
        Assert.IsNull(component.Instance.Value);
    }

    [TestMethod]
    public async Task FloatRetainsClrTypeAndReadOnlyGuards()
    {
        await using var context = CreateContext();
        object? result = null;
        var component = context.Render<FloatEditor>(p => p.Add(c => c.Value, 0f).Add(c => c.Setter, value => result = value));
        component.Find("input").Input("1.25");
        Assert.IsInstanceOfType<float>(result);
        Assert.AreEqual(1.25f, result);
        component.Render(p => p.Add(c => c.ReadOnly, true));
        var field = component.FindComponent<MudNumericField<float?>>();
        Assert.IsTrue(field.Instance.HideSpinButtons);
        await component.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(9f));
        Assert.AreEqual(1.25f, result);
    }
}
