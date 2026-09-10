using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.Requests.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class EditorTests
{
    [TestMethod]
    [DataRow(typeof(byte))]
    [DataRow(typeof(ushort))]
    [DataRow(typeof(long))]
    [DataRow(typeof(int))]
    [DataRow(typeof(double))]
    public void NumericDefaultsCanBePassedToTheSelectedComponent(Type type)
    {
        var handler = Settings.Editors.First(editor => editor.CanHandle(type));
        var value = handler.InitValue(type);
        Assert.IsNotNull(value);
        Assert.AreEqual(type, value.GetType());
        using var context = new BunitContext();
        var component = context.Render<DynamicComponent>(parameters => parameters
            .Add(p => p.Type, handler.EditorType(type))
            .Add(p => p.Parameters, new Dictionary<string, object>
            {
                ["Value"] = value,
                ["Setter"] = (Action<object?>)(_ => { })
            }));
        Assert.AreEqual("0", component.Find("input").GetAttribute("value"));
    }

    [TestMethod]
    public void NullableByteCanBeClearedAndRecreated()
    {
        using var context = new BunitContext();
        object? result = (byte)7;
        var component = context.Render<ByteEditor>(parameters => parameters
            .Add(p => p.Value, (byte?)7)
            .Add(p => p.AllowNull, true)
            .Add(p => p.Setter, value => result = value));
        component.Find("button").Click();
        Assert.IsNull(result);
        Assert.AreEqual(0, component.FindAll("input").Count);
        component.Find("button").Click();
        Assert.AreEqual((byte)0, result);
        Assert.AreEqual("0", component.Find("input").GetAttribute("value"));
    }

    [TestMethod]
    public void ReadOnlyByteDoesNotOfferMutationControls()
    {
        using var context = new BunitContext();
        var component = context.Render<ByteEditor>(parameters => parameters
            .Add(p => p.Value, (byte?)7)
            .Add(p => p.AllowNull, true)
            .Add(p => p.ReadOnly, true)
            .Add(p => p.Setter, _ => Assert.Fail("Read-only editor invoked setter.")));
        Assert.AreEqual(0, component.FindAll("button").Count);
        Assert.IsTrue(component.Find("input").HasAttribute("disabled"));
    }

    [TestMethod]
    public void DoubleEditorAcceptsIntegralJsonValues()
    {
        using var context = new BunitContext();
        object? result = null;
        var component = context.Render<DoubleEditor>(parameters => parameters
            .Add(p => p.Value, 42L)
            .Add(p => p.Setter, value => result = value));
        component.Find("input").Input("42.5");
        Assert.AreEqual(42.5, result);
    }

    [TestMethod]
    public void RequestPropertiesExposeCustomButExcludeCredentials()
    {
        var names = Settings.GetProperties(typeof(ProductSearchRequest)).Select(p => p.Name).ToArray();
        CollectionAssert.Contains(names, "Custom");
        CollectionAssert.DoesNotContain(names, "DatasetId");
        CollectionAssert.DoesNotContain(names, "APIKeySecret");
    }

    [TestMethod]
    public void FeedRequestsAppearInRecommendationDiscovery()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<Shared.RecommendationConstructor>(parameters => parameters.Get(p => p.RequestType).Name);
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var page = context.Render<Pages.Recommendations>();
        page.WaitForAssertion(() =>
        {
            StringAssert.Contains(page.Markup, "FeedRecommendationInitializationRequest");
            StringAssert.Contains(page.Markup, "FeedRecommendationNextItemsRequest");
        });
    }
}
