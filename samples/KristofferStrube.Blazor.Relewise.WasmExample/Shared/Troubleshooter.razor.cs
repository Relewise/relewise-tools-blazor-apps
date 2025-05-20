using KristofferStrube.DocumentSearching;
using KristofferStrube.DocumentSearching.SuffixTree;
using MessagePack;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Merchandising;
using Relewise.Client.DataTypes.Merchandising.Rules;
using Relewise.Client.DataTypes.Search;
using Relewise.Client.DataTypes.Search.Synonyms;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Requests.RelevanceModifiers;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Shared;
using Relewise.Client.Requests.ValueSelectors;
using Relewise.Client.Search;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static KristofferStrube.Blazor.Relewise.WasmExample.Shared.Troubleshooter;

namespace KristofferStrube.Blazor.Relewise.WasmExample.Shared
{
    public partial class Troubleshooter
    {
        private string? error;
        private string? message;
        private Searcher? searcher;
        private DataAccessor? dataAccessor;
        private SearchAdministrator? searchAdministrator;
        private MerchandisingAccessor? merchandisingAccessor;
        private SearchResult[]? results;
        private List<Improvement> improvements = [];
        private Dictionary<BoostAndBuryRule, List<(string productId, string? variantId)>> resultsWithoutMerchandisingRule = new();

        [Parameter, EditorRequired]
        public required object Request { get; set; }

        protected override async Task OnInitializedAsync()
        {
            jsonSerializerSettings.Converters.Add(new StringEnumConverter());

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
                merchandisingAccessor = new MerchandisingAccessor(new Guid(StaticDatasetStorage.DatasetId), StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl);

                message = "Successfully initialized the Searcher, DataAccessor, SearchAdministator, and MerchandisingAccessor.";
                error = null;
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


        private JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        private async Task ExecuteProductSearchRequest(ProductSearchRequest searchRequest)
        {
            try
            {
                List<SearchResult> localResults = new();

                improvements.Clear();

                var duplicateRequest = JsonConvert.DeserializeObject<ProductSearchRequest>(JsonConvert.SerializeObject(searchRequest, jsonSerializerSettings), jsonSerializerSettings)!;

                duplicateRequest.Skip = 0;
                duplicateRequest.Take = 1000; // We increase the number of response to make analysis on the tail.
                if (duplicateRequest.Settings is not null)
                {
                    duplicateRequest.Settings.SelectedProductProperties = new();
                    duplicateRequest.Settings.SelectedVariantProperties = new();
                    duplicateRequest.Settings.SelectedBrandProperties = new();
                }

                string? language = duplicateRequest.Language?.Value;

                Dictionary<string, List<string>> relevantSynonyms = new();

                string[] termParts = (from s in duplicateRequest.Term?.ToLower().Split(new string[] { " ", "_", "-", "|", ".", "," }, StringSplitOptions.RemoveEmptyEntries)
                                      where !(s.Trim() == "")
                                      select s).Distinct().ToArray() ?? [];

                SearchIndex? searchIndex = null;
                if (duplicateRequest.Term is not null)
                {
                    var synonyms = await searchAdministrator!.LoadAsync(new SynonymsRequest() { IsApproved = true, Take = 1000 });

                    var termsIndex = DocumentIndex<string, SuffixTrieSearchIndex>.Create(termParts, s => s.ToLower());

                    foreach (var synonym in synonyms.Values)
                    {
                        if (synonym.Languages?.Length > 0 && !string.IsNullOrEmpty(language) && !synonym.Languages.Any(l => l.Value.ToLower() == language.ToLower()))
                        {
                            continue;
                        }

                        foreach (var from in (synonym.Type is SynonymType.OneWay ? synonym.From : synonym.Words))
                        {
                            foreach(var result in termsIndex.ApproximateSearch(from.ToLower(), 1, 0))
                            {
                                foreach (var to in synonym.Words.Where(w => w.ToLower() != from.ToLower()))
                                {
                                    if (!relevantSynonyms.TryGetValue(to, out List<string>? originals))
                                    {
                                        originals = new();
                                        relevantSynonyms[to] = originals;
                                    }

                                    originals.Add(from);
                                }
                            }
                        }
                    }

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
                                improvements.Add(new(Severity.Error, $"The supplied language '{language}' should be one of the ones defined in the search index: '{string.Join(", ", availableLanguagesInIndex)}'."));
                            }
                        }
                    }
                }

