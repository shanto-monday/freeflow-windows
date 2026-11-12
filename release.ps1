# Publish a new FreeFlow version to GitHub. Every installed copy updates itself within ~6 hours.
#   powershell -ExecutionPolicy Bypass -File release.ps1 -Version 3.1.0 -Notes "What changed"
param([Parameter(Mandatory=$true)][string]$Version, [string]$Notes = "Improvements and fixes.")
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$gh = "C:\Program Files\GitHub CLI\gh.exe"
Set-Location $here
Set-Content -NoNewline -Encoding ASCII "$here\VERSION" $Version
& powershell -ExecutionPolicy Bypass -File "$here\build.ps1"
if ($LASTEXITCODE -ne 0) { throw "build failed" }
# safety: the public exe must not contain the personal key
$bytes = [IO.File]::ReadAllBytes("$here\dist\FreeFlow.exe")
$needle = [Text.Encoding]::Unicode.GetBytes("gsk_")
$text = [Text.Encoding]::Unicode.GetString($bytes)
if ($text.Contains("gsk_")) { throw "STOP: public exe contains a Groq key" }
git add -A
git commit -m "FreeFlow $Version" -m $Notes
git tag "v$Version"
git push origin HEAD --tags
& $gh release create "v$Version" "$here\dist\FreeFlow.exe" "$here\dist\FreeFlow-Windows.zip" --title "FreeFlow $Version" --notes $Notes
Write-Host "Released v$Version"
