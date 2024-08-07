using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KristofferStrube.Blazor.Relewise.WasmExample;

public class NugetClient(HttpClient httpClient)
{
    public async Task<List<PackageVersion>> Versions(string package)
    {
        return (await httpClient.GetFromJsonAsync<List<PackageVersion>>($"https://kristoffer-strube.dk/API/nuget/versions/{package}"))!;
    }

    public async Task<byte[]> DLL(string package, string version)
    {
        return await httpClient.GetByteArrayAsync($"https://kristoffer-strube.dk/API/nuget/dll/{package}/{version}");
    }

    public class PackageVersion
    {
        [JsonPropertyName("version")]
        public required string Version { get; set; }

        [JsonPropertyName("published")]
        public DateTimeOffset Published { get; set; }
    }
}
