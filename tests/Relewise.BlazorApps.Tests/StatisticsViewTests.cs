using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.Components;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.Responses;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class StatisticsViewTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ResponseStatisticsShowBothValuesInlineWithoutExpansion(bool openDetails)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var response = JsonConvert.DeserializeObject<ProductRecommendationResponse>(
            """{"Recommendations":[],"Statistics":{"ServerTimeInMs":12.75,"NetworkTimeInMs":0}}""")!;
        string before = JsonConvert.SerializeObject(response);
        var component = context.Render<ObjectEditor<RecommendationResponse>>(p => p
            .Add(c => c.Value, response)
            .Add(c => c.ReadOnly, true)
            .Add(c => c.OpenDetails, openDetails)
            .Add(c => c.OverridenHideDefaultValueProperties, true)
            .Add(c => c.Setter, _ => Assert.Fail("A response must remain read-only.")));

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(1, component.FindAll(".statistics-inline"));
            Assert.HasCount(2, component.FindAll(".statistics-inline input[disabled]"));
            Assert.HasCount(0, component.FindAll("details"));
            var fields = component.FindComponents<RNullableNumberField<double>>();
            Assert.HasCount(2, fields);
            Assert.AreEqual(12.75, fields.Single(field => field.Instance.Label == "ServerTimeInMs").Instance.Value);
            Assert.AreEqual(0.0, fields.Single(field => field.Instance.Label == "NetworkTimeInMs").Instance.Value);
            Assert.IsTrue(fields.All(field => field.Instance.ReadOnly && !field.Instance.AllowNull));
        });
        Assert.AreEqual(before, JsonConvert.SerializeObject(response));
    }
}
