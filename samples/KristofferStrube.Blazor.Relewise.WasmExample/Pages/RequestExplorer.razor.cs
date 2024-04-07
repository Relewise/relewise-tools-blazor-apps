using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Relewise.Client;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Search;
using System.IO.Compression;
using System.Reflection;

namespace KristofferStrube.Blazor.Relewise.WasmExample.Pages
{
    public partial class RequestExplorer
    {
        private bool readOnly = true;
        private bool openDetails = false;
        private string? error;
        private string? message;
        private Type selectedParseType;
        private readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            Converters = [new StringEnumConverter()]
        };
        private List<Type> derivedTypes;
        private LicensedRequest? request;
        private bool hideDefaultValueProperties = true;

        [Inject]
        public required NavigationManager NavigationManager { get; set; }

        protected override void OnInitialized()
        {
            Assembly? assembly = Assembly.GetAssembly(typeof(ClientBase));
            derivedTypes = assembly!
                .GetTypes()
                .Where(type => type != typeof(LicensedRequest) && type.IsAssignableTo(typeof(LicensedRequest)) && !type.IsGenericType && !type.IsAbstract)
                .OrderBy(type => type.Name)
                .ToList();
            selectedParseType = typeof(ProductSearchRequest);
        }

        public void TryParse()
        {
            try
            {
                request = JsonConvert.DeserializeObject<LicensedRequest?>(input, jsonSerializerSettings);
                error = null;
                message = "Found matching type from $type annotation on object.";
            }
            catch (Exception)
            {
                int numberOfTypesEquallyGood = 0;
                Type? bestMatchingSerializationType = null;
                int shortestMessageSizeDifference = int.MaxValue;
                foreach (Type type in derivedTypes)
                {
                    try
                    {
                        object? deserializedAsType = JsonConvert.DeserializeObject(input, type, jsonSerializerSettings);
                        string reserialized = JsonConvert.SerializeObject(deserializedAsType, type, jsonSerializerSettings);
                        int distance = Math.Abs(reserialized.Length - input.Length);
                        if (distance < shortestMessageSizeDifference)
                        {
                            bestMatchingSerializationType = type;
                            shortestMessageSizeDifference = distance;
                            numberOfTypesEquallyGood = 1;
                        }
                        else if (distance == shortestMessageSizeDifference)
                        {
                            numberOfTypesEquallyGood++;
                        }
                    }
                    catch (Exception) { }
                }
                if (bestMatchingSerializationType is null)
                {
                    error = "Could not find a request type that this was able to be deserialized as.";
                    message = null;
                }
                else if (numberOfTypesEquallyGood is not 1)
                {
                    error = $"Was not able to find a single matching type as multiple ones matches equally good. One of them was '{bestMatchingSerializationType.Name}'. Try to get the request with $type annotation on the object or manully find the type.";
                    message = null;
                }
                else
                {
                    request = (LicensedRequest?)JsonConvert.DeserializeObject(input, bestMatchingSerializationType, jsonSerializerSettings);
                    error = null;
                    message = $"Successfully found a best match for the request type: '{bestMatchingSerializationType.Name}'";
                }
            }
        }
        public void ParseAsSelectedType()
        {
            try
            {
                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                };
                jsonSerializerSettings.Converters.Add(new StringEnumConverter());
                request = (LicensedRequest?)JsonConvert.DeserializeObject(input, selectedParseType, jsonSerializerSettings);
                error = null;
                message = "Successfully parsed as the selected type.";
            }
            catch (Exception ex)
            {
                error = ex.Message;
                message = null;
            }
        }

        public void TransferToRecommendationsPage()
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                Converters = [new StringEnumConverter()]
            };

            Type? serializedType = request?.GetType();
            if (serializedType?.BaseType is not null)
            {
                serializedType = serializedType.BaseType;
            }

            string serialized = JsonConvert.SerializeObject(request, serializedType, jsonSerializerSettings);
            string compressed = ToGzip(serialized);

            string url = $"{NavigationManager.BaseUri}Recommendations?q={request?.GetType().Name}&o={compressed}";
            NavigationManager.NavigateTo(url);
        }

        public void TransferToSearchesPage()
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                Converters = [new StringEnumConverter()]
            };

            Type? serializedType = request?.GetType();
            if (serializedType?.BaseType is not null)
            {
                serializedType = serializedType.BaseType;
            }

            string serialized = JsonConvert.SerializeObject(request, serializedType, jsonSerializerSettings);
            string compressed = ToGzip(serialized);

            string url = $"{NavigationManager.BaseUri}Searches?q={request?.GetType().Name}&o={compressed}";
            NavigationManager.NavigateTo(url);
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