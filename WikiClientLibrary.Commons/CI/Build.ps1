param (
    [string]
    $Configuration = "CIRelease"
)
trap {
    Write-Error $_
    Write-Host $_.ScriptStackTrace
    Exit 1
}
# Assumes $PWD is the repo root
if ($env:BUILD_SECRET_KEY) {
    &"$PSScriptRoot/BuildSecret.ps1" -Restore -SourceRootPath . -SecretPath $PSScriptRoot/Secret.bin -Key $env:BUILD_SECRET_KEY
    $env:BUILD_SECRET_KEY = "_DUMMY_"
}
else {
    Write-Warning "BUILD_SECRET_KEY is not available. Will build without secret."
}

dotnet build WikiClientLibrary.sln -c $Configuration

$BuildResult = $LASTEXITCODE
if ($env:BUILD_SECRET_KEY) {
    &"$PSScriptRoot/BuildSecret.ps1" -Clear -SourceRootPath .
}

Exit $BuildResult
