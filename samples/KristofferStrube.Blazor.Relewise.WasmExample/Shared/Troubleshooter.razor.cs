using KristofferStrube.DocumentSearching;
using KristofferStrube.DocumentSearching.SuffixTree;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Shared;
using Relewise.Client.Responses.Search;
using Relewise.Client.Search;

namespace KristofferStrube.Blazor.Relewise.WasmExample.Shared
{
    public partial class Troubleshooter
    {
        private string? error;
        private string? message;
        private Searcher? searcher;
        private DataAccessor? dataAccessor;
        private SearchAdministrator? searchAdministrator;
        private SearchResult[]? results;
        private List<Improvement> improvements = [];

        [Parameter, EditorRequired]
        public required object Request { get; set; }

        protected override async Task OnInitializedAsync()
        {
            if (Request is not { } ProductSearchRequest)
                return;

            if (StaticDatasetStorage.DatasetId is not "" && !string.IsNullOrWhiteSpace(StaticDatasetStorage.ApiKey))
            {
                await Connect();
            }
            StaticDatasetStorage.AuthenticationReceived += Authenticated;
        }

        private async void Authenticated(object? sender, EventArgs args)
        {
            await Connect();

            StateHasChanged();
        }

        private async Task Connect()
        {
            try
            {
                searcher = new Searcher(new Guid(StaticDatasetStorage.DatasetId), StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl);
                dataAccessor = new DataAccessor(new Guid(StaticDatasetStorage.DatasetId), StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl);
                searchAdministrator = new SearchAdministrator(new Guid(StaticDatasetStorage.DatasetId), StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl);

                message = "Successfully initialized the Searcher, DataAccessor, and SearchAdministator.";
                error = null;
                await ExecuteRequest();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                message = null;
            }
        }

        private async Task ExecuteRequest()
        {
            if (Request is ProductSearchRequest { } searchRequest)
            {
                await ExecuteProductSearchRequest(searchRequest);
            }
        }

