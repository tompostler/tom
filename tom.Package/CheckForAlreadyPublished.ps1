# Tom Postler, 2017-08-13
# Check if the tom package is already published.

# Set CWD to script location
Push-Location $PSScriptRoot
[Environment]::CurrentDirectory = $PWD

# Get the version from the xml
[xml]$packageXml = Get-Content ".\tom.xml";
$version = $packageXml.Project.PropertyGroup.Version
Write-Host "Found version '$version' in tom.xml";

# Get the nuget package repository
$pkgNuGet_Core = "NuGet.Core." + ([xml](Get-Content ".\packages.config")).packages.package.version
[Reflection.Assembly]::LoadFile((Resolve-Path "..\packages\$pkgNuGet_Core\lib\net40-Client\NuGet.Core.dll").Path) | Out-Null;
$repo = [NuGet.PackageRepositoryFactory]::Default.CreateRepository("https://packages.nuget.org/api/v2");

# Check if current version is already published
if (($repo.FindPackagesById("Unlimitedinf.Tom") | ? {$_.Version -eq $version} | Measure-Object).Count -eq 1) {
    Write-Error "Nuget package Unlimitedinf.Tom.$version already published."
} else {
    Write-Host "Nuget package Unlimitedinf.Tom.$version not found on nuget.org";
}

# Restore CWD
Pop-Location
[Environment]::CurrentDirectory = $PWD
