using MudBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Relewise.Client;
using Relewise.Client.DataTypes.Merchandising.Rules;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Search;
using System.IO.Compression;
using System.Reflection;
using Relewise.BlazorApps.Shared;
using System.Runtime.CompilerServices;
using Relewise.Client.Search;

namespace Relewise.BlazorApps.Pages
{
    public partial class RequestExplorer
    {
        private sealed class RequestHistoryIdentity
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
        }

        private static readonly ConditionalWeakTable<object, RequestHistoryIdentity> requestHistoryIdentities = new();
        [Inject] public required ISnackbar Snackbar { get; set; }
        private Type? selectedParseType;
        private bool parseTypeDialogVisible;
        private bool showTroubleshooter;
        private int diagnosticsVersion;
        private bool diagnosticsStale;
        private bool troubleshootAfterParsing;
        private bool executeAfterParsing;
        private bool isRunning;
        private bool sourceExpanded = true;
        private bool sideBySide = true;
        private Searcher? searcher;
        private Recommender? recommender;
        private string? requestConnectionError;
        private SearchConstructor.EditorState searchState = new();
        private RecommendationConstructor.EditorState recommendationState = new();
        private static readonly HashSet<Type> recommendationRequestTypes = typeof(Recommender).GetMethods()
            .Where(method => method.Name == "RecommendAsync" && method.GetParameters().Length == 2)
            .Select(method => method.GetParameters()[0].ParameterType).ToHashSet();
        private string? parseTypeError;
        private JObject? submittedJson;
        private readonly DialogOptions parseTypeDialogOptions = new()
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Small,
            CloseButton = true,
            CloseOnEscapeKey = true
        };
        private readonly static JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            Converters = [new StringEnumConverter()]
        };
        private List<Type> derivedTypes = [];
        private object? inputObject;
        private Action<object?> saveRequestWithoutRendering = null!;
        private List<RequestEditHistory.Entry> requestHistory = [];
        private Type[] merchandisingRules = [typeof(BoostAndBuryRule), typeof(FilterRule), typeof(FixedPositionRule), typeof(InputModifierRule)];

        [Inject]
        public required NavigationManager NavigationManager { get; set; }

        [Inject]
        public required IJSInProcessRuntime JSRuntime { get; set; }


        protected override void OnInitialized()
        {
            saveRequestWithoutRendering = EventUtil.AsNonRenderingEventHandler<object?>(this, SaveEditedRequest);
            try
            {
                Assembly? assembly = Assembly.GetAssembly(typeof(ClientBase));
                derivedTypes = assembly!
                    .GetTypes()
                    .Where(type => type != typeof(LicensedRequest) && type.IsAssignableTo(typeof(LicensedRequest)) && !type.IsGenericType && !type.IsAbstract)
                    .OrderBy(type => type.Name)
                    .ToList();
                derivedTypes.AddRange(merchandisingRules);
                sideBySide = JSRuntime.Invoke<string?>("localStorage.getItem", "requestExplorerLayout") != "top-bottom";
                requestHistory = RequestEditHistory.Load(JSRuntime);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        private static string ResultLayoutName(bool value) => value ? "Side by side" : "Top / bottom";

        private void SetResultLayout(bool value)
        {
            sideBySide = value;
            try
            {
                JSRuntime.InvokeVoid("localStorage.setItem", "requestExplorerLayout", value ? "side-by-side" : "top-bottom");
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        public static void SaveInLocalStorage(IJSInProcessRuntime InProcessJSRuntime, object? objectToSave)
        {
            if (objectToSave is null)
            {
                return;
            }

            string serialized = JsonConvert.SerializeObject(objectToSave, typeof(object), jsonSerializerSettings);
            string compressed = ToGzip(serialized);
            var identity = requestHistoryIdentities.GetValue(objectToSave, _ => new RequestHistoryIdentity());
            identity.Id = RequestEditHistory.Save(InProcessJSRuntime, serialized, compressed, identity.Id);
            InProcessJSRuntime.InvokeVoid("localStorage.setItem", "lastRequest", compressed);
        }

        private void SaveEditedRequest(object? obj)
        {
            bool refresh = sourceExpanded || showTroubleshooter && !diagnosticsStale || !ReferenceEquals(inputObject, obj);
            if (showTroubleshooter) diagnosticsStale = true;
            if (!ReferenceEquals(inputObject, obj))
            {
                executeAfterParsing = false;
                if (obj is not null) PrepareExecution(obj);
                if (obj is SearchRequest searchRequest)
                {
                    searchState.Request = searchRequest;
                    searchState.RequestInitialized = true;
                }
                else if (obj is LicensedRequest recommendationRequest)
                {
                    recommendationState.Request = recommendationRequest;
                    recommendationState.RequestInitialized = true;
                }
            }
            inputObject = obj;
            try
            {
                SaveInLocalStorage(JSRuntime, obj);
                requestHistory = RequestEditHistory.Load(JSRuntime);
                if (obj is not null)
                    input = JsonConvert.SerializeObject(obj, typeof(object), jsonSerializerSettings);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
            if (refresh) StateHasChanged();
        }

        private async Task RestoreHistoryEntry(RequestEditHistory.Entry entry)
        {
            sourceExpanded = false;
            showTroubleshooter = false;
            executeAfterParsing = true;
            searcher = null;
            recommender = null;
            requestConnectionError = null;
            searchState = new();
            recommendationState = new();
            try
            {
                string restored = await Models.FromGzipAsync(entry.Compressed);
                object? restoredObject = JsonConvert.DeserializeObject<object>(restored, jsonSerializerSettings);
                if (restoredObject is not null)
                {
                    requestHistoryIdentities.GetValue(restoredObject, _ => new RequestHistoryIdentity()).Id = entry.Id;
                }
                input = restored;
                inputObject = restoredObject;
                if (restoredObject is not null)
                {
                    PrepareExecution(restoredObject);
                    SaveInLocalStorage(JSRuntime, restoredObject);
                    requestHistory = RequestEditHistory.Load(JSRuntime);
                }
            }
            catch (Exception ex)
            {
                sourceExpanded = true;
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        public async Task RetrieveFromLocalStorage()
        {
            try
            {
                string compressed = JSRuntime.Invoke<string?>("localStorage.getItem", "lastRequest")!;
                input = await Models.FromGzipAsync(compressed);
                inputObject = JsonConvert.DeserializeObject<object>(input, jsonSerializerSettings);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        public bool HasLastRequestBeenSavedPreviously()
        {
            return JSRuntime.Invoke<string?>("localStorage.getItem", "lastRequest") != null;
        }

        public void Submit() => ParseRequest(false);

        private void Troubleshoot() => ParseRequest(true);

        private void TroubleshootAgain()
        {
            if (inputObject is not ProductSearchRequest) return;
            showTroubleshooter = true;
            executeAfterParsing = false;
            diagnosticsStale = false;
            diagnosticsVersion++;
        }

        private void DiagnosticsRunningChanged(bool running) => isRunning = running;

        private void ParseRequest(bool troubleshoot)
        {
            sourceExpanded = false;
            troubleshootAfterParsing = troubleshoot;
            diagnosticsStale = false;
            diagnosticsVersion++;
            executeAfterParsing = !troubleshoot;
            searcher = null;
            recommender = null;
            requestConnectionError = null;
            searchState = new();
            recommendationState = new();
            showTroubleshooter = false;
            inputObject = null;
            parseTypeError = null;
            try
            {
                submittedJson = JObject.Parse(input);
                if (submittedJson.ContainsKey("$type"))
                {
                    try
                    {
                        object? annotated = JsonConvert.DeserializeObject<object>(input, jsonSerializerSettings);
                        if (annotated is not null && annotated.GetType().Assembly == typeof(ClientBase).Assembly)
                        {
                            CompleteParse(annotated);
                            return;
                        }
                    }
                    catch (JsonException)
                    {
                        // Explicit annotations must not silently become a different inferred type.
                    }
                    selectedParseType = null;
                    parseTypeDialogVisible = true;
                    return;
                }

                submittedJson.Remove("$type");
                string untypedJson = submittedJson.ToString(Formatting.None);
                int numberOfTypesEquallyGood = 0;
                object? bestMatch = null;
                int shortestMessageSizeDifference = int.MaxValue;
                if (submittedJson.Count > 0)
                {
                    foreach (Type type in derivedTypes)
                    {
                        try
                        {
                            object? candidate = JsonConvert.DeserializeObject(untypedJson, type, jsonSerializerSettings);
                            if (candidate is null) continue;
                            string reserialized = JsonConvert.SerializeObject(candidate, type, jsonSerializerSettings);
                            int distance = Math.Abs(JObject.Parse(reserialized).ToString(Formatting.None).Length - untypedJson.Length);
                            if (distance < shortestMessageSizeDifference)
                            {
                                bestMatch = candidate;
                                shortestMessageSizeDifference = distance;
                                numberOfTypesEquallyGood = 1;
                            }
                            else if (distance == shortestMessageSizeDifference)
                            {
                                numberOfTypesEquallyGood++;
                            }
                        }
                        catch (JsonException) { }
                    }
                }
                if (bestMatch is not null && numberOfTypesEquallyGood == 1)
                {
                    CompleteParse(bestMatch);
                    return;
                }
                selectedParseType = null;
                parseTypeDialogVisible = true;
            }
            catch (Exception ex)
            {
                sourceExpanded = true;
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        private void CancelTypeSelection()
        {
            parseTypeDialogVisible = false;
            sourceExpanded = true;
        }
        private void ParseAsSelectedType(Type type)
        {
            selectedParseType = type;
            parseTypeError = null;
            try
            {
                var json = (JObject)submittedJson!.DeepClone();
                json.Remove("$type");
                object? parsed = JsonConvert.DeserializeObject(json.ToString(), type, jsonSerializerSettings);
                if (parsed is null) throw new JsonSerializationException("The JSON did not contain a request.");
                CompleteParse(parsed);
            }
            catch (Exception ex)
            {
                parseTypeError = ex.GetBaseException().Message;
            }
        }

        private void CompleteParse(object parsed)
        {
            inputObject = parsed;
            parseTypeDialogVisible = false;
            showTroubleshooter = troubleshootAfterParsing && parsed is ProductSearchRequest;
            if (troubleshootAfterParsing && !showTroubleshooter)
            {
                Snackbar.Add("This request type cannot be troubleshot. Troubleshooting currently supports product search requests only.", MudBlazor.Severity.Warning);
            }
            if (executeAfterParsing || showTroubleshooter)
            {
                PrepareExecution(parsed);
            }
            try
            {
                SaveInLocalStorage(JSRuntime, parsed);
                requestHistory = RequestEditHistory.Load(JSRuntime);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        private void ConnectToDataset()
        {
            executeAfterParsing = false;
            requestConnectionError = null;
            if (inputObject is not null) PrepareExecution(inputObject);
        }

        private void PrepareExecution(object parsed)
        {
            searcher = null;
            recommender = null;
            requestConnectionError = null;
            if (parsed is not SearchRequest && !recommendationRequestTypes.Contains(parsed.GetType()))
            {
                requestConnectionError = "This request type cannot be run here. Running requests currently supports search and recommendation requests.";
                return;
            }
            if (!Guid.TryParse(StaticDatasetStorage.DatasetId, out var datasetId)
                || string.IsNullOrWhiteSpace(StaticDatasetStorage.ApiKey))
            {
                requestConnectionError = "Enter connection details, then click Connect.";
                return;
            }
            try
            {
                if (parsed is SearchRequest)
                    searcher = new Searcher(datasetId, StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl);
                else
                    recommender = new Recommender(datasetId, StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl);
            }
            catch (Exception ex)
            {
                requestConnectionError = ex.GetBaseException().Message;
            }
        }

        private async Task ExecutionChanged(bool running)
        {
            isRunning = running;
            if (running)
            {
                executeAfterParsing = false;
                showTroubleshooter = false;
            }
            if (!running && (searchState.Response is not null || recommendationState.Response is not null))
            {
                await SavedConnectionStorage.SaveAsync(JSRuntime);
            }
        }

        public void TransferToRecommendationsPage()
        {
            try
            {
                Type? serializedType = inputObject?.GetType();
                if (serializedType?.BaseType is not null)
                {
                    serializedType = serializedType.BaseType;
                }

                string serialized = JsonConvert.SerializeObject(inputObject, serializedType, jsonSerializerSettings);
                string compressed = ToGzip(serialized);

                string url = $"{NavigationManager.BaseUri}Recommendations?q={inputObject?.GetType().Name}&o={compressed}";
                NavigationManager.NavigateTo(url);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        public void TransferToSearchesPage()
        {
            try
            {
                Type? serializedType = inputObject?.GetType();
                if (serializedType?.BaseType is not null)
                {
                    serializedType = serializedType.BaseType;
                }

                string serialized = JsonConvert.SerializeObject(inputObject, serializedType, jsonSerializerSettings);
                string compressed = ToGzip(serialized);

                string url = $"{NavigationManager.BaseUri}Searches?q={inputObject?.GetType().Name}&o={compressed}";
                NavigationManager.NavigateTo(url);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        public void TransferToMerchandisingRulesPage()
        {
            try
            {
                Type? serializedType = inputObject?.GetType();
                if (serializedType?.BaseType is not null)
                {
                    serializedType = serializedType.BaseType;
                }

                string serialized = JsonConvert.SerializeObject(inputObject, serializedType, jsonSerializerSettings);
                string compressed = ToGzip(serialized);

                string url = $"{NavigationManager.BaseUri}MerchandisingRules?o={compressed}";
                NavigationManager.NavigateTo(url);
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.GetBaseException().Message, Severity.Error);
            }
        }

        public static string ToGzip(string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            using var input = new MemoryStream(bytes);
            using var output = new MemoryStream();
            using var stream = new GZipStream(output, CompressionLevel.Fastest);

            input.CopyTo(stream);

            stream.Dispose();

            var result = output.ToArray();
            return Convert.ToBase64String(result);
        }

        private void TypeDropdownChanged(ChangeEventArgs eventArgs)
        {
            selectedParseType = derivedTypes.First(t => t.Name == (string?)eventArgs.Value);
        }

        private string input = """
            {
              "$type": "Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client",
              "Term": "Dog House",
              "Skip": 0,
              "Take": 10,
              "DisplayedAtLocation": "Test Location",
              "Filters": {
                "Items": [
                  {
                    "$type": "Relewise.Client.Requests.Filters.OrFilter, Relewise.Client",
                    "Filters": [
                      {
                        "$type": "Relewise.Client.Requests.Filters.BrandIdFilter, Relewise.Client",
                        "BrandIds": [
                          "Nike"
                        ],
                        "TypeName": "BrandIdFilter",
                        "Negated": false
                      },
                      {
                        "$type": "Relewise.Client.Requests.Filters.ProductCategoryIdFilter, Relewise.Client",
                        "CategoryIds": [
                          "Sales-Category"
                        ],
                        "EvaluationScope": "Ancestor",
                        "TypeName": "ProductCategoryIdFilter",
                        "Negated": false
                      }
                    ],
                    "TypeName": "OrFilter",
                    "Negated": false
                  }
                ]
              }
            }
            """;
    }
}
