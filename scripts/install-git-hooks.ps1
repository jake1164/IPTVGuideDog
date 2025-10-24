Param()

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -Path $repoRoot

$result = git config core.hooksPath ".githooks"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to configure git hooks path. Ensure git is installed and you're inside the repository."
    exit 1
}

Write-Host "Git hooks configured to use '.githooks'." -ForegroundColor Green
