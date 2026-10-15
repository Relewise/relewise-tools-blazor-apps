using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;
using Relewise.Client.Requests;
using Relewise.Client.Responses;
using Relewise.Client.Responses.Search;
using Relewise.Client.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.Client;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class RequestFailureTests
{
    [TestMethod]
    [DataRow(false, HttpStatusCode.BadRequest)]
    [DataRow(true, HttpStatusCode.BadRequest)]
    [DataRow(false, HttpStatusCode.Unauthorized)]
    [DataRow(true, HttpStatusCode.ServiceUnavailable)]
    public async Task FailedRequestKeepsPostEnabledAndCanBeRetried(bool search, HttpStatusCode status)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(new UnusedInProcessRuntime());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<ObjectEditor<LicensedRequest>>();
        context.ComponentFactories.AddStub<ObjectEditor<SearchRequest>>();
        context.ComponentFactories.AddStub<ObjectEditor<RecommendationResponse>>();
        context.ComponentFactories.AddStub<ObjectEditor<SearchResponse>>();
        int attempts = 0;
        using var http = DocumentationCacheTests.CreateClient(_ =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{}")
            });
        });
        ClientBase client = search
            ? new Searcher(Guid.NewGuid(), "synthetic-api-key", "https://fixture.invalid")
            : new Recommender(Guid.NewGuid(), "synthetic-api-key", "https://fixture.invalid");
        client.OverriddenHttpClient = http;
        client.UseFastBinarySerializer = false;
        client.UseJsonCompression = false;
        object request = search ? new ProductSearchRequest() : new PopularProductsRequest();
        var parameters = new Dictionary<string, object>
        {
            [search ? "Searcher" : "Recommender"] = client,
            ["Connect"] = (Func<bool>)(() => true),
            ["Request"] = request,
            ["RequestType"] = request.GetType(),
            ["OpenDetails"] = true
        };
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, search ? typeof(SearchConstructor) : typeof(RecommendationConstructor))
            .Add(c => c.Parameters, parameters));

        await component.Find("button").ClickAsync(new());
        component.WaitForAssertion(() =>
        {
            Assert.AreEqual(1, attempts);
            Assert.HasCount(1, component.FindAll("[role=alert]"));
            StringAssert.Contains(component.Find("[role=alert]").TextContent, ((int)status).ToString());
            Assert.IsFalse(component.Find("button").HasAttribute("disabled"));
        });

        await component.Find("button").ClickAsync(new());
        component.WaitForAssertion(() =>
        {
            Assert.AreEqual(2, attempts);
            Assert.HasCount(1, component.FindAll("[role=alert]"));
            Assert.HasCount(0, component.FindAll("input[type=checkbox]"));
            Assert.IsFalse(component.Find("button").HasAttribute("disabled"));
        });
    }
    private sealed class UnusedInProcessRuntime : IJSInProcessRuntime
    {
        public TValue Invoke<TValue>(string identifier, params object?[]? args) => throw new InvalidOperationException("Unexpected synchronous browser access.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Unexpected browser access.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new InvalidOperationException("Unexpected browser access.");
    }
}
