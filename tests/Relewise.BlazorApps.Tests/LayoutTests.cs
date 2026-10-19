using Bunit;
using KristofferStrube.Blazor.Window;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;
using Relewise.BlazorApps.Layout;

namespace Relewise.BlazorApps.Tests;

[TestClass]
[DoNotParallelize]
public class LayoutTests
{
    [TestMethod]
    public async Task StandaloneLayoutKeepsAllToolRoutesAndNavigationOpen()
    {
        await using var context = CreateContext();
        string? previousOrigin = StaticDatasetStorage.ParentOrigin;
        try
        {
            StaticDatasetStorage.ParentOrigin = null;
            var layout = context.Render<TestLayout>(parameters => parameters.Add(p => p.Body, "<p>Fixture page</p>"));

            string[] routes = ["", "Products/", "Content/", "Triggers/", "MerchandisingRules/", "Recommendations/", "Searches/", "Models/", "RequestExplorer/", "Versions/"];
            CollectionAssert.AreEqual(routes, layout.FindComponents<MudNavLink>().Select(link => link.Instance.Href).ToArray());
            Assert.IsTrue(layout.FindComponent<MudDrawer>().Instance.GetState(x => x.Open));
            Assert.AreEqual(DrawerVariant.Persistent, layout.FindComponent<MudDrawer>().Instance.Variant);
            Assert.HasCount(0, layout.FindAll("button[aria-label='Toggle navigation']"));
            Assert.AreEqual("Fixture page", layout.Find("article p").TextContent);
        }
        finally
        {
            StaticDatasetStorage.ParentOrigin = previousOrigin;
        }
    }

    [TestMethod]
    public async Task EmbeddedLayoutHidesNavigationButKeepsContentAndMudProviders()
    {
        await using var context = CreateContext();
        string? previousOrigin = StaticDatasetStorage.ParentOrigin;
        try
        {
            StaticDatasetStorage.ParentOrigin = "https://fixture.invalid";
            var layout = context.Render<TestLayout>(parameters => parameters.Add(p => p.Body, "<p>Fixture page</p>"));

            Assert.HasCount(0, layout.FindComponents<MudDrawer>());
            Assert.HasCount(0, layout.FindAll("button[aria-label='Toggle navigation']"));
            Assert.HasCount(1, layout.FindComponents<MudThemeProvider>());
            Assert.HasCount(1, layout.FindComponents<MudPopoverProvider>());
            Assert.HasCount(1, layout.FindComponents<MudDialogProvider>());
            Assert.HasCount(1, layout.FindComponents<MudSnackbarProvider>());
            Assert.AreEqual("Fixture page", layout.Find("article p").TextContent);
        }
        finally
        {
            StaticDatasetStorage.ParentOrigin = previousOrigin;
        }
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddWindowService();
        return context;
    }

    // These tests cover shell rendering; the browser check exercises window interop.
    public class TestLayout : MainLayout
    {
        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }
}
