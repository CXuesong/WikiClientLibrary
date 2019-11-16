<#
.SYNOPSIS
Collects built packages as artifacts.
Assumes $PWD is the repo root.
#>
param (
    [string]
    $Configuration = "Release",

    [string]
    $ArtifactPath = "./CollectedArtifacts"
)

trap {
    Write-Error $_
    Write-Host $_.ScriptStackTrace
    Exit 1
}
$ErrorActionPreference = "Stop"
$LASTEXITCODE = 0
$PackageProjects = @(
    "WikiClientLibrary", 
    "WikiClientLibrary.Flow",
    "WikiClientLibrary.Wikia",
    "WikiClientLibrary.Wikibase"
)

$ArtifactRoot = New-Item $ArtifactPath -ItemType Directory

Write-Host "Copy packages."
foreach ($proj in $PackageProjects) {
    $OutDir = Resolve-Path "./$proj/bin/$Configuration/"
    $PackageFile = Get-ChildItem $OutDir -Filter "*.$proj.*.nupkg"
    Copy-Item $PackageFile $ArtifactRoot -Force
}

Write-Host "Collected artifacts:"
Get-ChildItem $ArtifactRoot
