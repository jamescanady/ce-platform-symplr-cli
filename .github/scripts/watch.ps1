#!usr/bin/env pwsh

param(
    [Parameter(Mandatory)]
    [string]$WorkflowName
)

# 1. Fetch the latest run ID for the specific workflow
$runId = gh run list --workflow $WorkflowName --limit 1 --json databaseId --jq '.[0].databaseId'

# 2. Watch the run if an ID was successfully found
if ($runId) {
    gh run watch $runId
} else {
    Write-Error "No recent runs found for workflow '$WorkflowName'."
}
