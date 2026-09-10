# Contract tests for pagination and error handling. No network or Git mutations.
$ErrorActionPreference = 'Stop'
$scriptUnderTest = Join-Path $PSScriptRoot '../../.agents/skills/read-pr-comments/scripts/read-pr-comments.ps1'
$global:prFeedbackTestFailure = $false

function global:gh {
    $global:LASTEXITCODE = 0
    if ($global:prFeedbackTestFailure) { return '{"errors":[{"message":"fixture access denied"}]}' }
    $query = ($args | Where-Object { $_ -like 'query=*' }) -join ''
    $after = ($args | Where-Object { $_ -like 'after=*' }) -join ''
    $info = @{ hasNextPage = $false; endCursor = $null }
    $comment = @{ author = @{ login = 'reviewer' }; body = 'Please preserve null'; url = 'https://github.com/o/r/pull/1#comment'; createdAt = '2026-09-10T00:00:00Z' }
    if ($query -like '*node(id:*') {
        return @{ data = @{ node = @{ comments = @{ nodes = @($comment); pageInfo = $info } } } } | ConvertTo-Json -Depth 20
    }
    if ($query -like '*reviewThreads(first:*') {
        $field = 'reviewThreads'
        $nodes = @(@{ id = 'thread1'; isResolved = $false; isOutdated = $true; path = 'file.cs'; line = 2; comments = @{ nodes = @($comment); pageInfo = @{ hasNextPage = $true; endCursor = 'thread-next' } } },
            @{ id = 'resolved'; isResolved = $true; comments = @{ nodes = @(); pageInfo = $info } })
    } elseif ($query -like '*reviews(first:*') {
        $field = 'reviews'
        $nodes = @(@{ state = 'CHANGES_REQUESTED'; body = 'Review summary' })
    } elseif ($query -like '*comments(first:*') {
        $field = 'comments'
        $nodes = @($comment)
        if (-not $after) { $info = @{ hasNextPage = $true; endCursor = 'conversation-next' } }
    } else {
        return '{"data":{"repository":{"pullRequest":{"number":1,"title":"Fixture","url":"https://github.com/o/r/pull/1"}}}}'
    }
    return @{ data = @{ repository = @{ pullRequest = @{ $field = @{ nodes = $nodes; pageInfo = $info } } } } } | ConvertTo-Json -Depth 20
}

try {
    $result = (& $scriptUnderTest -PrUrl 'https://github.com/o/r/pull/1') | ConvertFrom-Json -Depth 100
    if ($result.conversationComments.Count -ne 2) { throw 'Conversation pagination failed.' }
    if ($result.unresolvedReviewThreads.Count -ne 1) { throw 'Resolved threads were not filtered.' }
    if ($result.unresolvedReviewThreads[0].comments.Count -ne 2) { throw 'Nested comment pagination failed.' }
    if (-not $result.unresolvedReviewThreads[0].isOutdated) { throw 'Outdated status lost.' }
    if ($result.reviews[0].state -ne 'CHANGES_REQUESTED') { throw 'Review summary lost.' }
    if ($result.warnings.Count -ne 0) { throw 'Unexpected warning.' }

    $limited = (& $scriptUnderTest -PrUrl 'https://github.com/o/r/pull/1' -MaxPages 1) | ConvertFrom-Json -Depth 100
    if ($limited.warnings.Count -ne 2) { throw 'Truncation was not reported for both connections.' }

    $global:prFeedbackTestFailure = $true
    $rejected = $false
    try { & $scriptUnderTest -PrUrl 'https://github.com/o/r/pull/1' } catch {
        $rejected = $_.Exception.Message -like '*fixture access denied*'
    }
    if (-not $rejected) { throw 'GraphQL errors must fail retrieval.' }
    Write-Output 'PR feedback script tests passed (pagination, filtering, truncation, errors).'
} finally {
    Remove-Item Function:\gh
    Remove-Variable prFeedbackTestFailure -Scope Global
}
