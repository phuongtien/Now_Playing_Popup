param(
    [string]$Version,                                     # new version, e.g. 1.0.1
    [string]$ManifestFile = "releases\manifest.json"       # manifest file
)

# 1. Find newest setup file in installer_build\Output
$setupDir = "installer_build\Output"
$setupFile = Get-ChildItem -Path $setupDir -Filter "NowPlayingPopupSetup.exe" | 
             Sort-Object LastWriteTime -Descending | 
             Select-Object -First 1

if (-not $setupFile) {
    Write-Error "Could not find NowPlayingPopupSetup.exe in $setupDir"
    exit 1
}

Write-Host "Using setup file: $($setupFile.FullName)"

# 2. Compute SHA256
$sha256 = (Get-FileHash -Path $setupFile.FullName -Algorithm SHA256).Hash.ToUpper()

# 3. Load old manifest
Write-Host "📂 Reading manifest: $ManifestFile"
$manifest = Get-Content $ManifestFile | ConvertFrom-Json

# 4. Update manifest
$manifest.version = $Version
$manifest.url = "https://github.com/phuongtien/Now_Playing_Popup_clean/releases/download/v$Version/NowPlayingPopupSetup.exe"
$manifest.sha256 = $sha256
$manifest.publishedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")

# (optional: update notes)
# $manifest.notes = "Update $Version: bug fixes and improvements"

# 5. Save manifest
$manifest | ConvertTo-Json -Depth 3 | Out-File $ManifestFile -Encoding utf8

Write-Host "Manifest has been updated:"
Write-Host "Version : $Version"
Write-Host "SHA256  : $sha256"
Write-Host "URL     : $($manifest.url)"
