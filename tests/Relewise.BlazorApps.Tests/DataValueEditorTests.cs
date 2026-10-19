using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.TypeEditors;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class DataValueEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [TestMethod]
    public async Task CreatingNullDataValueOpensFieldsAndPreservesTypedSerialization()
    {
        await using var context = CreateContext();
        DataValue? result = null;
        var component = context.Render<DataValueEditor>(p => p
            .Add(c => c.Value, null).Add(c => c.Setter, value => result = (DataValue?)value));
        Assert.HasCount(0, component.FindAll("details"));
        component.Find(".data-value-null").Click();
        Assert.IsNotNull(result);
        Assert.IsTrue(component.Find("details").HasAttribute("open"));
        component.FindComponent<StringEditor>().Find("input").Input("fixture");
        var restored = JsonConvert.DeserializeObject<DataValue>(JsonConvert.SerializeObject(result))!;
        Assert.AreEqual(DataValue.DataValueTypes.String, restored.Type);
        Assert.AreEqual("fixture", restored.Value);
        await component.InvokeAsync(() => component.Instance.SetType(DataValue.DataValueTypes.Boolean));
        Assert.AreEqual(DataValue.DataValueTypes.Boolean, result.Type);
        Assert.AreEqual(false, result.Value);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InlineAndBoxedValuesKeepReadOnlyGuards(bool inline)
    {
        await using var context = CreateContext();
        var value = new DataValue("fixture");
        var component = context.Render<DataValueEditor>(p => p
            .Add(c => c.Value, value).Add(c => c.ReadOnly, true)
            .Add(c => c.Inline, inline).Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, _ => Assert.Fail("Read-only value was changed.")));
        Assert.HasCount(inline ? 0 : 1, component.FindAll("summary .data-value-title"));
        Assert.HasCount(0, component.FindAll("summary i"));
        await component.InvokeAsync(() => component.Instance.SetType(DataValue.DataValueTypes.Double));
        await component.InvokeAsync(component.Instance.Create);
        Assert.AreSame(value, component.Instance.Value);
        Assert.AreEqual("fixture", value.Value);
    }
}
