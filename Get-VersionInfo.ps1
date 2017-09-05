# Tom Postler, 2017-09-05
# Hit the apis to get version info. Returns all five parts separately and in order.

param(
    [Parameter(Mandatory=$true)][string]$VersionName
)

$urlbase = "https://unlimitedinf-apis.azurewebsites.net";

$url = "$urlbase/versioning/versions?username=unlimitedinf&versionName=$VersionName";
Write-Host "Retrieving current version information from $($url.SubString($urlbase.Length))";
Write-Host "  (This may take a few moments if the API has fallen asleep)";
$request = [System.Net.WebRequest]::CreateHttp($url);
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
Write-Host "Version $VersionName was getted as $version";

# Version info vars
$Major, $Minor, $Patch, $Prerelease, $Build = @("0","0","0","","");

# Parse it into parts
# A version can have a prerelease and a build, or just a build, or just a prerelease
if ($version.Contains("-")) {
	$Prerelease = $version.Substring($version.IndexOf("-") + 1);
    $Major, $Minor, $Patch = $version.Split("-")[0].Split('.');
	if ($Prerelease.Contains("+")) {
		$Prerelease, $Build = $Prerelease.Split("+");
	}
}
elseif ($version.Contains("+")) {
	$Build = $version.Substring($version.IndexOf("+") + 1);
    $Major, $Minor, $Patch = $version.Split("+")[0].Split('.');
}
else
{
	$Major, $Minor, $Patch = $version.Split('.');
}

# Give them what they asked for
$Major, $Minor, $Patch, $Prerelease, $Build
