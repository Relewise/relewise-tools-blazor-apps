# Browser assets

NuGet dependencies are declared in the project files. Two additional browser dependencies are maintained explicitly:

| Asset | Version | Source | License |
| --- | --- | --- | --- |
| Bundled Bootstrap CSS and source map | 5.3.8 | `https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css` and `.css.map` | MIT; copyright notice retained in CSS |
| Bundled Popper UMD script and source map | 2.11.8 | `https://unpkg.com/@popperjs/core@2.11.8/dist/umd/popper.min.js` and `.js.map` | MIT; copyright notice retained in JS |

Update each asset and its source map together, using an exact version. Popper is served locally from `wwwroot/js` so application startup does not depend on a third-party CDN. Check forms, navigation, dropdowns, and documentation tooltips after updates; the Blazor Popper package and JavaScript library must remain compatible.

The GitHub Pages redirect code in `wwwroot/index.html` and `404.html` originates from [spa-github-pages](https://github.com/rafgraph/spa-github-pages), under MIT. Preserve its attribution and path-prefix behavior.
