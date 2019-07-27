trap {
    Write-Error $_
    Exit 1
}
# Assumes $PWD is the repo root
if ($env:BUILD_SECRET_KEY) {
    &"$PSScriptRoot/BuildSecret.ps1" -Restore -SourceRootPath . -SecretPath $PSScriptRoot/Secret.bin -Key $env:BUILD_SECRET_KEY
} else {
    Write-Warning "BUILD_SECRET_KEY is not available. Will build without secret."
}

dotnet build WikiClientLibrary.sln -c CIRelease
Exit $LASTEXITCODE
