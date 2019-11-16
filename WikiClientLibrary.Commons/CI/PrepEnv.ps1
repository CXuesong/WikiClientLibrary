param (
    [switch]
    $SHFB = $false
)

$ErrorActionPreference = "Stop"

trap {
    Write-Error $_
    Write-Host $_.ScriptStackTrace
    Exit 1
}
function CheckLastExitCode($ExitCode = $LASTEXITCODE) {
    if ($ExitCode) {
        Write-Host (Get-PSCallStack)
        Exit $ExitCode
    }
}

# Assumes $PWD is the repo root
if ($IsLinux) {
    if ($SHFB) {
        Write-Error "SHFB is not supported on Linux."
    }
    sudo apt install dotnet-sdk-2.1.202 dotnet-sdk-3.0
    CheckLastExitCode
    Write-Host "Installed .NET Core SDKs:"
    dotnet --list-sdks
    CheckLastExitCode
}
elseif ($IsWindows) {
    # dotnet
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'DotNet-Install.ps1'
    ./DotNet-Install.ps1 -Version 2.1.202
    ./DotNet-Install.ps1 -Version 3.0.100
    Write-Host "Installed .NET Core SDKs:"
    dotnet --list-sdks
    CheckLastExitCode

    # SHFB
    if ($SHFB) {
        Write-Host "Downloading SHFB."
        Invoke-WebRequest "https://github.com/EWSoftware/SHFB/releases/download/v2019.9.15.0/SHFBInstaller_v2019.9.15.0.zip" -OutFile SHFBInstaller.zip
        New-Item -ItemType Directory SHFBInstaller | Out-Null
        Expand-Archive SHFBInstaller.zip SHFBInstaller
        Write-Host "Downloading SHFB."
        $proc = Start-Process -PassThru -Wait -FilePath ./SHFBInstaller/InstallResources/SandcastleHelpFileBuilder.msi -ArgumentList /quiet, /lwe, SHFBInstall.log
        Get-Content SHFBInstall.log
        CheckLastExitCode ($proc.ExitCode)
    }
}
else {
    Write-Error "Invalid Environment."
}
