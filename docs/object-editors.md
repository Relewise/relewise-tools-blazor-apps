# Editor architecture and behavior

This guide describes the editor contracts and interaction conventions used by the application. Check the affected implementation before extending it; user requirements can change the design. Development commands and validation permissions are in [AGENTS.md](../AGENTS.md).

## Architecture

1. `Settings.Editors` is an ordered registry. The first matching handler wins, specializations precede general collection handlers, and the object fallback stays last. Initial values must have the exact CLR type expected by the selected component.
2. `TypeEditors/ObjectEditor.razor` creates child editors through `DynamicComponent`. Preserve the `Value`, `Setter`, `ReadOnly`, `OpenDetails`, and `AncestorTypes` contracts and the context restrictions used to select derived SDK types.
3. Prefer existing wrappers in `Components/`, including `RTextField`, `RNumericField`, `RNullableNumberField`, `RSelect`, `RCheckBox`, `RButton`, and `RIconButton`. Inspect their supported parameters rather than assuming every MudBlazor option is forwarded. Bind string parameters as expressions when their values come from variables.
4. `Shared/SearchConstructor.razor` and `Shared/RecommendationConstructor.razor` own request editing and execution. Pages retain editor state, including the last response, independently of ordinary request edits. A root type switch must update the request and client family without automatically executing the replacement.
5. Preserve JSON `$type` metadata, compressed model links, the `lastRequest` storage format, query parameters, custom properties, and SDK wrapper objects. Presentation changes must not flatten or otherwise change serialized models.

## Layout and interaction

1. A root object stays open and has a humanized title, a Switch type popup, and copy/share actions. Avoid duplicate page headings and extra borders around the root editor.
2. Nested objects use one outlined border and a compact bold type name. Clicking the header collapses or expands the fields. Keep copy, remove, and menu actions from toggling the header; verify both click propagation and native summary behavior, including keyboard interaction.
3. Property labels precede their values in a two-column grid that fits the longest sibling label and lets the value column shrink. Keep nested fields slightly indented, without horizontal dividers or repeated labels inside controls.
4. Put property documentation tooltips on labels. Show them without delay, wrap the actual tooltip content to roughly 500px, and constrain it to the viewport. Action buttons retain their own tooltips.
5. A null complex object resembles an outlined input with a muted `<NULL>` placeholder and an inline Create action. The null area can create the object; polymorphic types offer their permitted choices. Creating an object opens its fields immediately.
6. Preserve manual expansion across parent renders. Track explicit changes to `OpenDetails` separately from ordinary parameter updates. Default nested dictionaries may start collapsed; creating a dictionary opens a blank row.
7. Keep selection, edits, and responses when toggling navigation or request/result layout. Editing a request must not discard its previous response.

## Primitive fields and SDK wrappers

1. Strings use immediate `RTextField` updates and a `<NULL>` placeholder. Clearing normally produces null; honor `AllowNull` where null is forbidden, including protected dictionary keys. Guard setters as well as visible controls when read-only.
2. Numeric editors preserve required versus nullable CLR types through `Settings.WithNumericNullability` and `AllowNull`. Required values such as Skip and Take must not accept null merely because a shared control supports it. Preserve numeric CLR types after editing and deserialization.
3. Booleans use `RCheckBox` with an accessible property label, without repeating the visible property label. Nullable booleans retain their null semantics.
4. `Language` and `Currency` use inline string controls even when the wrapper is null. Typing creates the wrapper and clearing removes it. Copy serializes the whole wrapper. Their copy adornment is separate from the clear action.
5. Non-nullable enums use `RSelect`. An undefined numeric zero displays `Select…`; a defined zero remains a real option. Keep the CLR value unchanged until selection, and do not infer API validation from the control's Required flag alone.

## Collections, drafts, and validation

