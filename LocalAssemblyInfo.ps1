﻿# Tom Postler, 2017-08-13
# Update the LocalAssemblyInfo.g.cs for a project

param(
    [Parameter(Mandatory=$true)][string]$ProjectDir,
    [Parameter(Mandatory=$true)][string]$AssemblyName,
    [Parameter(Mandatory=$false)][switch]$Increment,
    [Parameter(Mandatory=$false)][string]$VersionInc = "patch"
)

$version = "0.0.0";

# Make sure we have the env var defined
if ($env:UnlimitedinfApiToken -ne $null -and $Increment) {
    # Send the request to inc the build version number
    Write-Host "Updating version information at https://unlimitedinf-apis.azurewebsites.net/versioning/versions";
    Write-Host "  (This may take a few moments if the API has fallen asleep)";
    $request = [System.Net.WebRequest]::CreateHttp("https://unlimitedinf-apis.azurewebsites.net/versioning/versions");
    $request.ContentType = "application/json";
    $request.Method = "PUT";
    $request.Headers.Add("Authorization", "Token $env:UnlimitedinfApiToken");
    $reqStream = New-Object System.IO.StreamWriter $request.GetRequestStream();
    $reqStream.Write('{"username":"unlimitedinf","name":"tom.exe_{0}","inc":"{1}"}' -f $AssemblyName,$VersionInc);
    $reqStream.Close();
    $response = $request.GetResponse();

    # Check for failure
    if (!($response.StatusCode -eq [System.Net.HttpStatusCode]::OK)) {
        Write-Host "Request failed with $($response.StatusCode)";
        exit 1;
    }
    Write-Host "Version information updated. Using:";

    # Get the new version
    $version = (New-Object System.IO.StreamReader $response.GetResponseStream()).ReadToEnd() | ConvertFrom-Json;
    $version = $version.version;
    Write-Host "  $version";
} else {
    if ($env:UnlimitedinfApiToken -eq $null) {
        Write-Host -ForegroundColor Red "Envrionment variable UnlimitedinfApiToken is not found!";
    }
    if (-not $Increment) {
        Write-Host -ForegroundColor Yellow "Increment not requested!";
    }
    Write-Host "Retrieving current version information from https://unlimitedinf-apis.azurewebsites.net/versioning/versions";
    Write-Host "  (This may take a few moments if the API has fallen asleep)";
    $request = [System.Net.WebRequest]::CreateHttp("https://unlimitedinf-apis.azurewebsites.net/versioning/versions?username=unlimitedinf&versionName=tom.exe_$AssemblyName");
    $request.ContentType = "application/json";
    $request.Method = "GET";
    $response = $request.GetResponse();

    # Check for failure
    if (!($response.StatusCode -eq [System.Net.HttpStatusCode]::OK)) {
        Write-Host "Request failed with $($response.StatusCode)";
        exit 1;
    }

    # Get the version
    $version = (New-Object System.IO.StreamReader $response.GetResponseStream()).ReadToEnd() | ConvertFrom-Json;
    $version = $version.version;
    Write-Host "Version for assembly $AssemblyName was getted as $version";
} 

# Version info vars
$Major, $Minor, $Patch, $Prerelease = @(0,0,0,0);

# Remove anything that would disagree with AssemblyVersion
if ($version.Contains("-")) {
    $version = $version.Split("-")[0];
}
if ($version.Contains("+")) {
    $version = $version.Split("+")[0];
}

# Parse out the SemVer
$Major, $Minor, $Patch = $version.Split('.');

# If there's nothing in the Properties dir, then it won't be there...
$dirname = "$ProjectDir\Properties";
if (!(Test-Path $dirname)) {
    New-Item -ItemType Directory $dirname | Out-Null
}

# The file contents
$filename = "$ProjectDir\Properties\LocalAssemblyInfo.g.cs";
@"
//
// This code was generated by a tool. Any changes made manually will be lost the next time this code is regenerated.
//

using System.Reflection;

[assembly: AssemblyTitle("{4}")]
[assembly: AssemblyProduct("{4}")]

[assembly: AssemblyVersion("{0}.{1}.0.0")]
[assembly: AssemblyFileVersion("{0}.{1}.{2}.{3}")]
"@ -f $Major, $Minor, $Patch, $Prerelease, $AssemblyName > $filename;

Write-Host $("Generated local assembly info: {0}.{1}.{2}.{3} in $filename" -f $Major, $Minor, $Patch, $Prerelease);

# Restore CWD
Pop-Location
[Environment]::CurrentDirectory = $PWD
