param (
    [Parameter(ParameterSetName = "Build")]
    [Parameter(ParameterSetName = "Restore")]
    [Parameter(ParameterSetName = "Clear")]
    [string]
    $SourceRootPath = "../..",

    [Parameter(ParameterSetName = "Build")]
    [Parameter(ParameterSetName = "Restore")]
    [string]
    $SecretPath = "./Secret.bin",

    [Parameter(ParameterSetName = "Build")]
    [Parameter(ParameterSetName = "Restore")]
    [string]
    $Key,

    # Whether to keep the auto-generated key in current PSSession,
    # and load it automatically if $Key is not specified explicitly next time.
    [Parameter(ParameterSetName = "Build")]
    [Parameter(ParameterSetName = "Restore")]
    [switch]
    $CacheAutoKey,

    # Whether we are restoring encrypted files
    [Parameter(ParameterSetName = "Restore", Mandatory = $true)]
    [switch]
    $Restore,

    [Parameter(ParameterSetName = "Clear", Mandatory = $true)]
    [switch]
    $Clear
)

$ErrorActionPreference = "Stop"

$ArchiveRootFolder = "Root"
$SaltLength = 100
$FileList = @("UnitTestProject1/_private/Credentials.cs")

$KeyBytes = if ($Key) {
    [System.Convert]::FromBase64String($Key)
}
elseif ($CacheAutoKey) {
    Write-Host "Use cached key."
    [System.Convert]::FromBase64String($_WCL_CI_Secret_LastKey)
}

if ($Restore) {
    $WorkDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    New-Item $WorkDir -ItemType Directory | Out-Null
    try {
        Write-Host "Work dir: $WorkDir"
        $Content = Get-Content $SecretPath -AsByteStream -Raw

        $Aes = [System.Security.Cryptography.Aes]::Create()
        try {
            $Aes.Key = $KeyBytes
            $Decryptor = $Aes.CreateDecryptor()
            $Decrypted = $Decryptor.TransformFinalBlock($Content, 0, $Content.Length)
            $Decryptor.Dispose()
            $Decrypted = $Decrypted[$SaltLength..($Decrypted.Length - 1)]

            $ArchiveDir = Join-Path $WorkDir "Archive.zip"
            Set-Content $ArchiveDir $Decrypted -AsByteStream
            Expand-Archive $ArchiveDir $WorkDir | Out-Null
        }
        finally {
            $Aes.Dispose()
        }
        $ArchiveSourceDir = Join-Path $WorkDir $ArchiveRootFolder | Resolve-Path
        Copy-Item $ArchiveSourceDir/* $SourceRootPath -Recurse -Force
        Write-Host "Build secret restored."
    }
    finally {
        Remove-Item $WorkDir -Recurse -Force
        Write-Host "Work dir deleted."
    }
}
elseif ($Clear) {
    foreach ($FileName in $FileList) {
        $FullPath = Join-Path $SourceRootPath $FileName | Resolve-Path
        Remove-Item $FullPath -Force | Out-Null
    }
    Write-Host "Build secret cleared."
}
else {
    $WorkDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    New-Item $WorkDir -ItemType Directory | Out-Null
    try {
        Write-Host "Work dir: $WorkDir"
        $ArchiveSourceDir = Join-Path $WorkDir $ArchiveRootFolder
        $ArchiveDir = Join-Path $WorkDir "Archive.zip"
        New-Item $ArchiveSourceDir -ItemType Directory | Out-Null
        foreach ($FileName in $FileList) {
            $FullPath = Join-Path $SourceRootPath $FileName | Resolve-Path
            Write-Host "Copy: $FullPath"
            $TargetPath = Join-Path $ArchiveSourceDir $FileName
            $TargetFolder = (Join-Path $TargetPath ..)
            New-Item $TargetFolder -ItemType Directory -Force | Out-Null
            Copy-Item $FullPath $TargetPath
        }
        Compress-Archive $ArchiveSourceDir $ArchiveDir

        $Aes = [System.Security.Cryptography.Aes]::Create()
        if ($KeyBytes) {
            $Aes.Key = $KeyBytes
        }
        elseif ($CacheAutoKey) {
            Set-Variable -Name _WCL_CI_Secret_LastKey -Value ([System.Convert]::ToBase64String($Aes.Key)) -Visibility Private -Scope global
            Write-Host "Cached auto-generated key."
        }
        try {
            Write-Host "Encryption key:" ([System.Convert]::ToBase64String($Aes.Key))
            $Content = Get-Content $ArchiveDir -AsByteStream -Raw

            $Rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
            $Salt = [byte[]]::new($SaltLength)
            $Rng.GetBytes($Salt)
            $Rng.Dispose()

            $Encryptor = $Aes.CreateEncryptor()
            $SaltedContent = $Salt + $Content
            $Encrypted = $Encryptor.TransformFinalBlock($SaltedContent, 0, $SaltedContent.Length)
            $Encryptor.Dispose()
        }
        finally {
            $aes.Dispose()
        }

        Set-Content $SecretPath $Encrypted -AsByteStream
    }
    finally {
        Remove-Item $WorkDir -Recurse -Force
        Write-Host "Work dir deleted."
    }
}
