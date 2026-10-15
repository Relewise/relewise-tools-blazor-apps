using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using MudBlazor;
using Newtonsoft.Json;
using Relewise.BlazorApps.Components;
using Relewise.Client.DataTypes.Merchandising.Rules;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class MerchandisingRuleListTests
{
    [TestMethod]
    public async Task RulesKeepPriorityOrderAndEditTheSelectedInstance()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var rules = JsonConvert.DeserializeObject<FilterRule[]>("""
            [{"Id":"00000000-0000-0000-0000-000000000001","Name":"Later rule","Priority":20,"Enabled":false},
             {"Id":"00000000-0000-0000-0000-000000000002","Name":"First rule","Priority":1,"Enabled":true,"Description":"Example rule description"}]
            """)!;
        MerchandisingRule? edited = null;
        var component = context.Render<MerchandisingRuleList>(p => p
            .Add(c => c.Rules, rules).Add(c => c.OnEdit, rule => edited = rule));
        var rows = component.FindAll("tbody tr.mud-table-row");
        Assert.HasCount(2, rows);
        Assert.AreEqual("First rule", rows[0].QuerySelector("td")!.TextContent.Trim());
        Assert.AreEqual("Enabled", rows[0].QuerySelector(".mud-chip")!.TextContent.Trim());
        Assert.AreEqual("Disabled", rows[1].QuerySelector(".mud-chip")!.TextContent.Trim());
        StringAssert.Contains(rows[0].TextContent, "Example rule description");
        await rows[0].QuerySelector("button[aria-label='Edit First rule']")!.ClickAsync(new());
        Assert.AreSame(rules[1], edited);
        Assert.AreEqual(20, rules[0].Priority);
        Assert.IsFalse(rules[0].Enabled);
    }

    [TestMethod]
    public async Task EmptyDatasetHasAnExplicitEmptyState()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<MerchandisingRuleList>(p => p.Add(c => c.Rules, []));
        Assert.HasCount(0, component.FindComponent<MudDataGrid<ConfigurationListItem<MerchandisingRule>>>().Instance.Items!);
        Assert.HasCount(1, component.FindAll(".mud-table-empty-row"));
    }
}
