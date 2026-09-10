[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^https://github\.com/[^/]+/[^/]+/pull/[0-9]+/?$')]
    [string]$PrUrl,
    [ValidateRange(1, 1000)]
    [int]$MaxPages = 50
)

$ErrorActionPreference = 'Stop'
$parts = ([uri]$PrUrl).AbsolutePath.Trim('/').Split('/')
$owner, $repo, $number = $parts[0], $parts[1], [int]$parts[3]
$warnings = [System.Collections.Generic.List[string]]::new()

function Invoke-Query([string]$Query, [hashtable]$Variables) {
    $arguments = @('api', 'graphql', '-f', "query=$Query")
    foreach ($entry in $Variables.GetEnumerator()) {
        if ($null -ne $entry.Value) {
            $arguments += @('-F', "$($entry.Key)=$($entry.Value)")
        }
    }
    $response = & gh @arguments
    if ($LASTEXITCODE -ne 0) { throw 'GitHub GraphQL request failed. Check gh authentication and PR access.' }
    $result = ($response -join "`n") | ConvertFrom-Json -Depth 100
    if ($result.errors) { throw ($result.errors.message -join '; ') }
    return $result.data
}

$variables = @{ owner = $owner; repo = $repo; number = $number }
$metadata = Invoke-Query @'
query($owner:String!, $repo:String!, $number:Int!) {
  repository(owner:$owner, name:$repo) {
    pullRequest(number:$number) { number title url state headRefName baseRefName }
  }
}
'@ $variables
if (-not $metadata.repository.pullRequest) { throw 'PR not found or not accessible.' }

function Read-PrConnection([string]$Field, [string]$Selection) {
    $query = @'
query($owner:String!, $repo:String!, $number:Int!, $after:String) {
  repository(owner:$owner, name:$repo) {
    pullRequest(number:$number) {
      FIELD(first:100, after:$after) {
        nodes { SELECTION }
        pageInfo { hasNextPage endCursor }
      }
    }
  }
}
'@.Replace('FIELD', $Field).Replace('SELECTION', $Selection)
    $cursor = $null
    for ($page = 1; $page -le $MaxPages; $page++) {
        $data = Invoke-Query $query ($variables + @{ after = $cursor })
        $connection = $data.repository.pullRequest.$Field
        if (-not $connection) { throw "Missing $Field connection in GitHub response." }
        foreach ($item in $connection.nodes) { $item }
        if (-not $connection.pageInfo.hasNextPage) { return }
        $cursor = $connection.pageInfo.endCursor
        if (-not $cursor) { throw "Missing pagination cursor for $Field." }
    }
    $warnings.Add("$Field reached MaxPages=$MaxPages; output is partial.")
}

$commentFields = 'author { login } body createdAt url'
$comments = @(Read-PrConnection 'comments' $commentFields)
$reviews = @(Read-PrConnection 'reviews' "$commentFields state submittedAt")
$threads = @(Read-PrConnection 'reviewThreads' @"
id isResolved isOutdated path line originalLine diffSide
comments(first:100) { nodes { $commentFields } pageInfo { hasNextPage endCursor } }
"@)
$unresolved = @($threads | Where-Object { -not $_.isResolved })
foreach ($thread in $unresolved) {
    $allComments = [System.Collections.Generic.List[object]]::new()
    foreach ($comment in $thread.comments.nodes) { $allComments.Add($comment) }
    $pageInfo = $thread.comments.pageInfo
    $page = 1
    while ($pageInfo.hasNextPage -and $page -lt $MaxPages) {
        $query = @'
query($id:ID!, $after:String!) {
  node(id:$id) {
    ... on PullRequestReviewThread {
      comments(first:100, after:$after) {
        nodes { author { login } body createdAt url }
        pageInfo { hasNextPage endCursor }
      }
    }
  }
}
'@
        if (-not $pageInfo.endCursor) { throw 'Missing thread comment cursor.' }
        $data = Invoke-Query $query @{ id = $thread.id; after = $pageInfo.endCursor }
        if (-not $data.node.comments) { throw 'Missing thread comments in GitHub response.' }
        foreach ($comment in $data.node.comments.nodes) { $allComments.Add($comment) }
        $pageInfo = $data.node.comments.pageInfo
        $page++
    }
    if ($pageInfo.hasNextPage) { $warnings.Add("Comments on thread $($thread.id) reached MaxPages=$MaxPages; output is partial.") }
    $thread.comments = $allComments.ToArray()
}

[ordered]@{
    pr = $metadata.repository.pullRequest
    unresolvedReviewThreads = $unresolved
    reviews = $reviews
    conversationComments = $comments
    warnings = $warnings.ToArray()
} | ConvertTo-Json -Depth 100
