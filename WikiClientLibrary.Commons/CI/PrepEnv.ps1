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
}
elseif ($IsWindows) {
    # SHFB
    if ($SHFB) {
        Write-Host "Downloading SHFB."
        Invoke-WebRequest "https://github.com/EWSoftware/SHFB/releases/download/v2022.1.22.0/SHFBInstaller_v2022.1.22.0.zip" -OutFile SHFBInstaller.zip
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
