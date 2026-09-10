---
name: upgrade-dependencies
description: Inventory or upgrade NuGet, browser assets, and GitHub Actions dependencies in Relewise Blazor Tools, validate reflection and AOT compatibility, and report exclusions and results.
---

# Upgrade dependencies

Read the root `AGENTS.md`. Honor the user's package exclusions and delivery scope. An inventory does not require a Trello card, branch changes, or publication.

MessagePack policy: keep the 2.x major line (currently 2.5.302), aligned with Relewise.Client and the Relewise services. Do not upgrade to 3.x during dependency upgrades. Report any remaining advisories or newer-major recommendations without overriding this policy.

1. Inspect the working tree and package declarations. For implementation, work on a focused chore branch within the user's authorized Git scope; preserve unrelated work. Reuse a branch already created for the task rather than forcing a return to main.
2. Run `dotnet list relewise-tools-blazor-apps.sln package --outdated --include-transitive --format json` and `dotnet list relewise-tools-blazor-apps.sln package --vulnerable --include-transitive`. Query prerelease-only packages explicitly: the stable outdated report can say "Not found" even when the installed alpha is the newest release.
3. Review current release notes and compatibility before selecting versions. Keep ASP.NET packages aligned with the target framework, and preserve any explicit version-range policy. Relewise upgrades must be an explicit part of the requested scope; otherwise preserve `Relewise.*` versions. Do not import package exclusions from other repositories.
4. Update direct packages and review transitive changes. Do not add overrides for every old transitive package merely to make an outdated report empty. Direct overrides are appropriate for APIs used by this application or concrete security/compatibility needs, subject to the MessagePack policy above. Validate the SDK's serialization contract when updating serializer dependencies; a successful build is insufficient, and same-version round trips do not establish cross-version interoperability.
5. Check the SDK in `global.json`, GitHub Actions versions in `.github/workflows`, bundled Bootstrap CSS/source map, and bundled Popper JS/source map documented in `THIRD-PARTY.md`. There is no npm application or npm lockfile to regenerate.
6. Run the complete validation commands in `AGENTS.md`: Release build, unit/component tests, and AOT publish. Exercise affected UI paths and external CDN functionality when relevant. Add regression tests for actual upgrade fallout; avoid broad refactoring.
7. Re-run outdated and vulnerability reports. Record before/after versions, unchanged Relewise packages, prerelease-only packages, intentional skips, unresolved advisories, and validation results. Never hide audit warnings to declare success.

When the user requests delivery, commit only task changes, push the task branch, and create the requested PR type. Put the task's Trello URL first when available. Use a UTF-8 temporary file outside the repository with `gh pr create --body-file` for multiline descriptions. Do not merge or deploy as part of an upgrade unless explicitly authorized.
