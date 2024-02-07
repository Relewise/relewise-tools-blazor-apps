using KristofferStrube.Blazor.Popper;
using KristofferStrube.Blazor.Relewise.WasmExample;
using KristofferStrube.Blazor.Relewise.XmlSummaries;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<Popper>();
builder.Services.AddSingleton<XMLDocumentationCache>();

await builder.Build().RunAsync();
