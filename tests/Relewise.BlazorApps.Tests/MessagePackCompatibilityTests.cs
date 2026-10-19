using System.Net;
using MessagePack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses.Search;
using Relewise.Client.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class MessagePackCompatibilityTests
{
    // Synthetic SDK 1.308.0 payloads produced with MessagePack 2.5.302 before upgrading.
    // Fixed bytes check interoperability, rather than only a same-version round trip.
    private const string LegacyProduct = "mq9maXh0dXJlLXByb2R1Y3TAkMCQg6VwcmljZQHLQEVAAAAAAACkbmFtZQCnZml4dHVyZaVzaXplcweSyz/wAAAAAAAAy0AEAAAAAAAAwMDAwA==";
    private const string LegacyRequest = "3AAawIGnZml4dHVyZalwcmVzZXJ2ZWTAkaJlbsDAwMDAwMDAwMDAAAXAwMCnZml4dHVyZcDAwMDA";
    private const string LegacyResponse = "nJLLP+AAAAAAAADLAAAAAAAAAADAwMABwMCR3AAXr2ZpeHR1cmUtcHJvZHVjdMDAwADAg6VwcmljZQHLQEVAAAAAAACkbmFtZQCnZml4dHVyZaVzaXplcweSyz/wAAAAAAAAy0AEAAAAAAAAwMDAwMDAwMDAwMDAkMDAwMDAwMA=";

    [TestMethod]
    public void ProductPayloadRemainsCompatibleWithPreviousMessagePackVersion()
    {
        _ = CreateSearcher();
        var restored = MessagePackSerializer.Deserialize<Product>(Convert.FromBase64String(LegacyProduct));
        Assert.AreEqual("fixture-product", restored.Id);
        Assert.AreEqual(42.5, restored.Data["price"].Value);
        Assert.AreEqual("fixture", restored.Data["name"].Value);
        CollectionAssert.AreEqual(new[] { 1.0, 2.5 }, ((IEnumerable<double>)restored.Data["sizes"].Value).ToArray());
        Assert.AreEqual(LegacyProduct, Convert.ToBase64String(MessagePackSerializer.Serialize(restored)));
    }

    [TestMethod]
    public void RequestPayloadRemainsCompatibleWithPreviousMessagePackVersion()
    {
        _ = CreateSearcher();
        var restored = MessagePackSerializer.Deserialize<ProductSearchRequest>(Convert.FromBase64String(LegacyRequest));
        Assert.AreEqual("fixture", restored.Term);
        Assert.AreEqual(5, restored.Take);
        Assert.AreEqual("en", restored.Language.Value);
        Assert.AreEqual("preserved", restored.Custom["fixture"]);
        Assert.AreEqual(LegacyRequest, Convert.ToBase64String(MessagePackSerializer.Serialize(restored)));
    }

    [TestMethod]
    public async Task SdkReadsPreviousVersionResponseOverBinaryTransport()
    {
        var searcher = CreateSearcher();
        ProductSearchRequest? posted = null;
        using var http = DocumentationCacheTests.CreateClient(async message =>
        {
            posted = MessagePackSerializer.Deserialize<ProductSearchRequest>(await message.Content!.ReadAsByteArrayAsync());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(LegacyResponse))
            };
        });
        searcher.OverriddenHttpClient = http;
        var request = MessagePackSerializer.Deserialize<ProductSearchRequest>(Convert.FromBase64String(LegacyRequest));
        var response = await searcher.SearchAsync(request);
        Assert.IsNotNull(posted);
        Assert.AreEqual("fixture", posted.Term);
        Assert.AreEqual("preserved", posted.Custom["fixture"]);
        Assert.AreEqual(1, response.Hits);
        Assert.AreEqual("fixture-product", response.Results.Single().ProductId);
        Assert.AreEqual(42.5, response.Results.Single().Data["price"].Value);
        // NetworkTimeInMs is changed by the SDK; verify unchanged wire encoding separately.
        var original = MessagePackSerializer.Deserialize<ProductSearchResponse>(Convert.FromBase64String(LegacyResponse));
        Assert.AreEqual(LegacyResponse, Convert.ToBase64String(MessagePackSerializer.Serialize(original)));
    }

    private static Searcher CreateSearcher() => new(Guid.Parse("00000000-0000-0000-0000-000000000001"),
        "synthetic-api-key", "https://fixture.invalid");
}
