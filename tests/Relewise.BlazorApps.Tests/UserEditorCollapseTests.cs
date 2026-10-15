using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class UserEditorCollapseTests
{
    [TestMethod]
    public async Task CreatingUserKeepsDefaultDictionariesCollapsedAndPreservesManualExpansion()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        User? user = null;
        var component = context.Render<ObjectEditor<User>>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.AncestorTypes, new[] { typeof(object) })
            .Add(c => c.Setter, value => user = (User?)value));

        component.Find(".null-object-field").Click();

        Assert.IsNotNull(user);
        Assert.IsTrue(component.Find("details").HasAttribute("open"));
        Assert.HasCount(3, component.FindAll(".dictionary-box"));
        Assert.IsTrue(component.FindAll(".dictionary-box").All(box => box.GetAttribute("data-expanded") == "false"));
        Assert.HasCount(0, component.FindAll("input[data-testid='dictionary-key']"));

        component.FindAll(".dictionary-title")[0].Click();
        component.Render(p => p.Add(c => c.Value, user));
        Assert.AreEqual("true", component.FindAll(".dictionary-box")[0].GetAttribute("data-expanded"));
        Assert.HasCount(1, component.FindAll("input[data-testid='dictionary-key']"));
        Assert.AreEqual(0, user.Classifications.Count);
        Assert.AreEqual(0, user.Identifiers.Count);
        Assert.AreEqual(0, user.Data.Count);
    }

    [TestMethod]
    public async Task DefaultDataDictionaryStartsCollapsedAndCanBeOpened()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<DataValueDictionaryEditor>(p => p
            .Add(c => c.Value, new Dictionary<string, DataValue>())
            .Add(c => c.Setter, _ => { }));

        Assert.AreEqual("false", component.Find(".dictionary-box").GetAttribute("data-expanded"));
        Assert.HasCount(0, component.FindAll("input[data-testid='dictionary-key']"));
        component.Find(".dictionary-title").Click();
        Assert.AreEqual("true", component.Find(".dictionary-box").GetAttribute("data-expanded"));
        Assert.HasCount(1, component.FindAll("input[data-testid='dictionary-key']"));
    }
}
