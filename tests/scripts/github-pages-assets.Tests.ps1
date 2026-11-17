param(
    [Parameter(Mandatory)]
    [string]$IndexPath
)

$ErrorActionPreference = 'Stop'
$html = Get-Content -LiteralPath $IndexPath -Raw
$baseTag = [regex]::Match($html, '<base\s+href="([^"]+)"')
if (-not $baseTag.Success -or $baseTag.Index -gt $html.IndexOf('<script')) {
    throw 'The published HTML must declare its base before scripts and asset discovery.'
}

# Resolve the initial HTML without executing its scripts, as the preload scanner does.
$pageUri = [uri]'https://example.github.io/relewise-tools-blazor-apps/?/RequestExplorer/&datasetId=example~and~parentOrigin=https%3A%2F%2Fexample.com'
$baseUri = [uri]::new($pageUri, $baseTag.Groups[1].Value)
$expectedBase = 'https://example.github.io/relewise-tools-blazor-apps/'
if ($baseUri.AbsoluteUri -ne $expectedBase) {
    throw "Published assets would resolve from the wrong base: $baseUri"
}

$assets = [regex]::Matches($html, '<(?:link\b[^>]*\bhref|script\b[^>]*\bsrc)="([^"]+)"')
if ($assets.Count -eq 0) { throw 'No published CSS, scripts, or icons found to validate.' }
foreach ($asset in $assets) {
    $assetUri = [uri]::new($baseUri, $asset.Groups[1].Value)
    if (-not $assetUri.AbsoluteUri.StartsWith($expectedBase, [StringComparison]::Ordinal)) {
        throw "Published asset escapes the GitHub Pages path: $assetUri"
    }
}

Write-Output "GitHub Pages asset tests passed ($($assets.Count) assets resolve under the repository path before scripts execute)."
