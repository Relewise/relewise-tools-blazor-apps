using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Relewise.Client.DataTypes;

namespace KristofferStrube.Blazor.Relewise.WasmExample.Pages;

public partial class DataValueConverter
{
    private readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented,
        Converters = [new StringEnumConverter()]
    };
    private readonly JsonSerializerSettings simpleJsonSerializerSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented,
        Converters = [new StringEnumConverter()]
    };
    private string? error;
    private string? message;
    private string from = """
        {
          "description": {
            "da": "En Beskrivelse",
            "en": "A Description"
          },
          "Tags": [
            "Nice",
            "Cool",
            "Summer"
          ],
          "Color": "Blue",
          "InStock": true,
          "StockCount": 42
        }
        """;
    private string to = "";
    private string fromType = "Unstructured JSON";
    private string toType = "DataValue Bag";
    private readonly string[] types = ["Unstructured JSON", "DataValue Bag", "Webhook Data"];

    public void TryConvert()
    {
        Convert(false);
    }
    public void ForceConvert()
    {
        Convert(true);
    }

    public void Swap()
    {
        (from, to) = (to, from);
        (fromType, toType) = (toType, fromType);
    }

    public void Convert(bool suppressWarnings)
    {
        switch ((fromType, toType))
        {
            case ("Unstructured JSON", "DataValue Bag"):
                ConvertFromJSONToDataValue(suppressWarnings);
                break;
            case ("Unstructured JSON", "Webhook Data"):
                ConvertFromJSONToWebhookData(suppressWarnings);
                break;
            case ("DataValue Bag", "Unstructured JSON"):
                ConvertFromDataValueToJSON(suppressWarnings);
                break;
            case ("DataValue Bag", "Webhook Data"):
                string localTo = to;
                ConvertFromDataValueToJSON(suppressWarnings);
                string localFrom = from;
                from = to;
                to = localTo;
                ConvertFromJSONToWebhookData(suppressWarnings);
                from = localFrom;
                break;
            case ("Webhook Data", "Unstructured JSON"):
                ConvertFromWebhookDataToJSON(suppressWarnings);
                break;
            case ("Webhook Data", "DataValue Bag"):
                localTo = to;
                ConvertFromWebhookDataToJSON(suppressWarnings);
                localFrom = from;
                from = to;
                to = localTo;
                ConvertFromJSONToDataValue(suppressWarnings);
                from = localFrom;
                break;
        }
    }

    public void ConvertFromJSONToDataValue(bool suppressWarnings)
    {
        try
        {
            JObject jObject = default!;
            try
            {
                jObject = JObject.Parse(from);
            }
            catch (Exception)
            {
                error = "Input was not valid JSON.";
                message = null;
                return;
            }
            from = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<object>(from), jsonSerializerSettings);
            Dictionary<string, DataValue?> dataBag = [];
            error = "";
            foreach (JProperty property in jObject.Properties())
            {
                dataBag.Add(property.Name, Parse(property.Value, suppressWarnings));
            }
            if (error is "")
            {
                error = null;
                to = JsonConvert.SerializeObject(dataBag, jsonSerializerSettings);
                message = "Successfully parsed.";
            }
            else
            {
                error = error[..^2];
                message = null;
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            message = null;
            throw;
        }
    }

    public void ConvertFromJSONToWebhookData(bool suppressWarnings)
    {
        try
        {
            JObject jObject = default!;
            try
            {
                jObject = JObject.Parse(from);
            }
            catch (Exception)
            {
                error = "Input was not valid JSON.";
                message = null;
                return;
            }
            from = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<object>(from), jsonSerializerSettings);
            Dictionary<string, Dictionary<string, object>> dataBag = [];
            error = "";
            foreach (JProperty property in jObject.Properties())
            {
                string? bucket = WebhookDataBucket(property.Value, suppressWarnings);
                if (bucket is null)
                {
                    continue;
                }

                if (dataBag.TryGetValue(bucket, out Dictionary<string, object>? members))
                {
                    members.Add(property.Name, property.Value);
                }
                else
                {
                    dataBag[bucket] = new() { [property.Name] = property.Value };
                }
            }
            if (error is "")
            {
                error = null;
                to = JsonConvert.SerializeObject(dataBag, jsonSerializerSettings);
                message = "Successfully parsed.";
            }
            else
            {
                error = error[..^2];
                message = null;
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            message = null;
            throw;
        }
    }

    public void ConvertFromWebhookDataToJSON(bool suppressWarnings)
    {
        try
        {
            JObject jObject = default!;
            try
            {
                jObject = JObject.Parse(from);
            }
            catch (Exception)
            {
                error = "Input was not valid JSON.";
                message = null;
                return;
            }
            from = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<object>(from), jsonSerializerSettings);
            Dictionary<string, object> dataBag = [];
            error = "";
            HashSet<string> bucketNames = ["strings", "numbers", "booleans", "multilingual", "multilingualCollection", "stringLists", "numberLists", "booleanLists"];
            foreach (JProperty property in jObject.Properties())
            {
                if (!bucketNames.Contains(property.Name) || property.Value.Type is not JTokenType.Object)
                {
                    if (!suppressWarnings)
                    {
                        error += $"The property '{property.Name}' at line {((IJsonLineInfo)property.Value).LineNumber} char {((IJsonLineInfo)property.Value).LinePosition} was not one of the allowed buckets for data types.\n";
                    }
                }
                JObject obj = property.Value.ToObject<JObject>()!;
                foreach (JProperty nestedProperty in obj.Properties())
                {
                    dataBag.Add(nestedProperty.Name, nestedProperty.Value);
                }
            }
            if (error is "")
            {
                error = null;
                to = JsonConvert.SerializeObject(dataBag, jsonSerializerSettings);
                message = "Successfully parsed.";
            }
            else
            {
                error = error[..^2];
                message = null;
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            message = null;
            throw;
        }
    }

    public void ConvertFromDataValueToJSON(bool suppressWarnings)
    {
        try
        {
            Dictionary<string, DataValue?>? databag = default!;
            try
            {
                databag = JsonConvert.DeserializeObject<Dictionary<string, DataValue?>>(from);
            }
            catch (Exception)
            {
                error = "Input was not valid JSON or was not a valid DataValue bag.";
                message = null;
                return;
            }
            if (databag is null)
            {
                to = "";
                error = null;
                message = "Successfully parsed.";
                return;
            }
            from = JsonConvert.SerializeObject(databag, jsonSerializerSettings);
            Dictionary<string, dynamic?> json = [];
            error = "";
            foreach ((string key, DataValue? value) in databag)
            {
                json.Add(key, Parse(value, suppressWarnings));
            }
            if (error is "")
            {
                to = JsonConvert.SerializeObject(json, simpleJsonSerializerSettings);
                error = null;
                message = "Successfully parsed.";
            }
            else
            {
                error = error[..^2];
                message = null;
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            message = null;
            throw;
        }
    }

    public DataValue? Parse(JToken token, bool suppressWarnings)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                JObject obj = token.ToObject<JObject>()!;
                if (obj.Properties().All(p => p.Value.Type is JTokenType.String or JTokenType.Null))
                {
                    return new Multilingual(obj.Properties().Select(p => new Multilingual.Value(p.Name, p.Value.ToString())).ToList());
                }
                if (obj.Properties().All(p => p.Value.Type is JTokenType.Integer or JTokenType.Float))
                {
                    return new MultiCurrency(obj.Properties().Select(p => new Money(p.Name, p.Value.ToObject<decimal>())).ToList());
                }
                if (obj.Properties().All(p => p.Value.Type is JTokenType.Array && p.Value.ToArray().All(e => e.Type is JTokenType.String or JTokenType.Null)))
                {
                    return new MultilingualCollection(obj.Properties().Select(p => new MultilingualCollection.Value(p.Name, p.Value.ToArray().Select(e => e.ToString()).ToList())).ToList());
                }

                if (!suppressWarnings)
                {
                    error += $"Some JSON object could only be mapped to a DataValue 'DataObject' type which is almost never what you want. Please check this. The origin was at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                }

                return new DataObject(obj.Properties().ToDictionary(p => p.Name, p => Parse(p.Value, suppressWarnings)));
            case JTokenType.Array:
                DataValue?[] array = token.ToArray().Select(e => Parse(e, true)).ToArray();
                if (array.All(dv => dv?.Type is DataValue.DataValueTypes.String))
                {
                    return array.Select(dv => (string)dv!.Value!).ToList();
                }
                if (array.All(dv => dv?.Type is DataValue.DataValueTypes.Object))
                {
                    return array.Select(dv => (DataObject)dv!).ToList();
                }
                if (array.All(dv => dv?.Type is DataValue.DataValueTypes.Double))
                {
                    return array.Select(dv => (double)dv!.Value!).ToList();
                }
                if (array.All(dv => dv?.Type is DataValue.DataValueTypes.Boolean))
                {
                    return array.Select(dv => (bool)dv!.Value!).ToList();
                }
                if (suppressWarnings)
                {
                    return array.Where(dv => dv is not null).Select(dv => dv!.Value is DataObject ? (DataObject)dv.Value : new DataObject(new() { ["Value"] = dv })).ToList();
                }
                else
                {
                    error += $"Unsupported array of mixed types at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                    return null;
                }

            case JTokenType.Integer or JTokenType.Float:
                return token.ToObject<int>();
            case JTokenType.String:
                return token.ToObject<string>();
            case JTokenType.Boolean:
                return token.ToObject<bool>();
            case JTokenType.Null:
                return null;
            case JTokenType.Undefined:
                if (!suppressWarnings)
                {
                    error += $"Undefined value at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                }

                return null;
            case JTokenType.Date:
                return token.ToObject<DateTimeOffset>().ToUnixTimeMilliseconds();
            case JTokenType.Raw:
                if (!suppressWarnings)
                {
                    error += $"Unsupported 'Raw' type at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                }

                return null;
            case JTokenType.Bytes:
                if (!suppressWarnings)
                {
                    error += $"Unsupport 'Bytes' type at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                }

                return null;
            case JTokenType.Guid:
                return token.ToObject<Guid>().ToString();
            case JTokenType.Uri:
                return token.ToObject<Uri>()?.ToString();
            case JTokenType.TimeSpan:
                return token.ToObject<TimeSpan>().TotalMilliseconds;
            default:
                if (!suppressWarnings)
                {
                    error += $"Unsupported type '{token.Type}' at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                }

                return null;
        }
    }

    public string? WebhookDataBucket(JToken token, bool suppressWarnings)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                JObject obj = token.ToObject<JObject>()!;
                if (obj.Properties().All(p => p.Value.Type is JTokenType.String or JTokenType.Null))
                {
                    return "multilingual";
                }
                if (obj.Properties().All(p => p.Value.Type is JTokenType.Integer or JTokenType.Float))
                {
                    if (!suppressWarnings)
                    {
                        error += $"Some JSON object could only be mapped to a DataValue 'MultiCurrency' type which is not supported for the Webhook Data format. The origin was at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                    }

                    return null; // The spec doesn't support Multicurre
                }
                if (obj.Properties().All(p => p.Value.Type is JTokenType.Array && p.Value.ToArray().All(e => e.Type is JTokenType.String or JTokenType.Null)))
                {
                    return "multilingualCollection";
                }

                if (!suppressWarnings)
                {
                    error += $"Some JSON object could only be mapped to a DataValue 'DataObject' type which is not supported for the Webhook Data format. The origin was at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                }

                return null;
            case JTokenType.Array:
                DataValue?[] array = token.ToArray().Select(e => Parse(e, true)).ToArray();
                if (array.All(dv => dv?.Type is DataValue.DataValueTypes.String))
                {
                    return "stringLists";
                }
                if (array.All(dv => dv?.Type is DataValue.DataValueTypes.Object))
                {
                    if (!suppressWarnings)
                    {
                        error += $"Some JSON object could only be mapped to a DataValue 'DataObject' type which is not supported for the Webhook Data format. The origin was at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                    }

                    return null;
                }
                if (array.All(dv => dv?.Type is DataValue.DataValueTypes.Double))
                {
                    return "numberLists";
                }
                if (array.All(dv => dv?.Type is DataValue.DataValueTypes.Boolean))
                {
                    return "booleanLists";
                }
                if (suppressWarnings)
                {
                    return null;
                }
                else
                {
                    error += $"Unsupported array of mixed types at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                    return null;
                }

            case JTokenType.Integer or JTokenType.Float:
                return "numbers";
            case JTokenType.String:
                return "strings";
            case JTokenType.Boolean:
                return "booleans";
            case JTokenType.Null:
                return null;
            case JTokenType.Undefined:
                if (!suppressWarnings)
                {
                    error += $"Undefined value at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                }

                return null;
            case JTokenType.Date:
                return "strings";
            case JTokenType.Raw:
                if (!suppressWarnings)
                {
                    error += $"Unsupported 'Raw' type at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                }

                return null;
            case JTokenType.Bytes:
                if (!suppressWarnings)
                {
                    error += $"Unsupport 'Bytes' type at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                }

                return null;
            case JTokenType.Guid:
                return "strings";
            case JTokenType.Uri:
                return "strings";
            case JTokenType.TimeSpan:
                return "strings";
            default:
                if (!suppressWarnings)
                {
                    error += $"Unsupported type '{token.Type}' at line {((IJsonLineInfo)token).LineNumber} char {((IJsonLineInfo)token).LinePosition}.\n";
                }

                return null;
        }
    }

    public dynamic? Parse(DataValue? dataValue, bool suppressWarnings)
    {
        if (dataValue?.Value is null)
        {
            return null;
        }
        switch (dataValue.Type)
        {
            case DataValue.DataValueTypes.String or DataValue.DataValueTypes.Double or DataValue.DataValueTypes.Boolean:
                return dataValue.Value;
            case DataValue.DataValueTypes.Multilingual:
                Dictionary<string, string?> multilingual = [];
                foreach (Multilingual.Value value in ((JObject)dataValue.Value!).ToObject<Multilingual>()!.Values!)
                {
                    multilingual.Add(value.Language.Value, value.Text);
                }
                return multilingual;
            case DataValue.DataValueTypes.MultiCurrency:
                Dictionary<string, decimal> multicurrency = [];
                foreach (Money value in ((JObject)dataValue.Value!).ToObject<MultiCurrency>()!.Values!)
                {
                    multicurrency.Add(value.Currency.Value, value.Amount);
                }
                return multicurrency;
            case DataValue.DataValueTypes.StringList:
                return ((JArray)dataValue.Value!).Children().Select(t => t.ToString()).ToArray();
            case DataValue.DataValueTypes.DoubleList:
                return ((JArray)dataValue.Value!).Children().Select(t => t.ToObject<double>()).ToArray();
            case DataValue.DataValueTypes.BooleanList:
                return ((JArray)dataValue.Value!).Children().Select(t => t.ToObject<bool>()).ToArray();
            case DataValue.DataValueTypes.MultilingualCollection:
                Dictionary<string, List<string?>?> multilingualCollection = [];
                foreach (MultilingualCollection.Value? value in ((JObject)dataValue.Value!).ToObject<MultilingualCollection>()!.Values)
                {
                    multilingualCollection.Add(value.Language.Value, value.Values);
                }
                return multilingualCollection;
            case DataValue.DataValueTypes.Object:
                Dictionary<string, dynamic?> obj = [];
                foreach ((string key, DataValue? value) in ((JObject)dataValue.Value!).ToObject<DataObject>()!.Data)
                {
                    obj.Add(key, Parse(value, suppressWarnings));
                }
                return obj;
            case DataValue.DataValueTypes.ObjectList:
                List<dynamic?> objs = [];
                foreach (DataObject originalObj in ((JObject)dataValue.Value!).ToObject<List<DataObject>>()!)
                {
                    Dictionary<string, dynamic?> newObj = [];
                    foreach ((string key, DataValue? value) in originalObj.Data)
                    {
                        newObj.Add(key, Parse(value, suppressWarnings));
                    }
                    objs.Add(newObj);
                }
                return objs;
            default:
                if (!suppressWarnings)
                {
                    error += $"Unsupported DataValue type '{dataValue.Type}' at line.\n";
                }

                return null;
        }
    }
}