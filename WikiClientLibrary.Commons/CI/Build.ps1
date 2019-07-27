trap {
    Write-Error $_
    Exit 1
}
if ($env:BUILD_SECRET_KEY) {
    &"$PSScriptRoot/BuildSecret.ps1" -Restore -Key $env:BUILD_SECRET_KEY
}
# Assumes $PWD is the repo root
dotnet build WikiClientLibrary.sln -c CIRelease
Exit $LASTEXITCODE
