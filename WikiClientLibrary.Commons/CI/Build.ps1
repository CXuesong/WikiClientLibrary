trap {
    Write-Error $_
    Exit 1
}
# Assumes $PWD is the repo root
dotnet build WikiClientLibrary.sln -c CIRelease
Exit $LASTEXITCODE
