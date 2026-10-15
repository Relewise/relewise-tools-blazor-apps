using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Relewise.Client;

namespace Relewise.BlazorApps.Shared;

public static class RequestErrorHandling
{
    public static async Task<T> ExecuteAsync<T>(ClientBase client, Func<Task<T>> send)
    {
        var previousClient = client.OverriddenHttpClient;
        using var http = new HttpClient(new ErrorResponseHandler(previousClient)) { Timeout = client.Timeout };
        client.OverriddenHttpClient = http;
        try
        {
            return await send();
        }
        finally
        {
            client.OverriddenHttpClient = previousClient;
        }
    }

    public static string Message(Exception exception)
    {
        var failure = exception.GetBaseException();
        return failure is ApiRequestException ? failure.Message
            : failure is HttpRequestException { StatusCode: { } status } ? Describe(status, null)
            : failure.Message;
    }

    private static string Describe(HttpStatusCode status, string? details)
    {
        if (!string.IsNullOrWhiteSpace(details)) return $"{details} (HTTP {(int)status})";
        return status switch
        {
            HttpStatusCode.BadRequest => "The API rejected the request, but did not return validation details. Check the required fields and their values. (HTTP 400)",
            HttpStatusCode.Unauthorized => "The API could not authenticate this request. Check the API key and dataset ID. (HTTP 401)",
            HttpStatusCode.Forbidden => "The API key does not have permission to perform this request. (HTTP 403)",
            _ => $"The API could not complete the request. (HTTP {(int)status})"
        };
    }

    private static string? Details(string body, string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            var json = JToken.Parse(body);
            if (json is JValue { Type: JTokenType.String }) return json.Value<string>();
            if (json is not JObject obj) return null;
            var messages = new List<string>();
            var errors = obj.GetValue("errors", StringComparison.OrdinalIgnoreCase);
            if (errors is JObject fields)
            {
                foreach (var field in fields.Properties())
                {
                    foreach (var value in field.Value is JArray values ? values : new JArray(field.Value))
                    {
                        if (value.Type == JTokenType.String) messages.Add($"{field.Name}: {value.Value<string>()}");
                    }
                }
            }
            else if (errors is JArray values)
            {
                messages.AddRange(values.Where(v => v.Type == JTokenType.String).Select(v => v.Value<string>()!));
            }
            foreach (string name in new[] { "detail", "message", "error", "title" })
            {
                if (messages.Count > 0) break;
                if (obj.GetValue(name, StringComparison.OrdinalIgnoreCase) is JValue { Type: JTokenType.String } value)
                    messages.Add(value.Value<string>()!);
            }
            return messages.Count > 0 ? string.Join(" ", messages.Distinct()) : null;
        }
        catch (JsonReaderException)
        {
            // HTML/proxy pages and binary payloads are not useful validation messages.
            return mediaType?.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase) == true
                && !body.TrimStart().StartsWith('<') ? body.Trim() : null;
        }
    }

    private sealed class ApiRequestException(HttpStatusCode status, string message)
        : HttpRequestException(message, null, status);

    private sealed class ErrorResponseHandler(HttpClient? existing) : HttpMessageHandler
    {
        private readonly HttpClient transport = existing ?? new HttpClient();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Each HttpClient marks a request as sent, so forward a separate message.
            using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content,
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };
            foreach (var header in request.Headers)
                forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value);
            foreach (var option in request.Options)
                forwarded.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);

            HttpResponseMessage response;
            try
            {
                response = await transport.SendAsync(forwarded, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            finally
            {
                // The SDK owns the original request content.
                forwarded.Content = null;
            }
            if (response.IsSuccessStatusCode) return response;
            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var details = body.Length <= 16384 ? Details(body, response.Content.Headers.ContentType?.MediaType) : null;
                throw new ApiRequestException(response.StatusCode, Describe(response.StatusCode, details));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && existing is null) transport.Dispose();
            base.Dispose(disposing);
        }
    }
}
