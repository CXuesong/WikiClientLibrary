# Generates the packages and documentation for later publication.
# Assumes $PWD is the repo root
trap {
    Write-Error $_
    Write-Host $_.ScriptStackTrace
    Exit 1
}
$ErrorActionPreference = "Stop"
$LASTEXITCODE = 0
$PackageProjects = @(
    "WikiClientLibrary", 
    "WikiClientLibrary.Cargo",
    "WikiClientLibrary.Flow",
    "WikiClientLibrary.Wikia",
    "WikiClientLibrary.Wikibase"
)
$PublishRoot = "./_private/Publish"
$DocumentationRoot = "./DocumentationProject/Help"
$VersionNode = Select-Xml .\WikiClientLibrary.Commons\WikiClientLibrary.Packages.props -XPath //Version
if (-not $VersionNode) {
    throw [System.Exception]"Cannot find <Version> node in the props file."
}
$Version = $VersionNode.Node.InnerText.Trim()
$PublishRoot = New-Item $PublishRoot -ItemType Directory -Force
Write-Host "Package version: $Version"
Write-Host "Publish output path: $PublishRoot"

msbuild WikiClientLibrary.sln -p:Configuration=Release -p:Platform="Any CPU" -v:m
msbuild WikiClientLibrary.sln -p:Configuration=Publish -p:Platform="Any CPU" -v:m

if ($LASTEXITCODE) {
    Exit $LASTEXITCODE
}

# Prepare packages
foreach ($proj in $PackageProjects) {
    $OutDir = Resolve-Path "./$proj/bin/Release/"
    $PackageFile = Get-ChildItem $OutDir -Filter "*.$proj.$Version.nupkg"
    Write-Host $PackageFile
    $DocumentationFile = Resolve-Path "$DocumentationRoot/$proj.xml"
    Write-Host "Load docs from $DocumentationFile"
    $DocsStream = [System.IO.File]::OpenRead($DocumentationFile)
    # Replace XML documentation files.
    try {
        $Archive = [System.IO.Compression.ZipFile]::Open($PackageFile, [System.IO.Compression.ZipArchiveMode]::Update)
        foreach ($entry in $Archive.Entries) {
            $entry = [System.IO.Compression.ZipArchiveEntry]$entry
            if ([string]::Equals($entry.Name, "$proj.xml", [System.StringComparison]::InvariantCultureIgnoreCase)) {
                Write-Host "Replace docs in archive $($entry.FullName)"
                $stream = $entry.Open()
                try {
                    $stream.SetLength($DocsStream.Length)
                    $DocsStream.Position = 0
                    $DocsStream.CopyTo($stream)
                }
                finally {
                    $stream.Close()
                }
            }
        }
    }
    finally {
        $DocsStream.Close()
        $Archive -and $Archive.Dispose() | Out-Null
    }
    # Copy to output folder.
    Copy-Item $PackageFile $PublishRoot -Force
}

# Prepare HTML documentation.
pushd .
try {
    cd $DocumentationRoot
    Write-Host "Commit documentation in $PWD"
    [string]$LastCommitMessage = git log HEAD -n 1 --pretty=%B
    if ($LastCommitMessage.Contains("v$Version.")) {
        # Amend last commit
        Write-Host "Amend last commit."
        git reset --soft HEAD^
    }
    git checkout -- .gitignore
    if ($LASTEXITCODE) {
        Exit $LASTEXITCODE
    }
    git add .
    git commit -m "Publish reference for v$Version."
    if ($LASTEXITCODE) {
        Exit $LASTEXITCODE
    }
}
finally {
    popd
}
