trap {
    Write-Error $_
    Write-Host $_.ScriptStackTrace
    Exit 1
}
function CheckLastExitCode() {
    if ($LASTEXITCODE) {
        Write-Host (Get-PSCallStack)
        Exit $LASTEXITCODE
    }
}
# Assumes $PWD is the repo root
if ($IsLinux) {
    # Install .NET Core 3.0 preview
    Invoke-WebRequest https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/3.0.1xx/dotnet-sdk-latest-linux-x64.tar.gz -OutFile dotnet.tar.gz
    New-Item /usr/share/dotnet -ItemType Directory -Force
    tar -zxf dotnet.tar.gz -C /usr/share/dotnet
    CheckLastExitCode
    if (-not (Get-Command dotnet))
    {
        New-Item /usr/share/dotnet/dotnet -ItemType SymbolicLink -Value /usr/bin/dotnet | Out-Null
    }
    Write-Host "Installed .NET Core SDKs:"
    dotnet --list-sdks
    CheckLastExitCode
}