                var response = await searcher!.SearchAsync(duplicateRequest);

                // Prepare for the search without term, but limited to the same products and only with relevance.
                var requestWithoutTerm = JsonConvert.DeserializeObject<ProductSearchRequest>(JsonConvert.SerializeObject(duplicateRequest, jsonSerializerSettings), jsonSerializerSettings)!;

                string? term = requestWithoutTerm.Term;
                requestWithoutTerm.Term = null;
                requestWithoutTerm.Sorting = null;
                requestWithoutTerm.Filters = new(new ProductIdFilter(response.Results.Select(r => r.ProductId)));
                requestWithoutTerm.Facets = null;
                requestWithoutTerm.RelevanceModifiers = null;

                var relevanceResponse = await searcher!.SearchAsync(requestWithoutTerm);

                SelectedProductDetailsPropertiesSettings selectedProductProperties = new()
                {
                    DisplayName = true,
                    DataKeys = []
                };

                SelectedVariantDetailsPropertiesSettings selectedVariantProperties = new()
                {
                    DisplayName = true,
                    DataKeys = [],
                };

                List<(string name, int weight, Func<ProductResultDetails, VariantResultDetails?, string?> selector)> indexedKeys = new();

                if (searchIndex is not null)
                {
                    var productIndex = searchIndex.Configuration.Product;
                    if (productIndex.Id?.Included == true)
                    {
                        indexedKeys.Add(("Id", productIndex.Id.Weight, (p, _) => p.ProductId));
                    }
                    if (productIndex.DisplayName?.Included == true)
                    {
                        indexedKeys.Add(("Display Name", productIndex.DisplayName.Weight, (p, _) => p.DisplayName?.Values?.FirstOrDefault(d => d.Language.Value.Equals(language, StringComparison.OrdinalIgnoreCase))?.Text));
                    }
                    if (productIndex.Brand?.Id?.Included == true)
                    {
                        indexedKeys.Add(("Brand Id", productIndex.Brand.Id.Weight, (p, _) => p.Brand?.BrandId));
                        selectedProductProperties.Brand = true;
                    }
                    if (productIndex.Brand?.DisplayName?.Included == true)
                    {
                        indexedKeys.Add(("Brand Display Name", productIndex.Brand.DisplayName.Weight, (p, _) => p.Brand?.DisplayName));
                        selectedProductProperties.Brand = true;
                    }
                    if (productIndex.Category?.Unspecified?.Id?.Included == true)
                    {
                        indexedKeys.Add((
                            "Category Ids",
                            productIndex.Category.Unspecified.Id.Weight,
                            (p, _) => string.Join(" | ", p.CategoryPaths.Select(path => string.Join(">", path.BreadcrumbPathStartingFromRoot.Select(b => b.Id))))));

                        selectedProductProperties.CategoryPaths = true;
                    }
                    if (productIndex.Category?.Unspecified?.DisplayName?.Included == true)
                    {
                        indexedKeys.Add((
                            "Category Display Names",
                            productIndex.Category.Unspecified.DisplayName.Weight,
                            (p, _) => string.Join(
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
                        if (field.Value?.Included == true)
                        {
                            selectedProductProperties.DataKeys = [field.Key, .. selectedProductProperties.DataKeys];
                            indexedKeys.Add((field.Key, field.Value.Weight, (p, _) => p.Data?.TryGetValue(field.Key, out var fieldValue) == true ? DataValueAsString(fieldValue, language) : null));
                        }
                    }

                    var variantIndex = searchIndex.Configuration.Product?.Variants;
                    if (variantIndex?.Id?.Included == true)
                    {
                        indexedKeys.Add(("Variant: Id", variantIndex.Id.Weight, (_, v) => v?.VariantId));
                    }
                    if (variantIndex?.DisplayName?.Included == true)
                    {
                        indexedKeys.Add(("Variant: Display Name", variantIndex.DisplayName.Weight, (_, v) => v?.DisplayName?.Values?.FirstOrDefault(d => d.Language.Value.Equals(language, StringComparison.OrdinalIgnoreCase))?.Text));
                    }

                    foreach (var field in searchIndex.Configuration.Product?.Variants?.Data?.Keys ?? [])
                    {
                        if (field.Value?.Included == true)
                        {
                            selectedVariantProperties.DataKeys = [field.Key, .. selectedVariantProperties.DataKeys];
                            indexedKeys.Add(("Variant: " + field.Key, field.Value.Weight, (_, v) => v?.Data?.TryGetValue(field.Key, out var fieldValue) == true ? DataValueAsString(fieldValue, language) : null));
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

                var merchandisingRulesForProducts = await MatchingMerchandisingRules(searchRequest, response.Results);

                var allMatchingMerchandisingRules = merchandisingRulesForProducts
                    .SelectMany(m => m.Value.Select(s => s.Rule));

                var allDataDoubleSelectors = allMatchingMerchandisingRules
                    .Where(r => r.MultiplierSelector is DataDoubleSelector)
                    .Select(r => (DataDoubleSelector)r.MultiplierSelector)
                    .ToList();

                foreach (var selector in allDataDoubleSelectors)
                {
                    selectedProductProperties.DataKeys = [selector.Key, .. selectedProductProperties.DataKeys];
                    selectedVariantProperties.DataKeys = [selector.Key, .. selectedVariantProperties.DataKeys];
                }

                selectedProductProperties.DataKeys = selectedProductProperties.DataKeys.Distinct().ToArray();
                selectedVariantProperties.DataKeys = selectedVariantProperties.DataKeys.Distinct().ToArray();

                if (searchRequest.Settings?.ExplodedVariants > 0 || searchRequest.Term is not null)
                {
                    List<ProductAndVariantId> specificVariants = new();
                    foreach (var result in response.Results)
                    {
                        if (result.Variant?.VariantId is { } variantId)
                        {
                            specificVariants.Add(new(result.ProductId, variantId));
                        }
                    }
                    selectedProductProperties.FilteredVariants = new()
                    {
                        Filters = new(new ProductAndVariantIdFilter(specificVariants))
                    };
                }

                var resultDetailsResponse = await dataAccessor!.QueryAsync(
                    new ProductQuery(
                        response.Results.Select(r => r.ProductId),
                        language: duplicateRequest.Language,
                        currency: duplicateRequest.Currency
                        )
                    {
                        ResultSettings = new()
                        {
                            SelectedProductDetailsProperties = selectedProductProperties,
                            SelectedVariantDetailsProperties = selectedVariantProperties,
                        }
                    });

                resultsWithoutMerchandisingRule = new();

                foreach (var rule in allMatchingMerchandisingRules)
                {
                    resultsWithoutMerchandisingRule[rule] = await RequestWithoutMerchandisingRule(rule, merchandisingRulesForProducts.Where(kvp => kvp.Value.Any(s => s.Rule == rule)).Select(kvp => kvp.Key).ToList(), resultDetailsResponse.Products, duplicateRequest);
                }

                var resultsWithoutIdentifers = await RequestWithoutIdentifiers(resultDetailsResponse.Products, duplicateRequest);
                var resultsWithoutClassifications = await RequestWithoutClassifications(resultDetailsResponse.Products, duplicateRequest);
                var resultsWithoutRelevanceModifiers = await RequestWithoutRelevanceModifiers(resultDetailsResponse.Products, duplicateRequest);

                int show = Math.Min(response.Results.Length, 100); // We only show at most 100 results;
                for (int i = 0; i < show; i++)
                {
                    var result = response.Results[i];
                    var productQueryResult = resultDetailsResponse.Products.First(p => p.ProductId == result.ProductId);
                    var variantQueryResult = productQueryResult.FilteredVariants?.FirstOrDefault(v => v.VariantId == result.Variant?.VariantId);
                    List<IndexedValue> indexedValues = new();
                    foreach (var indexedKey in indexedKeys.OrderByDescending(k => k.weight))
                    {
                        var indexedValue = indexedKey.selector?.Invoke(productQueryResult, variantQueryResult);
                        if (indexedValue is not null)
                        {
                            List<(Match match, MatchOrigin matchType, string term)> matches = new();
                            if (term is not null)
                            {
                                var documentIndex = DocumentIndex<string, SuffixTrieSearchIndex>.Create([indexedValue], s => s.ToLower());
                                foreach(var termPart in termParts)
                                {
                                    var searchResults = documentIndex.ApproximateSearch(termPart.ToLower(), termPart.Length > 6 ? 2 : 1);
                                    if (searchResults.FirstOrDefault() is { } matchCollection)
                                    {
                                        var lowestNumberOfEdits = matchCollection.Matches.Min(m => m.Edits);
                                        var goodEnoughMatches = matchCollection.Matches.Where(m => m.Edits <= lowestNumberOfEdits + 1 && m.ExpandedCigar.First() is not EditType.Insert && m.ExpandedCigar.Last() is not EditType.Insert).OrderBy(m => m.Edits);
                                        if (goodEnoughMatches.Count() > 0)
                                        {
                                            matches.AddRange(goodEnoughMatches.Select(m => (m, MatchOrigin.Term, termPart)));
                                        }
                                    }
                                }
                                foreach((string to, List<string> from) in relevantSynonyms)
                                {
                                    var synonymSearchResult = documentIndex.ApproximateSearch(to.ToLower(), term.Length > 6 ? 2 : 1);
                                    if (synonymSearchResult.FirstOrDefault() is { } synonymMatchCollection)
                                    {
                                        var lowestNumberOfEdits = synonymMatchCollection.Matches.Min(m => m.Edits);
                                        var goodEnoughMatches = synonymMatchCollection.Matches.Where(m => m.Edits <= lowestNumberOfEdits + 1 && m.ExpandedCigar.First() is not EditType.Insert && m.ExpandedCigar.Last() is not EditType.Insert).OrderBy(m => m.Edits);
                                        if (goodEnoughMatches.Count() > 0)
                                        {
                                            matches.AddRange(goodEnoughMatches.Select(m => (m, MatchOrigin.Synonym, $"{string.Join(" or ", from.Select(w => $"\"{w}\""))} to \"{to}\"")));
                                        }
                                    }
                                }
                            }
                            indexedValues.Add(new(indexedKey.name, indexedKey.weight, indexedValue, matches));
                        }
                    }
                    localResults.Add(new()
                    {
                        Position = i + 1,
                        PopularityIndex = relevanceResponse.Results.ToList().IndexOf(relevanceResponse.Results.First(r => r.ProductId == result.ProductId)) + 1,
                        WithoutPersonalizationIndex = resultsWithoutIdentifers.FirstOrDefault(r => r.productId == result.ProductId && r.variantId == result.Variant?.VariantId) is { } matchingElementWithoutIdentifiers ? resultsWithoutIdentifers.IndexOf(matchingElementWithoutIdentifiers) : 1000,
                        WithoutClassificationsIndex = resultsWithoutClassifications.FirstOrDefault(r => r.productId == result.ProductId && r.variantId == result.Variant?.VariantId) is { } matchingElementWithoutClassifications ? resultsWithoutClassifications.IndexOf(matchingElementWithoutClassifications) : 1000,
                        WithoutRelevanceModifiersIndex = resultsWithoutRelevanceModifiers.FirstOrDefault(r => r.productId == result.ProductId && r.variantId == result.Variant?.VariantId) is { } matchingElementWithoutRelevanceModifiers ? resultsWithoutRelevanceModifiers.IndexOf(matchingElementWithoutRelevanceModifiers) : 1000,
                        ProductId = result.ProductId,
                        VariantId = result.Variant?.VariantId,
                        DisplayName = productQueryResult.DisplayName?.Values?.FirstOrDefault(d => d.Language.Value.Equals(language, StringComparison.OrdinalIgnoreCase))?.Text,
                        IndexedValues = indexedValues,
                        MerchandisingRules = merchandisingRulesForProducts.TryGetValue((result.ProductId, result.Variant?.VariantId), out var set) ? set.ToList() : [],
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

        private static readonly Dictionary<Type, byte> _typeToUnion = typeof(SearchRequest).GetCustomAttributes(typeof(UnionAttribute), false).Cast<UnionAttribute>().ToDictionary(k => k.SubType, v => (byte)v.Key);

        public async Task<Dictionary<(string productId, string? variantId), HashSet<MerchandisingRuleSummary>>> MatchingMerchandisingRules(ProductSearchRequest request, ProductResult[] productResults)
        {
            Dictionary<(string productId, string? variantId), HashSet<MerchandisingRuleSummary>> results = new();

            var merchandisingRulesRepsonse = await merchandisingAccessor!.LoadAsync<BoostAndBuryRule>();

            foreach (var rule in merchandisingRulesRepsonse.Rules)
            {
                if (!rule.Enabled)
                    continue;

                var contextFilters = rule.Conditions.Context.Filters;

                bool anyContextMatches = false;

                foreach (var filter in contextFilters)
                {
                    if (RequestIsValidSearchRequest(filter, request))
                    {
                        anyContextMatches = true;
                        break;
                    }
                }

                if (!anyContextMatches)
                    continue;

                if (rule.Conditions.User?.Conditions?.Items?.Count > 0)
                {
                    improvements.Add(new Improvement(Severity.Message, $"We found matches for the Boost and Burry Rule '{rule.Name}' but it had user conditions which we currently don't validate so there might be false positive matches."));
                }

                List<string> productsToFilter = productResults
                    .Select(r => r.ProductId)
                    .Distinct()
                    .ToList();

                var queryForProductsFittingFilters = new ProductQuery(productsToFilter, request.Language, request.Currency);

                queryForProductsFittingFilters.Filters!.Items!.AddRange(rule.Conditions.Target.Filters.Items ?? []);

                List<ProductAndVariantId> variantsToFilter = productResults
                    .Where(r => r.Variant?.VariantId is not null)
                    .Select(r => new ProductAndVariantId(r.ProductId, r.Variant.VariantId))
                    .ToList();

                queryForProductsFittingFilters.ResultSettings = new()
                {
                    SelectedProductDetailsProperties = new()
                    {
                        FilteredVariants = new()
                        {
                            Filters = new(new ProductAndVariantIdFilter(variantsToFilter))
                        }
                    },
                    SelectedVariantDetailsProperties = new(),
                };

                var productsFittingMerchandisingFilters = await dataAccessor!.QueryAsync(queryForProductsFittingFilters);

                foreach (var result in productsFittingMerchandisingFilters.Products)
                {
                    if (result.FilteredVariants?.Length > 0)
                    {
                        foreach (var variant in result.FilteredVariants)
                        {
                            if (!results.TryGetValue((result.ProductId, variant.VariantId), out var merchandisingRules))
                            {
                                merchandisingRules = new();
                                results[(result.ProductId, variant.VariantId)] = merchandisingRules;
                            }
                            merchandisingRules.Add(new(rule.Name, rule.Description, rule));
                        }
                    }
                    else
                    {
                        if (!results.TryGetValue((result.ProductId, null), out var merchandisingRules))
                        {
                            merchandisingRules = new();
                            results[(result.ProductId, null)] = merchandisingRules;
                        }
                        merchandisingRules.Add(new(rule.Name, rule.Description, rule));
                    }
                }
            }

            return results;
        }

        private async Task<List<(string productId, string? variantId)>> RequestWithoutMerchandisingRule(BoostAndBuryRule rule, List<(string productId, string? variantId)> resultsThatMatchRule, ProductResultDetails[] products, ProductSearchRequest searchRequest)
        {
            var duplicateRequest = JsonConvert.DeserializeObject<ProductSearchRequest>(JsonConvert.SerializeObject(searchRequest, jsonSerializerSettings), jsonSerializerSettings)!;

            duplicateRequest.RelevanceModifiers ??= new() { Items = new() };

            if (rule.MultiplierSelector is FixedDoubleValueSelector fixedValue)
            {
                duplicateRequest.RelevanceModifiers.Add(new ProductIdRelevanceModifier(resultsThatMatchRule.Select(r => r.productId).Distinct(), 1 / fixedValue.Value));
                //duplicateRequest.RelevanceModifiers.Add(new VariantIdRelevanceModifier(resultsThatMatchRule.Where(r => r.variantId is not null).Select(r => r.variantId).Distinct(), 1 / fixedValue.Value));
            }
            if (rule.MultiplierSelector is DataDoubleSelector dataValueSelector)
            {
                // TODO
            }

            var response = await searcher!.SearchAsync(duplicateRequest);

            return response.Results
                .Select(r => (r.ProductId, r.Variant?.VariantId))
                .ToList();
        }

        private async Task<List<(string productId, string? variantId)>> RequestWithoutIdentifiers(ProductResultDetails[] products, ProductSearchRequest searchRequest)
        {
            var duplicateRequest = JsonConvert.DeserializeObject<ProductSearchRequest>(JsonConvert.SerializeObject(searchRequest, jsonSerializerSettings), jsonSerializerSettings)!;

            if (duplicateRequest.User is not null)
            {
                duplicateRequest.User.AuthenticatedId = null;
                duplicateRequest.User.TemporaryId = null;
                duplicateRequest.User.Email = null;
                duplicateRequest.User.Fingerprint = null;
                duplicateRequest.User.Identifiers = null;
            }

            var response = await searcher!.SearchAsync(duplicateRequest);

            return response.Results
                .Select(r => (r.ProductId, r.Variant?.VariantId))
                .ToList();
        }

        private async Task<List<(string productId, string? variantId)>> RequestWithoutClassifications(ProductResultDetails[] products, ProductSearchRequest searchRequest)
        {
            var duplicateRequest = JsonConvert.DeserializeObject<ProductSearchRequest>(JsonConvert.SerializeObject(searchRequest, jsonSerializerSettings), jsonSerializerSettings)!;

            duplicateRequest.User = null;

            if (duplicateRequest.User is not null)
            {
                duplicateRequest.User.Classifications = new();
            }

            var response = await searcher!.SearchAsync(duplicateRequest);

            return response.Results
                .Select(r => (r.ProductId, r.Variant?.VariantId))
                .ToList();
        }

        private async Task<List<(string productId, string? variantId)>> RequestWithoutRelevanceModifiers(ProductResultDetails[] products, ProductSearchRequest searchRequest)
        {
            var duplicateRequest = JsonConvert.DeserializeObject<ProductSearchRequest>(JsonConvert.SerializeObject(searchRequest, jsonSerializerSettings), jsonSerializerSettings)!;

            duplicateRequest.RelevanceModifiers = null;

            var response = await searcher!.SearchAsync(duplicateRequest);

            return response.Results
                .Select(r => (r.ProductId, r.Variant?.VariantId))
                .ToList();
        }

        private bool RequestIsValidSearchRequest(RequestContextFilter filter, ProductSearchRequest request)
        {
            if (
                (filter.Searches?.UnionCodes.Count != 0
                    || filter.Recommendations?.UnionCodes.Count != 0) // There are either defined specific recommendation requests or search requests.
                &&
                (filter.Searches?.UnionCodes.Count is null or 0 // There are not defined specific search, so it must be specific recommendation requests.
                    || filter.Searches?.UnionCodes.Contains(_typeToUnion[typeof(ProductSearchRequest)]) != true) // product search request is not part of the search requests defined.
                )
                return false;

            if (filter.Locations?.Count != 0 && filter.Locations?.Contains(request.DisplayedAtLocation) != true)
                return false;

            if (filter.Languages?.Count != 0 && filter.Languages?.Any(l => l.Value.Equals(request.Language?.Value, StringComparison.OrdinalIgnoreCase)) != true)
                return false;

            if (filter.Currencies?.Count != 0 && filter.Currencies?.Any(c => c.Value.Equals(request.Currency?.Value, StringComparison.OrdinalIgnoreCase)) != true)
                return false;

            if (filter.Filters.Includes?.Items?.Count > 0 || filter.Filters.Excludes?.Items?.Count > 0)
                return false;

            return true;
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
            public required int PopularityIndex { get; set; }
            public required int WithoutPersonalizationIndex { get; set; }
            public required int WithoutClassificationsIndex { get; set; }
            public required int WithoutRelevanceModifiersIndex { get; set; }
            public required string ProductId { get; set; }
            public required string? VariantId { get; set; }
            public string? DisplayName { get; set; }
            public required List<IndexedValue> IndexedValues { get; set; }
            public required List<MerchandisingRuleSummary> MerchandisingRules { get; set; }
        }

        public record IndexedValue(string Name, int Weight, string Content, List<(Match match, MatchOrigin matchType, string term)>? TermMatches);

        public record Improvement(Severity Severity, string Message);

        public record MerchandisingRuleSummary(string Name, string Description, BoostAndBuryRule Rule);

        public enum Severity
        {
            Message,
            Warning,
            Error
        }

        public enum MatchOrigin
        {
            Term,
            Synonym
        }

        public string SeverityColor(Severity severity) => severity switch
        {
            Severity.Error => "red",
            Severity.Warning => "orange",
            _ => "black",
        };

        private interface IFilter
        {
        }
    }
}
