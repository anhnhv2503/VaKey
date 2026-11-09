<#
.SYNOPSIS
  Đóng gói VaKey: bản publish (đầu vào cho installer), bản portable (zip) và — nếu có Inno Setup — file cài đặt .exe.
.EXAMPLE
  ./build/publish.ps1 -Version 1.0.0                 # self-contained: người dùng không cần cài .NET
  ./build/publish.ps1 -Version 1.0.0 -FrameworkDependent   # nhỏ hơn, cần .NET 10 Desktop Runtime
  ./build/publish.ps1 -Version 1.0.0 -SkipInstaller
#>
param(
    [string]$Version = "1.0.0",
    [switch]$FrameworkDependent,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publishDir = Join-Path $artifacts "publish"

Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $artifacts | Out-Null

$selfContained = if ($FrameworkDependent) { "false" } else { "true" }
$publishArgs = @(
    "publish", (Join-Path $root "src/VaKey.Win/VaKey.Win.csproj"),
    "-c", "Release", "-r", "win-x64", "--self-contained", $selfContained,
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:Version=$Version",
    "-p:DebugType=none",
    "-o", $publishDir
)
# Không bật EnableCompressionInSingleFile: nó giải nén toàn bộ runtime vào RAM (+40 MB).
# Installer (lzma2) và file zip đã tự nén.

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish thất bại" }

# Bản portable: thêm portable.txt để cấu hình nằm trong thư mục Data cạnh exe.
$portableDir = Join-Path $artifacts "portable"
Remove-Item -Recurse -Force $portableDir -ErrorAction SilentlyContinue
Copy-Item -Recurse $publishDir $portableDir
Set-Content (Join-Path $portableDir "portable.txt") "Có file này: VaKey lưu cấu hình trong thư mục Data cạnh VaKey.exe." -Encoding utf8
$zip = Join-Path $artifacts "VaKey-$Version-portable-win-x64.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $zip
Remove-Item -Recurse -Force $portableDir
Write-Host "Portable:  $zip"

if ($SkipInstaller) { return }

$iscc = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
    "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning "Không tìm thấy Inno Setup 6 (winget install JRSoftware.InnoSetup). Bỏ qua bước tạo installer."
    return
}

& $iscc "/DAppVersion=$Version" "/Qp" (Join-Path $root "installer/VaKey.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup thất bại" }
Write-Host "Installer: $(Join-Path $artifacts "VaKey-$Version-setup-win-x64.exe")"
