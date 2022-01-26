#! /usr/bin/env pwsh

$additionalArgs = @()

if (![string]::IsNullOrEmpty($env:GITHUB_SHA)) {
    $additionalArgs += "--logger"
    $additionalArgs += "GitHubActions;report-warnings=false"
}

dotnet test --configuration Release --output ./artifacts $additionalArgs
