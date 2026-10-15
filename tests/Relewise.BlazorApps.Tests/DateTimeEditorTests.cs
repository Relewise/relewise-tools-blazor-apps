using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Relewise.BlazorApps.Components;
using Relewise.BlazorApps.TypeEditors;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class DateTimeEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task DateChangesPreserveUtcKindAndFractionalTimeAndTimeChangesKeepDate()
    {
        await using var context = CreateContext();
        var initial = new DateTime(2026, 10, 12, 13, 14, 15, DateTimeKind.Utc).AddTicks(1234);
        object? saved = null;
        var component = context.Render<DateTimeEditor>(p => p
            .Add(c => c.Value, initial).Add(c => c.Setter, value => saved = value));

        await component.InvokeAsync(() => component.FindComponent<MudDatePicker>().Instance.DateChanged.InvokeAsync(new DateTime(2026, 11, 2)));
        Assert.IsInstanceOfType<DateTime>(saved);
        Assert.AreEqual(new DateTime(2026, 11, 2, 13, 14, 15, DateTimeKind.Utc).AddTicks(1234), (DateTime)saved!);
        Assert.AreEqual(DateTimeKind.Utc, ((DateTime)saved!).Kind);
        await component.InvokeAsync(() => component.FindComponent<MudTimePicker>().Instance.TimeChanged.InvokeAsync(new TimeSpan(16, 17, 18)));
        Assert.AreEqual(new DateTime(2026, 11, 2, 16, 17, 18, DateTimeKind.Utc), (DateTime)saved!);
        Assert.AreEqual(DateTimeKind.Utc, ((DateTime)saved!).Kind);
    }

    [TestMethod]
    public async Task OffsetDateChangesPreserveOffsetAndFractionalTimeAndAllowClearing()
    {
        await using var context = CreateContext();
        var initial = new DateTimeOffset(2026, 10, 12, 13, 14, 15, TimeSpan.FromHours(5.5)).AddTicks(1234);
        object? saved = null;
        var component = context.Render<DateTimeOffsetEditor>(p => p
            .Add(c => c.Value, initial).Add(c => c.Setter, value => saved = value));

        await component.InvokeAsync(() => component.FindComponent<MudDatePicker>().Instance.DateChanged.InvokeAsync(new DateTime(2026, 11, 2)));
        Assert.IsInstanceOfType<DateTimeOffset>(saved);
        var date = (DateTimeOffset)saved!;
        Assert.AreEqual(initial.Offset, date.Offset);
        Assert.AreEqual(initial.TimeOfDay, date.TimeOfDay);
        Assert.AreEqual(new DateTime(2026, 11, 2), date.Date);
        await component.InvokeAsync(() => component.FindComponent<MudDatePicker>().Instance.DateChanged.InvokeAsync(null));
        Assert.IsNull(saved);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadOnlyDatesUseOutlinedFieldsAndRejectChanges(bool offset)
    {
        await using var context = CreateContext();
        int saves = 0;
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, offset ? typeof(DateTimeOffsetEditor) : typeof(DateTimeEditor))
            .Add(c => c.Parameters, new Dictionary<string, object>
            {
                ["Value"] = offset ? (object)new DateTimeOffset(2026, 10, 12, 13, 14, 15, TimeSpan.FromHours(2)) : new DateTime(2026, 10, 12, 13, 14, 15, DateTimeKind.Utc),
                ["ReadOnly"] = true, ["Setter"] = (Action<object?>)(_ => saves++)
            }));
        var field = component.FindComponent<RTextField>();
        Assert.IsTrue(field.Instance.Disabled);
        Assert.AreEqual(Variant.Outlined, field.Instance.Variant);
        StringAssert.Contains(field.Instance.Value!, offset ? "+02:00" : "UTC");
        Assert.HasCount(0, component.FindComponents<MudDatePicker>());
        await component.InvokeAsync(() => component.FindComponent<RDateTimeField>().Instance.ValueChanged.InvokeAsync(DateTime.Today));
        Assert.AreEqual(0, saves);
    }

    [TestMethod]
    public async Task RequiredDateCannotBeClearedAndNullableDateCanBeCreatedInline()
    {
        await using var context = CreateContext();
        int saves = 0;
        var required = context.Render<DateTimeEditor>(p => p
            .Add(c => c.Value, DateTime.MinValue).Add(c => c.AllowNull, false).Add(c => c.Setter, _ => saves++));
        Assert.IsFalse(required.FindComponent<MudDatePicker>().Instance.Clearable);
        await required.InvokeAsync(() => required.FindComponent<MudDatePicker>().Instance.DateChanged.InvokeAsync(null));
        Assert.AreEqual(0, saves);
        object? saved = null;
        var nullable = context.Render<DateTimeEditor>(p => p
            .Add(c => c.Value, null).Add(c => c.Setter, value => saved = value));
        await nullable.InvokeAsync(() => nullable.FindComponent<MudDatePicker>().Instance.DateChanged.InvokeAsync(new DateTime(2026, 10, 12)));
        Assert.IsInstanceOfType<DateTime>(saved);
        Assert.AreEqual(new DateTime(2026, 10, 12), saved);
        var requiredParameters = Settings.WithNumericNullability(new Dictionary<string, object>(), typeof(DateTimeOffset));
        var nullableParameters = Settings.WithNumericNullability(new Dictionary<string, object>(), typeof(DateTimeOffset?));
        Assert.AreEqual(false, requiredParameters["AllowNull"]);
        Assert.AreEqual(true, nullableParameters["AllowNull"]);
    }
}