1. Exact string/string and string/DataValue dictionaries use specialized editors before the generic dictionary handler. Preserve each dictionary's comparer and loaded empty keys, empty values, and null values.
2. Dictionary rows distinguish drafts from committed entries. Untouched spare rows stay out of serialization. Keep at most one entirely blank draft at the end, and do not add another spare while an entry is incomplete.
3. A duplicate or incomplete draft must not overwrite or remove committed entries. Highlight duplicate keys using the dictionary's comparer. Isolate mutable list and object payloads too: replacing only their enclosing DataValue wrapper leaves the same payload shared with committed data.
4. Read-only collections have no mutation controls or spare rows, and their setters reject attempted mutations. Preserve row identity during editing so focus and partially entered values survive updates. Structural changes such as adding, removing, or replacing an item's type must update the visible rows and headers.
5. `Shared/EditorValidationState.cs` collects dictionary draft errors. Request constructors and dataset save pages use its cascade. Check the relevant execution/save path when adding controls; this is not a universal validator for every field or action. Clear an editor's registered error when it is disposed.
6. Search and recommendation requests expose Disable merchandising beside Custom. It controls only `Debug_DisableMerchandising` with the string value `true`; removing that flag preserves other Custom entries. Keep manual dictionary edits and the checkbox synchronized, and respect read-only mode.

## Rendering performance

1. Cache SDK type candidates per generic editor using `EditorSdkTypes.All`, then apply ancestor/context restrictions. Do not scan assemblies on every render or hide legitimate derived types to reduce the work.
2. Build Create and Switch type popup choices only while their menus are open. Rendering hidden choices throughout a recursive editor tree is expensive.
3. Reuse default comparison instances while the runtime type is unchanged. Cache reflected properties and documentation help rather than looking them up on every keystroke. Invalidate caches when their runtime type or documentation source changes so delayed documentation and retries still work.
   Read-only API responses are snapshots: their non-default property comparisons are weakly cached by object identity and shared across recreated editors. Replace a response snapshot when its data changes. Editable request comparisons must continue to reflect in-place edits.
4. Ordinary edits should update the affected control without re-rendering unrelated facets, collections, or responses. Request Explorer uses a non-rendering edit callback and explicitly refreshes surrounding UI when needed. Preserve refreshes for root replacements, dynamic help, identifier headers, Custom options, diagnostics becoming stale, and visible source JSON/history.
5. Request Explorer groups rapid edits into one snapshot after a 300 ms pause, reusing the serialized JSON for source display, compression and history. Flush pending edits before execution, source editing/display, request replacement, navigation, backgrounding or disposal. Constructors delegate persistence to their owning page when a RequestChanged handler is supplied; standalone constructors retain their immediate persistence path.
6. Keep inactive editors from rendering unnecessarily while retaining their state. Inspect `IsActive`, `ShouldRender`, keyed components, and page-owned state before changing component lifetimes or adding broader memoization.
7. Measure the affected interaction with a representative request, including expanded nested collections and an existing response. Separate type discovery, documentation, serialization/history writes, rendering, and network time. Development measurements do not establish published AOT performance.

## Verification

1. Use synthetic data and the product-native collaborative browser when available. Models is useful for isolated checks, but reproduce parent/setter interactions on the actual affected request page too. Request-page edits write browser history; use an isolated session rather than overwriting the user's saved requests.
2. Check visible controls, underlying CLR values, copied JSON, and saved/reopened requests. For collection creation, inspect actual rows and expansion state rather than only the header.
3. Check create/remove, null and required values, type switching, duplicate drafts, comparer behavior, read-only setters, parent renders, and response preservation. For performance regressions, count renders of unrelated components rather than relying only on timing-sensitive test thresholds.
4. Focused coverage lives in the editor-specific test classes and `RequestEditingPerformanceTests`. Follow the test permissions in AGENTS.md and report what actually ran; a successful build does not establish AOT compatibility.
5. Read and write Razor files as UTF-8 while accounting for an existing BOM. Review diffs and rendered text after automated edits to catch encoding artifacts.
