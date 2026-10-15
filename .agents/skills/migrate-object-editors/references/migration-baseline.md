# Object editor migration baseline

This is the agreed design at the time this skill was created, not a mandate to reject later user changes. Confirm the source before extending it.

## Layout and interactions

1. The root ObjectEditor has a humanized title, a Switch type popup menu, and copy/share actions on the right. It stays open, has no collapse controls, no light blue header, and no extra border inside the page's existing border.
2. Nested complex objects keep one outlined border matching input fields. The compact bold type name uses the property label's font and size, without a type-name tooltip. Clicking the header collapses/expands; the arrow and plus/minus controls are hidden. Copy and an outlined red bin sit inside the header.
3. Property labels sit before their values in a shared two-column CSS grid: `max-content minmax(0, 1fr)`, 4px horizontal gap, top alignment, and 13px label top padding. This fits the longest sibling label instead of imposing a large fixed label column. Nested property content is slightly indented relative to its header. Do not restore horizontal dividers or duplicate labels inside every field.
4. Property documentation tooltips belong to the labels. They appear without a delay, wrap to roughly 500px maximum width, and must constrain the actual tooltip content rather than just its trigger. Action buttons keep their own tooltips.
5. A null complex object looks like an outlined input: the same `<NULL>` position, font, and muted color as a real field, with a `Create` action inside. The whole null area can create the object; polymorphic types use a Create menu. Creation should open it immediately.
6. Main and content sidebars are independently collapsible. Keep selection and request editing state when toggling navigation. Searches and Recommendations omit the extra selected-request heading before the root ObjectEditor.

## Primitive and wrapper fields

1. Strings use `RTextField` with `<NULL>` placeholder, immediate updates, and clearing to null. Avoid accidentally supplying `Value` as literal display text. Preserve read-only guards in both the UI and setter.
2. Numeric editors distinguish required and nullable CLR types through `Settings.WithNumericNullability` and `AllowNull`. Nullable fields support clearing to null and spinner buttons; required integer fields use `RNumericField` and retain their numeric type. There is no `Empty = null` helper text. Never offer null for non-nullable Skip/Take merely because a shared editor accepts a nullable parameter.
3. Booleans use `RCheckBox` with the property name supplied as an accessible label, without duplicating the visible property label. Nullable booleans retain their null semantics; inspect `NullableBoolEditor` before changing its existing null controls.
4. `Language` and `Currency` are explicit inline object specializations in `ObjectEditor.IsInlineValueType`. Show the string input even while the wrapper is null. Typing creates/updates the wrapper; clearing removes it. Copy must serialize the whole SDK wrapper, not only its string value.
5. Inline wrapper fields use the text field's end copy adornment alongside its clear X. Ordinary primitive fields only have a copy action if their caller supplies one; the migration has not added copy to every primitive editor.
6. Non-nullable enums use `RSelect`. Defined values retain their names; an undefined numeric zero displays `Select…`. Defined zero values remain real options. The CLR value remains zero, not null. Clearing is disabled and the select is marked Required, but this alone does not prove that request execution rejects an undefined enum value.
7. For `PopularProductsRequest.BasedOn`, the SDK values inspected were MostPurchased=5, MostViewed=10, and LineRevenue=15. The serialization constructor leaves it at zero; normal SDK constructors require a `PopularityTypes` argument. API rejection/default handling of zero was not established. Verify current SDK/API behavior before making a stronger claim or adding execution validation.

## Dictionary<string, string>

1. `Settings.Editors` selects `StringDictionaryEditor` only for the exact string/string dictionary type, before the generic `DictionaryEditor<,>` handler. Other dictionary types retain their existing editor.
2. Null renders as an outlined `<NULL>` field with Create. Creation opens one blank key/value row and focuses the key. The compact header shows entry count, copy JSON, and an outlined red remove icon. Each used row has an inline remove icon.
3. Rows are editing drafts distinct from committed dictionary entries. Once a draft has both key and value, commit it and provide the next blank row automatically. No Add entry button is needed. An untouched spare row is ignored by serialization and execution.
4. Keep at most one entirely blank draft and place it last. If a partially filled draft exists, do not add another blank merely because the user typed a key. When clearing rows, coalesce blank drafts without discarding the active row unnecessarily.
5. Duplicate keys color the affected key text red; no red helper text is rendered below the key/value fields. A duplicate draft must not overwrite the earlier entry. Use the dictionary's comparer when detecting duplicates.
6. Preserve loaded empty-string keys/values and existing null values rather than reclassifying them as unused drafts. Read-only rendering has no spare row or mutation controls. Only edited incomplete rows and duplicate drafts participate in draft validation.
7. `Shared/EditorValidationState.cs` collects draft errors from editors. Search/Recommendation constructors check the cascade before execution and report an error. This is not yet a universal validation framework for every page or field.
8. Regression coverage is in `StringDictionaryEditorTests.cs`. Verify create/open, external empty dictionary initialization, a parent rerender, auto rows, duplicate preservation, removing/clearing, comparer behavior, read-only data, and copied JSON excluding drafts. Tests added during the migration were not executed in the original session; do not describe them as passing without running them.

## Remaining scope and adjacent work

1. The migration is incremental. Native controls remain in some collection editors, `NullableEnumEditor`, and nested polymorphic object selectors. Inspect the actual path before assuming a field already uses an R component.
2. Saved connection storage is enabled in both Debug and Release. The standalone UI offers Remember connection in this browser and Forget saved configuration; restore/save use the existing browser storage module. Embedded connections are still excluded. Do not change the authentication handshake as part of an editor migration.
3. Inspect the working tree rather than assuming these changes are committed, published, or deployed. Preserve unrelated pending work. Do not carry transient process IDs, tunnel URLs, credentials, machine-specific paths, or historical build logs into permanent project documentation.
