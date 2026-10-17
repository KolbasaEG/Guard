param(
    [ValidateSet('Basic', 'Postgres', 'Browser', 'Benchmark')]
    [string] $Mode = 'Basic',
    [string] $SettingsPath = 'Guard/appsettings.json',
    [string] $BinaryDirectory,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    # Build first. No application startup, schema update or seeding here.
    foreach ($project in @('Guard.UnitTests', 'Guard.IntegrationTests')) {
        $binary = if ($BinaryDirectory) { Join-Path $BinaryDirectory "$project.dll" }
                  else { "$project/bin/$Configuration/net10.0/$project.dll" }
        if (-not (Test-Path -LiteralPath $binary)) { throw "Сначала соберите решение: отсутствует $binary" }
    }
    $unitBinary = if ($BinaryDirectory) { Join-Path $BinaryDirectory 'Guard.UnitTests.dll' }
                  else { "Guard.UnitTests/bin/$Configuration/net10.0/Guard.UnitTests.dll" }
    & dotnet $unitBinary
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $integrationBinary = if ($BinaryDirectory) { Join-Path $BinaryDirectory 'Guard.IntegrationTests.dll' }
                         else { "Guard.IntegrationTests/bin/$Configuration/net10.0/Guard.IntegrationTests.dll" }
    $runnerArguments = @()
    if ($Mode -ne 'Basic') { $runnerArguments = @("--$($Mode.ToLowerInvariant())", $SettingsPath) }
    & dotnet $integrationBinary @runnerArguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
