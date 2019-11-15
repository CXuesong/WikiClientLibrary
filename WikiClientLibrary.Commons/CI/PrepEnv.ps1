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
    sudo apt install dotnet-sdk-2.1.202 dotnet-sdk-3.0
    CheckLastExitCode
    Write-Host "Installed .NET Core SDKs:"
    dotnet --list-sdks
    CheckLastExitCode
} elseif ($IsWindows) {
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'DotNet-Install.ps1'
    ./DotNet-Install.ps1 -Version 2.1.202
    ./DotNet-Install.ps1 -Version 3.0
    Write-Host "Installed .NET Core SDKs:"
    dotnet --list-sdks
    CheckLastExitCode
} else {
    Write-Error "Invalid Environment."
}
