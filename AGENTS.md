# Working in this repository

This is a standalone .NET 10 Blazor WebAssembly application for exploring and editing Relewise requests and entities. Keep changes focused on the requested behavior. User instructions define scope and override skill defaults.

## Map and architecture

- `src/Relewise.BlazorApps/Pages`: routed tools, including model/request explorers, entity editing, recommendations, and version comparison.
- `Settings.cs`: ordered editor registry, reflection property selection, and context-specific SDK type restrictions. The first matching editor wins; keep the object fallback last.
- `TypeEditors`: primitive/collection editors and recursive `ObjectEditor`. Initial values must have the exact CLR type expected by the component. Preserve nullable values, custom properties, and read-only behavior.
- `Shared/DataValueExtensions.cs`: normalizes numeric SDK data after JSON deserialization. Preserve nested collections and cycle handling.
- `XmlSummaries`: SDK XML documentation fetched from the CDN and bundled community documentation. Failures must remain retryable; concurrent callers must complete.
- `StaticDatasetStorage.cs`, `Layout/MainLayout.razor`, and `wwwroot/js/embedding.js`: browser credentials and the My Relewise iframe handshake. The local JS module sends the ready message across window realms. Authentication messages are a sensitive compatibility boundary; change them only as part of an explicitly scoped task.
- `NugetClient.cs` and `Pages/Versions.razor`: external CDN metadata/DLL downloads and runtime assembly inspection.
- `tests/Relewise.BlazorApps.Tests`: deterministic MSTest/bUnit regressions.

## Setup and validation

Use the SDK selected by `global.json`, PowerShell 7 for repository scripts, and the `wasm-tools` workload. All packages restore from public NuGet; no private feed credentials are needed.

Keep the configured HTTPS launch profile (`https://localhost:7176`) for local development. Overriding the scheme or port can break external service requests. Restart `dotnet watch` after changing package references or startup service registrations; hot reload cannot reliably apply those changes.
If that port is occupied, inspect its owning process and stop only task-started development servers. Do not silently choose another port or stop unrelated processes.

Use `dotnet watch --no-hot-reload --project src/Relewise.BlazorApps` during development. WebAssembly hot reload has repeatedly caused runtime metadata assertions after Razor changes; rebuild and restart on edits instead.
Restart the watcher after adding Razor components: newly added files have been missed by its existing watch list, leaving the running assembly behind the source.

Run restore and Release build from the repository root as needed. When you add tests yourself, you may run those specific tests without a separate user request; use a focused filter or command to limit execution to the added tests. Running other tests (including script tests), the full test suite, or AOT publishing requires an explicit user request; ordinary implementation, iteration, and validation requests do not authorize those broader checks. Use focused browser checks during UI iteration.

Available commands:

```powershell
dotnet workload restore
dotnet restore relewise-tools-blazor-apps.sln
dotnet build relewise-tools-blazor-apps.sln --configuration Release --no-restore
dotnet test tests/Relewise.BlazorApps.Tests --configuration Release --no-build
pwsh tests/scripts/read-pr-comments.Tests.ps1
dotnet publish src/Relewise.BlazorApps --configuration Release --no-restore --output artifacts/publish
```

Release publishing uses AOT. A build does not establish AOT compatibility; report that limitation when relevant without automatically running or requesting tests or AOT publishing. See README for interactive development and manual checks.

Do not run build, test-with-build, and publish concurrently against the same intermediate directory. When comparing AOT modes or dependency combinations, use a separate `--artifacts-path` and publish directory for each combination; reused native/stripped assemblies can produce misleading runtime failures.

## Change and delivery conventions

- Follow nearby C#/Razor style; avoid repository-wide formatting during functional changes. `.editorconfig` records formatting defaults.
- This is a public repository. Do not add internal CDN or My Relewise implementation details, private source paths, credentials, or internal configuration to code, documentation, tests, or PR descriptions. Describe compatibility requirements only at the level needed to work on this application.
- Add regression tests for changed behavior, especially editor type selection, numeric conversion, serialization, cache retry/concurrency, and route compatibility. Do not add assertions for incidental wording.
- Preserve existing JSON `$type` metadata, compressed model links, the `lastRequest` localStorage format, query parameters, and GitHub Pages path-prefix routing unless explicitly migrating them.
- Use synthetic data for tests. Product/content tracking and merchandising saves mutate datasets; never use real customer credentials for smoke tests or include credentials in files, output, or PRs.
- Use focused `feat/`, `fix/`, or `chore/` branches and conventional commit subjects. Preserve unrelated user changes. Do not switch branches, push, or publish merely because a skill describes delivery; follow the user's authorization.
- Put the task's Trello URL at the top of the PR description when available. Describe resulting behavior, validation, and known limitations.
- `Validate application` runs on PRs and main; deployment runs only after validation on main. Never merge just to test deployment. Repository administrators should require the `validate` status check after its first run.

## Repository skills

1. `.agents/skills/read-pr-comments/SKILL.md`: retrieve PR conversation, reviews, and unresolved review threads without changing the checkout.
2. `.agents/skills/upgrade-dependencies/SKILL.md`: inventory and upgrade dependencies with compatibility checks and explicit exclusions.
3. `.agents/skills/migrate-object-editors/SKILL.md`: continue the R component/MudBlazor editor migration, apply the established layout and collection behavior, and diagnose recursive rendering performance.
