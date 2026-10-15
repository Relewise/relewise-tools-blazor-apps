using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.Responses;
using Relewise.Client.Responses.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ResponseDocumentationLoadingTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ResponseResultsRenderWhileDocumentationDownloadIsPending(bool search)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(async request =>
        {
            started.TrySetResult();
            await release.Task;
            return DocumentationCacheTests.Response(request);
        }));
        context.Services.AddSingleton<DocumentationCache>();
        object response = search
            ? JsonConvert.DeserializeObject<ProductSearchResponse>("""{"Results":[{"ProductId":"fixture-1"}],"Hits":1}""")!
            : JsonConvert.DeserializeObject<ProductRecommendationResponse>("""{"Recommendations":[{"ProductId":"fixture-1"}]}""")!;
        string before = JsonConvert.SerializeObject(response);
        try
        {
            var component = context.Render<DynamicComponent>(p => p
                .Add(c => c.Type, search ? typeof(ObjectEditor<SearchResponse>) : typeof(ObjectEditor<RecommendationResponse>))
                .Add(c => c.Parameters, new Dictionary<string, object>
                {
                    ["Value"] = response, ["ReadOnly"] = true, ["SingleLevelOpenDetails"] = true,
                    ["OverridenHideDefaultValueProperties"] = true, ["Setter"] = (Action<object?>)(_ => Assert.Fail())
                }));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(release.Task.IsCompleted);
            component.WaitForAssertion(() =>
            {
                Assert.HasCount(1, component.FindAll(".result-toggle"));
                Assert.HasCount(0, component.FindAll(".response-empty"));
                StringAssert.Contains(component.Find(".result-toggle").TextContent, "fixture-1");
            });
            release.SetResult();
            component.WaitForAssertion(() => Assert.HasCount(1, component.FindAll(".result-toggle")));
            Assert.AreEqual(before, JsonConvert.SerializeObject(response));
        }
        finally
        {
            release.TrySetResult();
        }
    }
}
