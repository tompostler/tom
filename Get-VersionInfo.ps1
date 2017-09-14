# Tom Postler, 2017-09-05
# Hit the apis to get version info. Returns all five parts separately and in order.

param(
    [Parameter(Mandatory=$true)][string]$AssemblyName
)

$urlbase = "https://unlimitedinf-apis.azurewebsites.net";
$VersionName = "tom.exe_$AssemblyName";
$CountName = "tom.exe_$($AssemblyName)";
$count = "0";

# If we have the token defined, try to update the count. Swallow all errors.
if ($env:UnlimitedinfApiToken -ne $null) {
	$url = "$urlbase/versioning/counts";
	Write-Host "Incrementing count information at $($url.SubString($urlbase.Length))";
	$request = [System.Net.WebRequest]::CreateHttp($url);
	$request.ContentType = "application/json";
	$request.Method = "PUT";
	$request.Headers.Add("Authorization", "Token $env:UnlimitedinfApiToken");
	$reqStream = New-Object System.IO.StreamWriter $request.GetRequestStream();
	$reqStream.Write(('{{"username":"unlimitedinf","name":"{0}","type":"inc"}}' -f $CountName));
	$reqStream.Close();
	$response = $request.GetResponse();

	# Check for failure
	if (!($response.StatusCode -eq [System.Net.HttpStatusCode]::OK)) {
		Write-Host "Request to increase count failed with $($response.StatusCode)";
	}
	else {
		# Get the count
		$count = (New-Object System.IO.StreamReader $response.GetResponseStream()).ReadToEnd() | ConvertFrom-Json;
		$count = $count.count.ToString();
		Write-Host "Count $CountName was updated to $count";
	}
}
# Else just get the count
else {
	$url = "$urlbase/versioning/counts?username=unlimitedinf&countName=$CountName";
	Write-Host "Retrieving current count information from $($url.SubString($urlbase.Length))";
	$request = [System.Net.WebRequest]::CreateHttp($url);
	$request.Method = "GET";
	$response = $request.GetResponse();

	# Check for failure
	if (!($response.StatusCode -eq [System.Net.HttpStatusCode]::OK)) {
		Write-Host "Request for count failed with $($response.StatusCode)";
		exit 1;
	}

	# Get the version
	$count = (New-Object System.IO.StreamReader $response.GetResponseStream()).ReadToEnd() | ConvertFrom-Json;
	$count = $count.count.ToString();
	Write-Host "Count $CountName was getted as $count";
}

$url = "$urlbase/versioning/versions?username=unlimitedinf&versionName=$VersionName";
Write-Host "Retrieving current version information from $($url.SubString($urlbase.Length))";
$request = [System.Net.WebRequest]::CreateHttp($url);
$request.Method = "GET";
$response = $request.GetResponse();

# Check for failure
if (!($response.StatusCode -eq [System.Net.HttpStatusCode]::OK)) {
    Write-Host "Request for version failed with $($response.StatusCode)";
    exit 1;
}

# Get the version
$version = (New-Object System.IO.StreamReader $response.GetResponseStream()).ReadToEnd() | ConvertFrom-Json;
$version = $version.version;
Write-Host "Version $VersionName was getted as $version";

# Version info vars
$Major, $Minor, $Patch, $Prerelease, $Build = @("0","0","0",$count,"");

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
