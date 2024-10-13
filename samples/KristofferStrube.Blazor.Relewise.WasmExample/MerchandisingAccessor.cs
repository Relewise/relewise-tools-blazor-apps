using Relewise.Client.DataTypes.Merchandising.Rules;
using Relewise.Client.Requests.Merchandising;
using Relewise.Client.Responses.Merchandising;
using Relewise.Client;
using System.Data;

namespace KristofferStrube.Blazor.Relewise.WasmExample;

public class MerchandisingAccessor(Guid datasetId, string apiKeySecret, string serverUrl) : ClientBase(datasetId, apiKeySecret, serverUrl, TimeSpan.FromSeconds(30))
{
    public async Task<MerchandisingRuleCollectionResponse<T>> LoadAsync<T>(CancellationToken token = default) where T : MerchandisingRule
    {
        var response = await PostAsync<MerchandisingRulesRequest, MerchandisingRuleCollectionResponse>(new MerchandisingRulesRequest(MerchandisingRule.GetTypeId<T>()), token).ConfigureAwait(false);
        return new MerchandisingRuleCollectionResponse<T>(response);
    }

    public async Task<MerchandisingRuleCollectionResponse> LoadAsync(CancellationToken token = default)
    {
        var response = await PostAsync<MerchandisingRulesRequest, MerchandisingRuleCollectionResponse>(new MerchandisingRulesRequest(), token).ConfigureAwait(false);
        return response;
    }
}
