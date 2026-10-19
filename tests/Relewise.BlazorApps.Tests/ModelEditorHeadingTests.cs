using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Relewise.BlazorApps.Components;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ModelEditorHeadingTests
{
    [TestMethod]
    public async Task ComplexModelHasOneHeadingAndKeepsCopyActions()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var component = context.Render<ModelEditor>(p => p
            .Add(c => c.ModelType, typeof(ProductAndVariantId))
            .Add(c => c.State, new ModelEditor.EditorState { Initialized = true, Value = new ProductAndVariantId("demo", "variant") }));
        Assert.AreEqual(1, component.FindAll("h3").Count);
        Assert.AreEqual(1, component.FindAll("button[aria-label='Copy object as JSON']").Count);
        Assert.AreEqual(1, component.FindAll("button[aria-label='Copy model link']").Count);
    }

    [TestMethod]
    public async Task PrimitiveModelRetainsItsHeading()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<ModelEditor>(p => p
            .Add(c => c.ModelType, typeof(string))
            .Add(c => c.State, new ModelEditor.EditorState { Initialized = true, Value = "demo" }));
        Assert.AreEqual(1, component.FindAll("h3").Count);
    }
}
