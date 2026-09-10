using System.Net;
using Relewise.BlazorApps.XmlSummaries;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class DocumentationCacheTests
{
    internal const string Xml = "<doc><assembly><name>Relewise.Client</name></assembly><members><member name=\"T:Relewise.Client.TestModel\"><summary>Fixture documentation</summary></member></members></doc>";

    [TestMethod]
    public async Task ConcurrentCallersShareOneSuccessfulLoad()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0;
        using var client = CreateClient(async request =>
        {
            Interlocked.Increment(ref requests);
            if (request.RequestUri!.AbsolutePath.Contains("xmldocs"))
            {
                entered.SetResult();
                await release.Task;
            }
            return Response(request);
        });
        var cache = new DocumentationCache(client);
        var first = cache.GetAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = cache.GetAsync();
        release.SetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(2, requests);
        Assert.IsNotNull(results[0].xml);
        Assert.AreSame(results[0].xml, results[1].xml);
        Assert.AreSame(results[0].community, results[1].community);
    }

    [TestMethod]
    [DataRow("xmldocs")]
    [DataRow("communitydocs")]
    public async Task FailedDownloadCanBeRetried(string failingResource)
    {
        bool fail = true;
        using var client = CreateClient(request =>
        {
            if (fail && request.RequestUri!.AbsolutePath.Contains(failingResource))
            {
                fail = false;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(Response(request));
        });
        var cache = new DocumentationCache(client);
        var failed = await cache.GetAsync();
        Assert.IsNull(failed.xml);
        var retried = await cache.GetAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsNotNull(retried.xml);
        Assert.IsNotNull(retried.community);
        Assert.AreEqual("Fixture documentation", retried.xml.GetSummary("TestModel"));
    }

    internal static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) =>
        new(new FixtureHandler(send)) { BaseAddress = new Uri("https://fixture.invalid/") };

    internal static HttpResponseMessage Response(HttpRequestMessage request) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(request.RequestUri!.AbsolutePath.Contains("xmldocs") ? Xml : "[]")
    };

    private sealed class FixtureHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
