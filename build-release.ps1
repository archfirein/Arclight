param([string]$Compiler, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot 'release' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$buildDirectory = Join-Path $PSScriptRoot '.build'
New-Item -ItemType Directory -Force -Path $buildDirectory,$OutputDirectory | Out-Null
if (-not $Compiler) {
    $Compiler = Join-Path $buildDirectory 'compiler/tasks/net472/csc.exe'
    if (-not (Test-Path -LiteralPath $Compiler)) {
        $package = Join-Path $buildDirectory 'compiler.zip'
        Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.8.0/microsoft.net.compilers.toolset.4.8.0.nupkg' -OutFile $package
        Expand-Archive -LiteralPath $package -DestinationPath (Join-Path $buildDirectory 'compiler') -Force
    }
}
$Compiler = [IO.Path]::GetFullPath($Compiler)
Push-Location $PSScriptRoot
try {
    foreach ($architecture in @('x64','x86','arm64')) {
        $stage = Join-Path $buildDirectory $architecture
        New-Item -ItemType Directory -Force -Path $stage | Out-Null
        $application = Join-Path $stage 'ArcLight.exe'
        & $Compiler /nologo /codepage:65001 /deterministic+ /target:winexe "/platform:$architecture" /optimize+ /win32icon:app.ico "/out:$application" /r:System.Windows.Forms.dll /r:System.Drawing.dll Program.cs Settings.cs
        if ($LASTEXITCODE -ne 0) { throw "Application build failed: $architecture" }
        $installer = Join-Path $OutputDirectory "ArcLight-Installer.win-$architecture.exe"
        & $Compiler /nologo /codepage:65001 /deterministic+ /target:winexe "/platform:$architecture" /optimize+ /win32icon:app.ico "/out:$installer" "/resource:$application,ArcLight.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Microsoft.CSharp.dll Installer.cs
        if ($LASTEXITCODE -ne 0) { throw "Installer build failed: $architecture" }
        Copy-Item -LiteralPath README.md -Destination (Join-Path $stage 'README.md')
        Compress-Archive -Path $application,(Join-Path $stage 'README.md') -DestinationPath (Join-Path $OutputDirectory "ArcLight.win-$architecture.zip") -Force
    }
    $sourceFiles = @('Program.cs','Settings.cs','Installer.cs','GenerateIcon.cs','app.ico','README.md','CHANGELOG.md','build-release.ps1','SettingsTests.cs','RecoveryTests.cs','layout-results.txt')
    Compress-Archive -Path $sourceFiles -DestinationPath (Join-Path $OutputDirectory 'Source.code.zip') -Force
    & tar -czf (Join-Path $OutputDirectory 'Source.code.tar.gz') @sourceFiles
    if ($LASTEXITCODE -ne 0) { throw 'Source tar archive failed' }
    $sums = Get-ChildItem -LiteralPath $OutputDirectory -File | Where-Object { $_.Name -match '^(ArcLight.*\.(exe|zip)|Source\.code\.(zip|tar\.gz))$' } | Sort-Object Name | ForEach-Object {
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
    }
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory 'SHA256SUMS.txt'), [string[]]$sums, (New-Object Text.UTF8Encoding($false)))
} finally { Pop-Location }

