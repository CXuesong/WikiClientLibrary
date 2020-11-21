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

function checkDotNetSdkVersions {
    [CmdletBinding()] param([string] $Channel)

    [string[]]$sdks = dotnet --list-sdks

    $matchingSdks = $sdks | ? { $_ -match "^$Channel\." }

    Write-Host "Installed .NET Core SDK $Channel.x:"
    Write-Host $matchingSdks

    if ($matchingSdks) {
        return $true
    }
    else {
        Write-Error "No matching SDK installed for channel: $Channel."
        return $false
    }
}

# Assumes $PWD is the repo root
if ($IsLinux) {
    if ($SHFB) {
        Write-Error "SHFB is not supported on Linux."
    }
    if (-not (checkDotNetSdkVersions -Channel 2 -ErrorAction Continue)) {
        sudo apt install dotnet-sdk-2.1
        CheckLastExitCode
        checkDotNetSdkVersions -Channel 2
    }
    if (-not (checkDotNetSdkVersions -Channel 3 -ErrorAction Continue)) {
        sudo apt install dotnet-sdk-3.1
        CheckLastExitCode
        checkDotNetSdkVersions -Channel 3
    }
}
elseif ($IsWindows) {
    # dotnet
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'DotNet-Install.ps1'
    if (-not (checkDotNetSdkVersions -Channel 2 -ErrorAction Continue)) {
        ./DotNet-Install.ps1 -Version 2.1.23
        checkDotNetSdkVersions -Channel 2
    }
    if (-not (checkDotNetSdkVersions -Channel 3 -ErrorAction Continue)) {
        ./DotNet-Install.ps1 -Version 3.1.10
        checkDotNetSdkVersions -Channel 3
    }

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
