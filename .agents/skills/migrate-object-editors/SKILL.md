---
name: migrate-object-editors
description: Continue the Relewise Blazor ObjectEditor migration to R components and MudBlazor, preserve editing and serialization behavior, and diagnose recursive editor rendering performance. Use for editor controls, collections, request editor layout, or regressions from this migration.
---

# Migrate object editors

Read the root `AGENTS.md` and inspect the current working tree. This skill captures the established editor design and lessons from the migration; current user instructions can change that design. It does not authorize dependency upgrades, authentication changes, dataset operations, deployment, or Git delivery.

## Choose the relevant context

1. For a control or layout change, inspect its editor, the matching `Settings.Editors` entry, and its parent rendering path. Read [the migration baseline](references/migration-baseline.md) for the relevant control or layout.
2. For sluggish rendering or incomplete type choices, inspect `ObjectEditor.razor.cs`, `EditorSdkTypes`, `Settings.GetProperties`, and the request page's retained editor state. Apply the performance guidance below before changing how types are discovered.
3. For creation, collapse, or collection regressions, reproduce on the actual request page, not only Models or an isolated component. Parent setters, parameter updates, and DOM events can change the result.

## Architecture and component selection

1. `src/Relewise.BlazorApps/Settings.cs` is an ordered registry: the first matching handler wins. Place specializations before broader collection handlers; keep the object fallback last. A handler's initial value must have the expected CLR type.
2. Prefer existing wrappers in `src/Relewise.BlazorApps/Components`: `RTextField`, `RNumericField`, `RNullableNumberField`, `RSelect`, `RCheckBox`, `RButton`, and `RIconButton`. Inspect supported parameters rather than assuming every MudBlazor option is forwarded.
3. `TypeEditors/ObjectEditor.razor` creates child editors through `DynamicComponent`. Preserve `Value`, `Setter`, `ReadOnly`, `OpenDetails`, and `AncestorTypes` contracts. Propagate context-specific parameters through the registry/rendering path rather than making every numeric property nullable.
4. `Shared/SearchConstructor.razor` and `Shared/RecommendationConstructor.razor` own request editing and execution. Their `EditorValidationState` cascade currently handles dictionary draft errors before execution. The page may retain a separate editor state per request type.
5. Preserve `$type` JSON metadata, SDK wrapper objects, compressed model links, `lastRequest`, custom dictionary entries, and nullable/required distinctions. An inline field is a presentation change, not permission to flatten the SDK model.

## Rendering performance

1. Cache SDK type candidates per generic editor and filter them by the current ancestor/context restrictions. The current `derivedTypeCandidates` is a static `Lazy<Type[]>` using `EditorSdkTypes.All`. Do not restore per-render assembly scans or narrow a selector to only the current type to hide the cost.
2. Build popup choices only while the menu is open. The current top-level Switch type and polymorphic Create menus conditionally render items under `typeMenuOpen`; creating many hidden menus across a recursive object tree was costly.
3. Preserve all permitted derived types. Check Searches, Recommendations, collections, and nested contexts: a fast selector missing legitimate alternatives is a regression.
4. Reuse the default comparison instance while its runtime type is unchanged (`emptyInstanceType`), and cache type title documentation (`typeTitles`). Avoid reconstructing defaults or repeatedly fetching documentation during ordinary value edits.
5. Keep inactive request/model editors from rendering unnecessarily while preserving their state. Inspect `IsActive`, `ShouldRender`, keyed components, and retained page state before introducing broader memoization or component disposal.
6. Measure the actual interaction: initial page load, opening Switch type, creating a nested object, and editing a primitive. Distinguish expensive discovery, component creation, documentation fetches, and parent rerenders. Optimize the demonstrated bottleneck without changing SDK type restrictions or editor selection order.

## Creation and collapse behavior

1. Creating an editable complex object should expose its fields immediately. Root objects remain open; nested header clicks retain collapse functionality without a visible arrow or plus/minus controls.
2. Keep copy/remove/menu actions from toggling their surrounding header. Native `<summary>` default behavior can interact with click bubbling; use appropriate propagation/default prevention and verify keyboard behavior.
3. `StringDictionaryEditor` uses explicit `expanded` state and a `<section>`, rather than native `<details>`. Creation and a newly supplied editable empty dictionary open a key/value row. Ordinary parent rerenders must not reset a user's manual collapse.
4. Track external `OpenDetails` changes separately from ordinary parameter updates. Do not unconditionally assign local expansion state from `OpenDetails` on every render.

## Validation and local preview

1. Follow the runtime and validation commands in `AGENTS.md`. Use `https://localhost:7176` and `dotnet watch --no-hot-reload --project src/Relewise.BlazorApps`. Inspect an occupied port's owner; do not stop an unrelated process or silently change the port.
2. Restart the watcher after adding Razor files or changing package/startup registrations. A stale running WebAssembly assembly can make a correct source fix appear ineffective. Hard-refresh the browser after the rebuilt server is ready; remote previews may not receive the local reload notification.
3. Prefer the product-native collaborative browser tools when available: status, open, then inspect and interact. If an interaction reports failure, inspect the resulting DOM before retrying, since an action may have executed. Do not infer success solely from a click tool's return value.
4. Verify rendered fields, visibility, underlying values, and copy/serialization output. For dictionary creation, check `data-expanded`, visible key/value inputs, and survival after an unrelated parent edit. A header appearing is not proof that its rows appeared.
5. Use synthetic data. Models is useful for isolated editing without request autosave, but also check the actual affected request constructor safely. Do not execute requests using real customer credentials. Request-page setters write `lastRequest`; avoid overwriting the user's saved request during automated checks.
6. For remote access when requested, host the configured HTTPS port with `devtunnel host -p 7176 --protocol https`. Report the actual emitted URL; do not reuse a URL from an earlier session or enable anonymous access without authorization.
7. Add focused regressions for type selection, nullable CLR values, wrapper serialization, dictionary drafts/duplicates, read-only guards, and parent rerender behavior. You may run the specific tests you add using a focused filter or command, as allowed by `AGENTS.md`. Running other tests or the full suite requires an explicit user request; report tests as unrun when applicable.
8. Use builds and focused browser checks during iteration. Run Release AOT publishing only when the user explicitly requests it, including for registry, reflection, new generic editor, or dependency changes; report unverified AOT compatibility when relevant. Do not run build/test-with-build/publish against the same intermediate directory concurrently; use distinct `--artifacts-path` and publish outputs when needed. A local publish is not deployment.
9. Read/write Razor files as UTF-8, accounting for an existing BOM. Incorrect decoding previously introduced visible `ï»¿` text beside icons. Review diffs and rendered text after automated edits.

When reporting completion, distinguish implemented behavior, browser verification, builds, tests, and AOT results. Do not turn a successful earlier publish into validation of later source changes.
