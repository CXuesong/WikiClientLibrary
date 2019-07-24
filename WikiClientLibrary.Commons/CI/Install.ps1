trap {
    Write-Error $_
    Exit 1
}
# Assumes $PWD is the repo root
Exit $LASTEXITCODE
