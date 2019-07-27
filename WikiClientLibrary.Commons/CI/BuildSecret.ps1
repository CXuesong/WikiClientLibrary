param (
    [Parameter()]
    [string]
    $SourceRootPath = "../..",

    [Parameter()]
    [string]
    $SecretPath = "./Secret.bin",

    [Parameter()]
    [string]
    $Key,

    # Whether we are restoring encrypted files
    [Parameter()]
    [switch]
    $Restore
)

$ErrorActionPreference = "Stop"

$ArchiveRootFolder = "Root"
$SaltLength = 100
$FileList = @("UnitTestProject1/_private/Credentials.cs")

if ($Restore) {
    $WorkDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    mkdir $WorkDir | Out-Null
    try {
        Write-Host "Work dir: $WorkDir"
        $KeyBytes = [System.Convert]::FromBase64String($Key)
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
else {
    $WorkDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    mkdir $WorkDir | Out-Null
    try {
        Write-Host "Work dir: $WorkDir"
        $ArchiveSourceDir = Join-Path $WorkDir $ArchiveRootFolder
        $ArchiveDir = Join-Path $WorkDir "Archive.zip"
        mkdir $ArchiveSourceDir | Out-Null
        foreach ($FileName in $FileList) {
            $FullPath = Join-Path $SourceRootPath $FileName | Resolve-Path
            Write-Host "Copy: $FullPath"
            $TargetPath = Join-Path $ArchiveSourceDir $FileName
            $TargetFolder = (Join-Path $TargetPath ..)
            mkdir $TargetFolder -Force | Out-Null
            Copy-Item $FullPath $TargetPath
        }
        Compress-Archive $ArchiveSourceDir $ArchiveDir

        $Aes = [System.Security.Cryptography.Aes]::Create()
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
