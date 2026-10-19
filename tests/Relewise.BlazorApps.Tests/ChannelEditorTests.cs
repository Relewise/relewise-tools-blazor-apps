using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ChannelEditorTests
{
    [TestMethod]
    public void DeprecatedChannelAliasesAreHiddenAcrossTheSdk()
    {
        var aliases = typeof(User).Assembly.GetTypes().SelectMany(type => type.GetProperties()
            .Where(property => property.Name == "Channel" && property.PropertyType == typeof(Channel)
                && property.GetCustomAttribute<ObsoleteAttribute>() is not null
                && type.GetProperty("User")?.PropertyType == typeof(User))
            .Select(property => (Type: type, Property: property))).ToArray();
        Assert.IsTrue(aliases.Length > 0);
        foreach (var alias in aliases)
        {
            Assert.IsFalse(Settings.GetProperties(alias.Type).Contains(alias.Property), alias.Type.FullName);
            Assert.IsTrue(Settings.GetProperties(alias.Type).Any(property => property.Name == "User"), alias.Type.FullName);
        }
        Console.WriteLine($"Hidden aliases on {aliases.Length} SDK types: {string.Join(", ", aliases.Select(alias => alias.Type.Name))}");
        Assert.IsTrue(Settings.GetProperties(typeof(User)).Any(property => property.Name == "Channel"));
        Assert.IsTrue(Settings.GetProperties(typeof(Channel)).Any(property => property.Name == "SubChannel"));
    }

    [TestMethod]
    [DataRow(typeof(ProductSearchRequest))]
    [DataRow(typeof(PopularProductsRequest))]
    public async Task ChannelCanBeCreatedEditedAndRemovedUnderUser(Type requestType)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var request = Activator.CreateInstance(requestType)!;
        var userProperty = requestType.GetProperty("User")!;
        var component = context.Render<ObjectEditor<LicensedRequest>>(p => p
            .Add(c => c.Value, request)
            .Add(c => c.OpenDetails, true)
            .Add(c => c.Setter, _ => { }));

        Assert.IsFalse(component.FindAll(".object-property-label").Any(label => label.TextContent == "Channel"));
        var userRow = component.FindAll(".object-property").Single(row => row.QuerySelector(".object-property-label")?.TextContent == "User");
        userRow.QuerySelector(".null-object-field")!.Click();
        var user = (User)userProperty.GetValue(request)!;
        Assert.IsNotNull(user);
        var channelRow = component.FindAll(".object-property").Single(row => row.QuerySelector(".object-property-label")?.TextContent == "Channel");
        channelRow.QuerySelector(".null-object-field")!.Click();
        channelRow = component.FindAll(".object-property").Single(row => row.QuerySelector(".object-property-label")?.TextContent == "Channel");
        channelRow.QuerySelector("input")!.Input("Web");
        Assert.AreEqual("Web", user.Channel!.Name);
        Assert.AreEqual("Web", JObject.Parse(JsonConvert.SerializeObject(request))["User"]?["Channel"]?["Name"]?.Value<string>());
        component.Find("button[aria-label='Remove Channel']").Click();
        Assert.IsNull(user.Channel);
        Assert.AreSame(user, userProperty.GetValue(request));
    }
}
