trap {
    Write-Error $_
    Exit 1
}
# Assumes $PWD is the repo root
if ($env:BUILD_SECRET_KEY) {
    dotnet test ./UnitTestProject1/UnitTestProject1.csproj --no-build -c Release -- TestSessionTimeout=3600000 StopOnFail=True
    Exit $LASTEXITCODE
}
else {
    Write-Warning "BUILD_SECRET_KEY is not available. Will not execute tests."
    return 0
}
