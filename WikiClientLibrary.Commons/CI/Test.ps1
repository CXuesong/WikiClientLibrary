trap {
    Write-Error $_
    Exit 1
}
# Assumes $PWD is the repo root
dotnet test ./UnitTestProject1/UnitTestProject1.csproj -c Release
Exit $LASTEXITCODE
