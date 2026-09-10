# Contributing

Discuss substantial features with a maintainer to agree on scope. Report bugs and feature requests in [GitHub Issues](https://github.com/Relewise/relewise-tools-blazor-apps/issues/new).

Code and sample contributions must be provided under the MIT license. Preserve existing third-party notices.

Start from current `main` on a focused `feat/`, `fix/`, or `chore/` branch (or a fork for external contributions). Follow [AGENTS.md](AGENTS.md) for the architecture map and [README.md](README.md) for setup and validation. Keep unrelated cleanup separate and preserve user changes already in the working tree.

Add tests that demonstrate changed behavior. SDK/editor changes need runtime validation because reflection and AOT can fail without a compile error. Use synthetic fixtures; do not connect automated tests to customer datasets.

Before requesting review, run the Release build, regression tests, AOT publish, and published browser smoke tests. Explain any unresolved failures or warnings. Use conventional commit subjects such as `fix: preserve nullable editor values`.

PRs target `main`. Put the Trello task URL at the top of the description when available, followed by the problem, resulting behavior, validation performed, and remaining limitations. Include screenshots for visual changes. Open a draft when the work needs discussion or validation is incomplete.

Merging to main publishes the application after validation passes. Do not edit `gh-pages` manually, bump an unrelated version, or include generated build output in the PR.
