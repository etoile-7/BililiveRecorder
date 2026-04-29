Set-PSDebug -Trace 1
Push-Location "$PSScriptRoot/source"
git apply --reverse --check ../patches/fmp4-record-mode.patch *> $null
if ($LASTEXITCODE -eq 0) {
    Write-Host "WebUI fMP4 patch already applied"
} else {
    git apply ../patches/fmp4-record-mode.patch
}
$env:BASE_URL = "./"
$env:VITE_EMBEDDED_BUILD = "true"
(npm ci) -and (npx vite build)
Remove-Item -Recurse -ErrorAction SilentlyContinue ../../BililiveRecorder.Web/embeded/ui
Copy-Item -Recurse dist ../../BililiveRecorder.Web/embeded/ui
Pop-Location
