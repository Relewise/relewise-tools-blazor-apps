using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Relewise.BlazorApps.Pages;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class VersionsTests
{
    private const string PackageVersions = """
        [{"version":"2.0.0","published":"2026-02-01T00:00:00Z"},
         {"version":"1.0.0","published":"2026-01-01T00:00:00Z"}]
        """;

    [TestMethod]
    public void FailedVersionDownloadCanBeRetried()
    {
        using var context = new BunitContext();
        int requests = 0;
        using var client = DocumentationCacheTests.CreateClient(request =>
        {
            if (++requests == 1)
                throw new HttpRequestException("Fixture connection failure");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PackageVersions) });
        });
        context.Services.AddSingleton(new NugetClient(client));

        var component = context.Render<Versions>();
        component.WaitForAssertion(() => Assert.HasCount(1, component.FindAll("[role=alert]")));
        component.Find("[role=alert] button").Click();

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(0, component.FindAll("[role=alert]"));
            Assert.HasCount(4, component.FindAll(".list > div"));
        });
        Assert.AreEqual(2, requests);
    }

    [TestMethod]
    public void FailedComparisonDownloadCanBeRetriedWithoutReloadingVersionList()
    {
        using var context = new BunitContext();
        int versionRequests = 0;
        int dllRequests = 0;
        using var client = DocumentationCacheTests.CreateClient(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/versions/"))
            {
                versionRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PackageVersions) });
            }
            dllRequests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        context.Services.AddSingleton(new NugetClient(client));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/Versions?from=1.0.0&to=2.0.0");

        var component = context.Render<Versions>();
        component.WaitForAssertion(() => Assert.HasCount(1, component.FindAll("[role=alert]")));
        component.Find("[role=alert] button").Click();

        component.WaitForAssertion(() => Assert.AreEqual(2, dllRequests));
        Assert.AreEqual(1, versionRequests);
        Assert.HasCount(1, component.FindAll("[role=alert]"));
    }

    [TestMethod]
    public void EmptyVersionListOffersRetry()
    {
        using var context = new BunitContext();
        int requests = 0;
        using var client = DocumentationCacheTests.CreateClient(request =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        });
        context.Services.AddSingleton(new NugetClient(client));

        var component = context.Render<Versions>();
        component.WaitForAssertion(() => Assert.HasCount(1, component.FindAll("button")));
        component.Find("button").Click();
        component.WaitForAssertion(() => Assert.AreEqual(2, requests));
        Assert.HasCount(0, component.FindAll("[role=alert]"));
    }
}
