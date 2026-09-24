<#
Download vis-network (minified JS and CSS) into the assets/vis folder.

Usage (PowerShell):
  .\download-vis.ps1 -Version "9.1.2"

This script downloads:
  - https://unpkg.com/vis-network@{Version}/dist/vis-network.min.js
  - https://unpkg.com/vis-network@{Version}/dist/vis-network.min.css

Run this locally to populate the assets/vis directory. The viewer will prefer local files when present.
#>

param(
	[string]$Version = "9.1.2",
	[string]$OutDir = "$PSScriptRoot"
)

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$base = "https://unpkg.com/vis-network@$Version/dist"
$files = @("vis-network.min.js", "vis-network.min.css")

foreach ($f in $files) {
	$url = "$base/$f"
	$out = Join-Path $OutDir $f
	try {
		Write-Host "Downloading $url -> $out"
		Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing -ErrorAction Stop
	}
	catch {
		Write-Error "Failed to download $url : $_"
	}
}

Write-Host "Done. Place files under assets/vis if not already." -ForegroundColor Green
