using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.Pages;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Search;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ModelShareLinkTests
{
    [TestMethod]
    [DataRow(typeof(SimilarProductsRequest), "Recommendations")]
    [DataRow(typeof(ProductSearchRequest), "Searches")]
    [DataRow(typeof(ProductRecommendationRequestCollection), "Recommendations")]
    [DataRow(typeof(User), "Models")]
    public void SharedLinksUseTheExecutablePageAndPreserveTheDeploymentPrefix(Type type, string page)
    {
        Assert.AreEqual($"https://example.test/tools/{page}?q={type.Name}&o=a%2Bb%2Fc%3D",
            ModelShareLink.Create("https://example.test/tools/", type, "a+b/c="));
    }

    [TestMethod]
    public async Task CopyingSimilarProductsLinkPreservesRequestValues()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var request = JsonConvert.DeserializeObject<SimilarProductsRequest>("""{"ExistingProductId":{"ProductId":"fixture-1"}}""")!;
        var component = context.Render<ObjectEditor<SimilarProductsRequest>>(p => p
            .Add(c => c.Value, request).Add(c => c.Setter, _ => { }));
        await component.InvokeAsync(component.Instance.CopyLinkToClipboard);

        var call = context.JSInterop.Invocations.Single(invocation => invocation.Identifier == "navigator.clipboard.writeText");
        string link = (string)call.Arguments[0]!;
        StringAssert.StartsWith(link, "http://localhost/Recommendations?q=SimilarProductsRequest&o=");
        string compressed = Uri.UnescapeDataString(link.Split("&o=")[1]);
        string json = await Models.FromGzipAsync(compressed);
        var restored = JsonConvert.DeserializeObject<Relewise.Client.Requests.LicensedRequest>(json,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
        Assert.IsInstanceOfType<SimilarProductsRequest>(restored);
        Assert.AreEqual("fixture-1", ((SimilarProductsRequest)restored).ExistingProductId.ProductId);
    }
}
