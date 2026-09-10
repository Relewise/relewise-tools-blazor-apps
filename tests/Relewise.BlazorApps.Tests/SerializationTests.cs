using Microsoft.VisualStudio.TestTools.UnitTesting;
using Relewise.Client.DataTypes;
using MessagePack;
using Newtonsoft.Json;
using Relewise.Client;
using Relewise.Client.Search;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses.Search;
using System.Net;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class SerializationTests
{
    [TestMethod]
    public async Task ExistingSdkCanDeserializeASearchResponse()
    {
        var response = JsonConvert.DeserializeObject<ProductSearchResponse>("{\"Results\":[],\"Hits\":0,\"Statistics\":{\"ServerTimeInMs\":0}}")!;
        using var http = DocumentationCacheTests.CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(MessagePackSerializer.Serialize(response))
        }));
        var searcher = new Searcher(Guid.Parse("00000000-0000-0000-0000-000000000001"), "fixture", "https://fixture.invalid") { OverriddenHttpClient = http };
        var request = JsonConvert.DeserializeObject<ProductSearchRequest>("{\"Term\":\"fixture\",\"Skip\":0,\"Take\":5}")!;
        var result = await searcher.SearchAsync(request);
        Assert.AreEqual(0, result.Hits);
    }

    [TestMethod]
    public void MessagePackRoundTripUsesTheExistingSdkFormatters()
    {
        var product = new Product("fixture-product")
        {
            Data = new Dictionary<string, DataValue?>
            {
                ["price"] = new(42.5),
                ["sizes"] = new(new List<double> { 1, 2.5 }),
                ["name"] = new("fixture")
            }
        };
        var bytes = MessagePackSerializer.Serialize(product);
        var restored = MessagePackSerializer.Deserialize<Product>(bytes);
        Assert.AreEqual(product.Id, restored.Id);
        Assert.IsNotNull(restored.Data);
        Assert.AreEqual(42.5, restored.Data["price"]?.Value);
        Assert.AreEqual("fixture", restored.Data["name"]?.Value);
        var sizes = restored.Data["sizes"]?.Value as IEnumerable<double>;
        Assert.IsNotNull(sizes);
        CollectionAssert.AreEqual(new[] { 1.0, 2.5 }, sizes.ToArray());
    }
}
