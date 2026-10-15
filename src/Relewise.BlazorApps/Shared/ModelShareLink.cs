using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Recommendations.Feed;
using Relewise.Client.Requests.Search;

namespace Relewise.BlazorApps.Shared;

public static class ModelShareLink
{
    public static string Page(Type type)
        => type.IsAssignableTo(typeof(SearchRequest)) ? "Searches"
            : type.IsAssignableTo(typeof(RecommendationRequest))
                || type == typeof(ProductRecommendationRequestCollection)
                || type == typeof(ContentRecommendationRequestCollection)
                || type == typeof(FeedRecommendationNextItemsRequest) ? "Recommendations"
            : "Models";

    public static string Create(string baseUri, Type type, string compressed)
        => $"{baseUri}{Page(type)}?q={Uri.EscapeDataString(type.Name)}&o={Uri.EscapeDataString(compressed)}";
}
