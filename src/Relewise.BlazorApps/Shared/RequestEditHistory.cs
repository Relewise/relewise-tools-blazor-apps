using System.IO.Compression;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Relewise.BlazorApps.Shared;

public static class RequestEditHistory
{
    private const string StorageKey = "requestEditHistory";

    public sealed class Entry
    {
        public string Id { get; set; } = "";
        public string Compressed { get; set; } = "";
        public string Title { get; set; } = "";
        public string Preview { get; set; } = "";
        public DateTimeOffset? SavedAt { get; set; }
    }

    public static List<Entry> Load(IJSInProcessRuntime runtime)
    {
        string? stored = runtime.Invoke<string?>("localStorage.getItem", StorageKey);
        if (stored is not null)
        {
            var history = (JsonConvert.DeserializeObject<List<Entry>>(stored) ?? []).Take(10).ToList();
            if (history.Any(entry => string.IsNullOrEmpty(entry.Id)))
            {
                foreach (var entry in history.Where(entry => string.IsNullOrEmpty(entry.Id)))
                {
                    entry.Id = Guid.NewGuid().ToString();
                }
                runtime.InvokeVoid("localStorage.setItem", StorageKey, JsonConvert.SerializeObject(history));
            }
            return history;
        }

        string? legacy = runtime.Invoke<string?>("localStorage.getItem", "lastRequest");
        if (legacy is null)
        {
            return [];
        }

        using var bytes = new MemoryStream(Convert.FromBase64String(legacy));
        using var gzip = new GZipStream(bytes, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        var legacyEntry = CreateEntry(reader.ReadToEnd(), legacy, null);
        legacyEntry.Id = Guid.NewGuid().ToString();
        List<Entry> migrated = [legacyEntry];
        runtime.InvokeVoid("localStorage.setItem", StorageKey, JsonConvert.SerializeObject(migrated));
        return migrated;
    }

    public static string Save(IJSInProcessRuntime runtime, string serialized, string compressed, string? editId = null)
        => Save(runtime, serialized, compressed, out _, editId);

    public static string Save(IJSInProcessRuntime runtime, string serialized, string compressed, out List<Entry> history, string? editId = null)
    {
        history = Load(runtime);
        int existingIndex = history.FindIndex(entry => editId is not null && entry.Id == editId);
        if (existingIndex < 0)
        {
            existingIndex = history.FindIndex(entry => entry.Compressed == compressed);
        }

        Entry? existing = existingIndex >= 0 ? history[existingIndex] : null;
        string id = !string.IsNullOrEmpty(existing?.Id) ? existing.Id : editId ?? Guid.NewGuid().ToString();
        if (existingIndex == 0 && existing!.Compressed == compressed && existing.Id == id)
        {
            return id;
        }
        if (existingIndex >= 0)
        {
            history.RemoveAt(existingIndex);
        }

        var updated = CreateEntry(serialized, compressed, DateTimeOffset.UtcNow);
        updated.Id = id;
        history.Insert(0, updated);
        history = history.Take(10).ToList();
        runtime.InvokeVoid("localStorage.setItem", StorageKey, JsonConvert.SerializeObject(history));
        return id;
    }

    private static Entry CreateEntry(string serialized, string compressed, DateTimeOffset? savedAt)
    {
        var json = JObject.Parse(serialized);
        string? type = json.Value<string>("$type")?.Split(',')[0].Split('.').Last();
        json.Remove("$type");
        string preview = json.ToString(Formatting.None);
        return new Entry
        {
            Compressed = compressed,
            Title = type ?? "Request",
            Preview = preview.Length > 160 ? preview[..160] + "…" : preview,
            SavedAt = savedAt
        };
    }
}
