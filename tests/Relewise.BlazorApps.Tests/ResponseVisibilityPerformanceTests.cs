using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.Responses.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ResponseVisibilityPerformanceTests
{
    [TestMethod]
    public async Task ResponseVisibilityDoesNotReserializePayloadOnRendersOrEditorRecreation()
    {
        await using var context = CreateContext();
        var payload = new CountedPayload { Text = "fixture" };
        var value = new VisibilityFixture { Payload = payload };
        var component = RenderResponse(context, value);
        Assert.IsTrue(payload.Serializations > 0);
        int serialized = payload.Serializations;
        component.Render(p => p.Add(c => c.OpenDetails, true));
        component.Render(p => p.Add(c => c.OpenDetails, false));
        RenderResponse(context, value);
        Assert.AreEqual(serialized, payload.Serializations, "Expansion must reuse comparisons even when editors are recreated.");

        var replacement = new VisibilityFixture { Payload = new CountedPayload { Text = "replacement" } };
        component.Render(p => p.Add(c => c.Value, replacement));
        Assert.IsTrue(replacement.Payload.Serializations > 0);
        Assert.AreEqual(serialized, payload.Serializations);
    }

    [TestMethod]
    public async Task EditableValuesRecomputeVisibilityAfterInPlaceChanges()
    {
        await using var context = CreateContext();
        var value = new VisibilityFixture { Payload = new CountedPayload { Text = "fixture" } };
        var component = RenderResponse(context, value);
        component.Render(p => p.Add(c => c.ReadOnly, false));
        value.Payload = null;
        component.Render(p => p.Add(c => c.Value, value));
        Assert.HasCount(0, component.FindAll(".object-property"));
        value.Payload = new CountedPayload { Text = "edited" };
        component.Render(p => p.Add(c => c.Value, value));
        Assert.HasCount(1, component.FindAll(".object-property"));
        Assert.IsTrue(value.Payload.Serializations > 0);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        context.ComponentFactories.AddStub<ObjectEditor<CountedPayload>>();
        return context;
    }

    private static IRenderedComponent<ObjectEditor<VisibilityFixture>> RenderResponse(BunitContext context, VisibilityFixture value)
        => context.Render<ObjectEditor<VisibilityFixture>>(p => p.Add(c => c.Value, value)
            .Add(c => c.ReadOnly, true).Add(c => c.SingleLevelOpenDetails, true)
            .Add(c => c.OverridenHideDefaultValueProperties, true)
            .Add(c => c.AncestorTypes, new[] { typeof(ProductSearchResponse) })
            .Add(c => c.Setter, _ => Assert.Fail()));

    public class VisibilityFixture
    {
        public CountedPayload? Payload { get; set; }
    }

    [JsonConverter(typeof(CountedPayloadConverter))]
    public class CountedPayload
    {
        public string Text { get; set; } = "";
        public int Serializations { get; set; }
    }

    public class CountedPayloadConverter : JsonConverter<CountedPayload>
    {
        public override void WriteJson(JsonWriter writer, CountedPayload? value, JsonSerializer serializer)
        {
            value!.Serializations++;
            writer.WriteValue(value.Text);
        }

        public override CountedPayload? ReadJson(JsonReader reader, Type objectType, CountedPayload? existingValue, bool hasExistingValue, JsonSerializer serializer)
            => throw new NotSupportedException();
    }
}
