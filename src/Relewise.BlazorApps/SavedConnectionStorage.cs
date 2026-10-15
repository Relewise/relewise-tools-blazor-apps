using Microsoft.JSInterop;

namespace Relewise.BlazorApps;

public static class SavedConnectionStorage
{
    public static bool Enabled => true;

    public static bool Remember { get; set; } = true;
    public static bool HasSavedConfiguration { get; private set; }
    public static event Action? Changed;
    public static string? Error { get; private set; }

    public static async Task RestoreAsync(IJSRuntime runtime)
    {
        if (!Enabled || StaticDatasetStorage.ParentOrigin is not null)
            return;

        try
        {
            await using var module = await runtime.InvokeAsync<IJSObjectReference>("import", "./js/savedConnection.js");
            string[]? connection = await module.InvokeAsync<string[]?>("read");
            if (connection is { Length: 3 }
                && Uri.TryCreate(connection[0], UriKind.Absolute, out var uri)
                && uri.Scheme is "https" or "http"
                && Guid.TryParse(connection[1], out _)
                && !string.IsNullOrWhiteSpace(connection[2]))
            {
                StaticDatasetStorage.ServerUrl = connection[0];
                StaticDatasetStorage.DatasetId = connection[1];
                StaticDatasetStorage.ApiKey = connection[2];
                HasSavedConfiguration = true;
            }
            Error = null;
        }
        catch (JSException)
        {
            Error = "Could not restore the saved connection. Check that browser storage is available.";
        }
    }

    public static async Task SaveAsync(IJSRuntime runtime)
    {
        if (!Enabled || StaticDatasetStorage.ParentOrigin is not null)
            return;

        try
        {
            await using var module = await runtime.InvokeAsync<IJSObjectReference>("import", "./js/savedConnection.js");
            if (!Remember)
            {
                await module.InvokeVoidAsync("forget");
                HasSavedConfiguration = false;
            }
            else
            {
                await module.InvokeVoidAsync("save", StaticDatasetStorage.ServerUrl, StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey);
                HasSavedConfiguration = true;
            }
            Error = null;
        }
        catch (JSException)
        {
            Error = "Could not update the saved connection. Check that browser storage is available.";
        }
        Changed?.Invoke();
    }

    public static async Task ForgetAsync(IJSRuntime runtime)
    {
        Remember = false;
        await SaveAsync(runtime);
        Remember = true;
        if (Error is null)
        {
            StaticDatasetStorage.ServerUrl = "https://api.relewise.com/";
            StaticDatasetStorage.DatasetId = "";
            StaticDatasetStorage.ApiKey = "";
        }
    }
}