        private async Task ExecuteProductSearchRequest(ProductSearchRequest searchRequest)
        {
            try
            {
                List<SearchResult> localResults = new();

                improvements.Clear();

                var jsonSerializerSettings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.None
                };
                jsonSerializerSettings.Converters.Add(new StringEnumConverter());
                var duplicateRequest = JsonConvert.DeserializeObject<ProductSearchRequest>(JsonConvert.SerializeObject(searchRequest, jsonSerializerSettings), jsonSerializerSettings)!;

                duplicateRequest.Take = 200; // We increase the number of response to make analysis on the tail.

                string? language = duplicateRequest.Language?.Value;

                SearchIndex? searchIndex = null;
                if (duplicateRequest.Term is not null)
                {
                    if (string.IsNullOrEmpty(language))
                    {
                        improvements.Add(new(Severity.Error, "A search request that uses a Term should also supply a language."));
                    }
                    else
                    {
                        var searchIndexResponse = await searchAdministrator!.LoadAsync(new SearchIndexesRequest());
                        if (searchIndexResponse.Indexes.FirstOrDefault(i => i.IsDefault) is not { } index)
                        {
                            improvements.Add(new(Severity.Error, "The dataset doesn't have a search index setup."));
                        }
                        else
                        {
                            searchIndex = index;
                            var availableLanguagesInIndex = searchIndex.Configuration.Language.Languages.Select(l => l.Language.Value.ToLower());
                            if (!availableLanguagesInIndex.Contains(language.ToLower()))
                            {
                                improvements.Add(new(Severity.Error, $"The supplied language '{language}' should be one of the ones defined in the search index {string.Join(", ", availableLanguagesInIndex)}"));
                            }
                        }
                    }
                }

                var response = await searcher!.SearchAsync(duplicateRequest);

                SelectedProductDetailsPropertiesSettings selectedProductProperties = new()
                {
                    DisplayName = true,
                    DataKeys = []
                };

                List<(string name, int weight, Func<ProductResultDetails, string?> selector)> indexedKeys = new();

                if (searchIndex is not null)
                {
                    var productIndex = searchIndex.Configuration.Product;
                    if (productIndex.Id?.Included == true)
                    {
                        indexedKeys.Add(("Id", productIndex.Id.Weight, p => p.ProductId));
                    }
                    if (productIndex.DisplayName?.Included == true)
                    {
                        indexedKeys.Add(("Display Name", productIndex.DisplayName.Weight, p => p.DisplayName?.Values?.FirstOrDefault(d => d.Language.Value.Equals(language, StringComparison.OrdinalIgnoreCase))?.Text));
                    }
                    if (productIndex.Brand?.Id?.Included == true)
                    {
                        indexedKeys.Add(("Brand Id", productIndex.Brand.Id.Weight, p => p.Brand?.BrandId));
                        selectedProductProperties.Brand = true;
                    }
                    if (productIndex.Brand?.DisplayName?.Included == true)
                    {
                        indexedKeys.Add(("Brand Display Name", productIndex.Brand.DisplayName.Weight, p => p.Brand?.DisplayName));
                        selectedProductProperties.Brand = true;
                    }
                    if (productIndex.Category?.Unspecified?.Id?.Included == true)
                    {
                        indexedKeys.Add((
                            "Category Ids",
                            productIndex.Category.Unspecified.Id.Weight,
                            p => string.Join(" | ", p.CategoryPaths.Select(path => string.Join(">", path.BreadcrumbPathStartingFromRoot.Select(b => b.Id))))));

                        selectedProductProperties.CategoryPaths = true;
                    }
                    if (productIndex.Category?.Unspecified?.DisplayName?.Included == true)
                    {
                        indexedKeys.Add((
                            "Category Display Names",
                            productIndex.Category.Unspecified.DisplayName.Weight,
                            p => string.Join(
                                " | ",
                                p.CategoryPaths?.Select(path =>
                                    string.Join(
                                        ">",
                                        path.BreadcrumbPathStartingFromRoot?.Select(b =>
                                            b.DisplayName?.Values?.FirstOrDefault(d =>
                                                d.Language?.Value?.Equals(language, StringComparison.OrdinalIgnoreCase) == true)?.Text
                                        ).Where(t => t is not null) ?? []
                                    )
                                ).Where(t => t.Length > 0) ?? []
                            )
                        ));
                        selectedProductProperties.CategoryPaths = true;
                    }

                    foreach (var field in searchIndex.Configuration.Product?.Data?.Keys ?? [])
                    {
                        if (field.Value.Included)
                        {
                            selectedProductProperties.DataKeys = [field.Key, .. selectedProductProperties.DataKeys];
                            indexedKeys.Add((field.Key, field.Value.Weight, p => (p.Data?.TryGetValue(field.Key, out var fieldValue) == true ? DataValueAsString(fieldValue, language) : null)));
                        }
                    }
                }

                if (response.Results.Length is 0)
                {
                    results = [];
                    message = "Search gave no results.";
                    error = null;
                    return;
                }

                var productQueryResponse = await dataAccessor!.QueryAsync(
                    new ProductQuery(
                        response.Results.Select(r => r.ProductId),
                        language: duplicateRequest.Language,
                        currency: duplicateRequest.Currency
                    )
                    {
                        ResultSettings = new()
                        {
                            SelectedProductDetailsProperties = selectedProductProperties,
                            SelectedVariantDetailsProperties = new()
                        }
                    }
                );

                int show = Math.Min(response.Results.Length, 30); // We only show at most 30 results;
                for (int i = 0; i < show; i++)
                {
                    var result = response.Results[i];
                    var productQueryResult = productQueryResponse.Products.First(p => p.ProductId == result.ProductId);
                    List<IndexedValue> indexedValues = new();
                    foreach (var indexedKey in indexedKeys.OrderByDescending(k => k.weight))
                    {
                        var indexedValue = indexedKey.selector?.Invoke(productQueryResult);
                        if (indexedValue is not null)
                        {
                            List<Match>? matches = null;
                            if (duplicateRequest.Term is not null)
                            {
                                var documentIndex = DocumentIndex<string, SuffixTrieSearchIndex>.Create([indexedValue], s => s.ToLower());
                                var searchResults = documentIndex.ApproximateSearch(duplicateRequest.Term.ToLower(), duplicateRequest.Term.Length > 6 ? 2 : 1);
                                if (searchResults.FirstOrDefault() is { } matchCollection)
                                {
                                    var lowestNumberOfEdits = matchCollection.Matches.Min(m => m.Edits);
                                    var goodEnoughMatches = matchCollection.Matches.Where(m => m.Edits <= lowestNumberOfEdits + 1).OrderBy(m => m.Edits).ThenBy(m => m.Position);
                                    if (goodEnoughMatches.Count() > 0)
                                    {
                                        matches = goodEnoughMatches.ToList();
                                    }
                                }
                            }
                            indexedValues.Add(new(indexedKey.name, indexedKey.weight, indexedValue, matches));
                        }
                    }
                    localResults.Add(new()
                    {
                        Position = i + 1,
                        Id = result.ProductId,
                        DisplayName = productQueryResult.DisplayName?.Values?.FirstOrDefault(d => d.Language.Value.Equals(language, StringComparison.OrdinalIgnoreCase))?.Text,
                        IndexedValues = indexedValues
                    });
                }
                results = localResults.ToArray();
                message = "Successfully searched!";
                error = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.GetType().Name + ": " + ex.Message + " -- " + ex.StackTrace);
                error = ex.Message;
                message = null;
            }
            StateHasChanged();
        }

        public string? DataValueAsString(DataValue? value, string? language) => value?.Type switch
        {
            DataValue.DataValueTypes.String => (string?)value?.Value,
            DataValue.DataValueTypes.StringList => value.ValueAsListOf<string>() is { } list ? string.Join(", ", list) : null,
            DataValue.DataValueTypes.Multilingual => ((Multilingual?)value.Value)?.Values?.FirstOrDefault(v => v.Language.Value.Equals(language, StringComparison.OrdinalIgnoreCase))?.Text,
            _ => null
        };

        public class SearchResult
        {
            public required int Position { get; set; }
            public required string Id { get; set; }
            public string? DisplayName { get; set; }
            public required List<IndexedValue> IndexedValues { get; set; }
        }
        
        public record IndexedValue(string Name, int Weight, string Content, List<Match>? TermMatches);

        public record Improvement(Severity Severity, string Message);

        public enum Severity
        {
            Message,
            Warning,
            Error
        }

        public string SeverityColor(Severity severity) => severity switch
        {
            Severity.Error => "red",
            Severity.Warning => "orange",
            _ => "black",
        };
    }
}