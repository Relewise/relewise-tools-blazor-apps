using KristofferStrube.Blazor.DOM;
using KristofferStrube.Blazor.Window;

namespace KristofferStrube.Blazor.Relewise.WasmExample;

public static class StaticDatasetStorage
{
    public static string ServerUrl { get; set; } = "https://api.relewise.com/";
    public static string DatasetId { get; set; } = "";
    public static string ApiKey { get; set; } = "";
    public static string? ParentOrigin { get; set; }
    public static EventListener<MessageEvent>? ParentMessageEventListener { get; set; }
    public static event EventHandler? AuthenticationReceived;

    public static void NotifyAuthenticationReceived()
    {
        AuthenticationReceived?.Invoke(null, EventArgs.Empty);
    }
}
