# Relewise Blazor Tools

Experimental browser tools for exploring Relewise models, constructing search/recommendation requests, troubleshooting searches, editing products/content and merchandising rules, and comparing SDK versions.

**Not for production use.** Entity and merchandising actions can modify the connected dataset.

[Open the tools](https://relewise.github.io/relewise-tools-blazor-apps/) · [Report an issue](https://github.com/Relewise/relewise-tools-blazor-apps/issues/new)

## Run locally

Install the .NET SDK selected by `global.json`. From the repository root:

```powershell
dotnet workload restore
dotnet restore relewise-tools-blazor-apps.sln
dotnet watch --project src/Relewise.BlazorApps
```

Open the localhost URL printed by the dev server. The application is standalone WebAssembly; it does not need a local backend or private NuGet feed.

Model editing works without dataset credentials. SDK documentation and version comparison use `https://cdn.relewise.com/services/blazor-apps/stable/nuget/`. That service is maintained in `cdn.relewise.com`, under `src/Services.BlazorApps.Website`. Dataset operations call the supplied Relewise server directly from the browser. Embedded mode receives credentials through the My Relewise parent-window handshake.

## Validate changes

```powershell
dotnet build relewise-tools-blazor-apps.sln --configuration Release
dotnet test tests/Relewise.BlazorApps.Tests --configuration Release --no-build --filter 'TestCategory!=Browser'
pwsh tests/scripts/read-pr-comments.Tests.ps1
dotnet publish src/Relewise.BlazorApps --configuration Release --no-restore --output artifacts/publish
pwsh tests/Relewise.BlazorApps.Tests/bin/Release/net10.0/playwright.ps1 install chromium
$env:BLAZOR_PUBLISH_DIR = (Resolve-Path artifacts/publish/wwwroot).Path
dotnet test tests/Relewise.BlazorApps.Tests --configuration Release --no-build --filter 'TestCategory=Browser'
```

Publishing compiles WebAssembly ahead of time and can take several minutes. Windows publishing also requires the native build prerequisites reported by the .NET workload (including Python on PATH). On Linux, install Chromium dependencies using `install --with-deps chromium`. Generated outputs go under ignored `artifacts/`, `bin/`, and `obj/` directories.

Unit/component tests cover cache concurrency and retries, typed editor defaults, nullable byte editing, numeric data conversion, custom properties, feed discovery, and SDK serialization. Browser tests launch their own local static host, use fixture network responses, and validate published output under a GitHub Pages path prefix, including tooltips, SDK DLL comparison, request round-trips, a fixture-backed SDK search, and the iframe handshake. Browser tests are inconclusive if `BLAZOR_PUBLISH_DIR` is not supplied; use the separate commands above to explicitly validate both groups.

For changes touching SDK types, routes, or dependencies, also inspect the affected tools interactively: edit/copy/reopen a model, restore a saved request, compare SDK versions against the CDN, and inspect tooltips. Dataset writes require an explicitly designated test dataset. Never paste a real API key into test fixtures or screenshots.

## Maintenance and deployment

The application has one production project. `Settings.cs` and `TypeEditors/` turn SDK types into UI through reflection; upgrades can affect runtime behavior even when compilation succeeds.

PRs target `main`, normally link to a Trello task, and run build, tests, AOT publish, and browser validation. The same validation runs on main before publishing to `gh-pages` using the existing `PUBLISH_TOKEN`. GitHub Pages serves that branch. Configure the branch protection required check as `validate` after its first run; workflow files alone cannot enforce merge protection.

Issue creation uses the existing Relewise Hub integration to create a Trello inbound card. `HUB_BASIC_AUTH_USERNAME` and `HUB_BASIC_AUTH_PASSWORD` are repository secrets used only by that workflow.

See [CONTRIBUTING.md](CONTRIBUTING.md), [AGENTS.md](AGENTS.md), and the repository's `.agents/skills/` for the development workflow. Third-party browser assets and their update sources are documented in [THIRD-PARTY.md](THIRD-PARTY.md).
