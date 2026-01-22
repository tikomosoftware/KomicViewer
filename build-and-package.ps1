# KomicViewer デュアルリリースビルドスクリプト
# 2つのビルドを作成: フレームワーク依存版（軽量）と自己完結型版（単一EXE）

param(
    [string]$Version = "1.0.3",
    [switch]$Clean
)

Write-Host "KomicViewer v$Version Dual Build and Package Script" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green
Write-Host ""

# 変数定義
$ProjectFile = "KomicViewer.csproj"
$DistDir = "dist"
$TempFrameworkDir = "$DistDir\temp_framework"
$TempStandaloneDir = "$DistDir\temp_standalone"
$FrameworkZipFile = "$DistDir\KomicViewer-v$Version-framework-dependent-release.zip"
$StandaloneZipFile = "$DistDir\KomicViewer-v$Version-standalone-release.zip"

# ビルド開始時刻を記録
$BuildStartTime = Get-Date

if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean
    if (Test-Path "bin") { Remove-Item "bin" -Recurse -Force }
    if (Test-Path "obj") { Remove-Item "obj" -Recurse -Force }
}

# Create distribution folder
if (Test-Path $DistDir) { 
    Remove-Item $DistDir -Recurse -Force 
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

# ========================================
# フレームワーク依存ビルド（軽量版）
# ========================================
Write-Host "Building Framework-Dependent (Lightweight)..." -ForegroundColor Yellow
$frameworkBuildSuccess = $false
try {
    New-Item -ItemType Directory -Path $TempFrameworkDir -Force | Out-Null
    
    Write-Host "  Building..." -ForegroundColor Gray
    dotnet build $ProjectFile --configuration Release
    
    if ($LASTEXITCODE -eq 0) {
        $sourceDir = "bin/Release/net9.0-windows"
        
        # Copy necessary files
        Copy-Item "$sourceDir/KomicViewer.exe" $TempFrameworkDir
        Copy-Item "$sourceDir/KomicViewer.dll" $TempFrameworkDir
        Copy-Item "$sourceDir/KomicViewer.deps.json" $TempFrameworkDir
        Copy-Item "$sourceDir/KomicViewer.runtimeconfig.json" $TempFrameworkDir
        Copy-Item "$sourceDir/SharpCompress.dll" $TempFrameworkDir
        Copy-Item "$sourceDir/SkiaSharp.dll" $TempFrameworkDir
        Copy-Item "README.md" $TempFrameworkDir
        
        # Copy only win-x64 runtime files
        if (Test-Path "$sourceDir/runtimes/win-x64") {
            New-Item -ItemType Directory -Path "$TempFrameworkDir/runtimes/win-x64/native" -Force | Out-Null
            Copy-Item "$sourceDir/runtimes/win-x64/native/*" "$TempFrameworkDir/runtimes/win-x64/native/" -ErrorAction SilentlyContinue
        }
        
        # Create ZIP
        Compress-Archive -Path "$TempFrameworkDir/*" -DestinationPath $FrameworkZipFile -Force
        Write-Host "  ✓ Framework-dependent build completed" -ForegroundColor Green
        $frameworkBuildSuccess = $true
    } else {
        throw "Build failed!"
    }
} catch {
    Write-Host "  ✗ Framework-dependent build failed: $($_.Exception.Message)" -ForegroundColor Red
}

# ========================================
# 自己完結型ビルド（単一EXE版）
# ========================================
Write-Host ""
Write-Host "Building Self-Contained (Single EXE)..." -ForegroundColor Yellow
$standaloneBuildSuccess = $false
try {
    New-Item -ItemType Directory -Path $TempStandaloneDir -Force | Out-Null
    
    Write-Host "  Publishing..." -ForegroundColor Gray
    dotnet publish $ProjectFile `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -o $TempStandaloneDir
    
    if ($LASTEXITCODE -eq 0) {
        # Copy README
        Copy-Item "README.md" $TempStandaloneDir
        
        # Create ZIP
        Compress-Archive -Path "$TempStandaloneDir/*" -DestinationPath $StandaloneZipFile -Force
        Write-Host "  ✓ Self-contained build completed" -ForegroundColor Green
        $standaloneBuildSuccess = $true
    } else {
        throw "Publish failed!"
    }
} catch {
    Write-Host "  ✗ Self-contained build failed: $($_.Exception.Message)" -ForegroundColor Red
}

# 両方のビルドが失敗した場合はエラー終了
if (-not $frameworkBuildSuccess -and -not $standaloneBuildSuccess) {
    Write-Host "Both builds failed!" -ForegroundColor Red
    exit 1
}

# Cleanup temporary directories
Write-Host ""
Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
if (Test-Path $TempFrameworkDir) {
    Remove-Item -Path $TempFrameworkDir -Recurse -Force
}
if (Test-Path $TempStandaloneDir) {
    Remove-Item -Path $TempStandaloneDir -Recurse -Force
}
Write-Host "Cleanup completed" -ForegroundColor Green
Write-Host ""

# ビルド結果のサマリー表示
$BuildEndTime = Get-Date
$BuildDuration = $BuildEndTime - $BuildStartTime
$BuildTimeSeconds = [math]::Round($BuildDuration.TotalSeconds, 1)

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host ""

# フレームワーク依存ビルドの情報
if ($frameworkBuildSuccess -and (Test-Path $FrameworkZipFile)) {
    $frameworkZipInfo = Get-Item $FrameworkZipFile
    $frameworkZipHash = Get-FileHash $FrameworkZipFile -Algorithm SHA256
    
    Write-Host "📦 Framework-Dependent Build (Lightweight):" -ForegroundColor Cyan
    Write-Host "   File: $($frameworkZipInfo.Name)" -ForegroundColor White
    Write-Host "   Size: $([math]::Round($frameworkZipInfo.Length / 1MB, 2)) MB" -ForegroundColor White
    Write-Host "   SHA256: $($frameworkZipHash.Hash)" -ForegroundColor Gray
    Write-Host "   ⚠ Requires .NET 9.0 Desktop Runtime" -ForegroundColor Yellow
    Write-Host ""
}

# 自己完結型ビルドの情報
if ($standaloneBuildSuccess -and (Test-Path $StandaloneZipFile)) {
    $standaloneZipInfo = Get-Item $StandaloneZipFile
    $standaloneZipHash = Get-FileHash $StandaloneZipFile -Algorithm SHA256
    
    Write-Host "📦 Self-Contained Build (Single EXE):" -ForegroundColor Cyan
    Write-Host "   File: $($standaloneZipInfo.Name)" -ForegroundColor White
    Write-Host "   Size: $([math]::Round($standaloneZipInfo.Length / 1MB, 2)) MB" -ForegroundColor White
    Write-Host "   SHA256: $($standaloneZipHash.Hash)" -ForegroundColor Gray
    Write-Host "   ✓ No .NET Runtime installation required" -ForegroundColor Green
    Write-Host ""
}

Write-Host "⏱ Total build time: $BuildTimeSeconds seconds" -ForegroundColor White
Write-Host "Package is located at: $DistDir\" -ForegroundColor White
Write-Host ""