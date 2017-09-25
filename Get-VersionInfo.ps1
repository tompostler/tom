# Tom Postler, 2017-09-05
# Hit the apis to get version info. Returns all five parts separately and in order.

param(
    [Parameter(Mandatory=$true)][string]$AssemblyName
)

function GetResponseGobble {
    # In the case where we get an exception with a specific message,
    #   we're probably offline and should just gobble the exception instead.
    param (
        [Parameter(Mandatory=$true)][System.Net.WebRequest]$Request,
        [Parameter(Mandatory=$true)][String]$Default
    )

    try
    {
        $response = $Request.GetResponse();
        # Check for failure
	    if (!($response.StatusCode -eq [System.Net.HttpStatusCode]::OK)) {
		    Write-Host "Request failed with $($response.StatusCode)";
		    exit 1;
	    }
        return (New-Object System.IO.StreamReader $response.GetResponseStream()).ReadToEnd();
    }
    catch [System.Net.WebException]
    {
        # If the message does not contain the following, then we have a different problem.
        if (-not $_.Exception.Message.StartsWith("The remote name could not be resolved: '")) {
            throw;
        }
        Write-Host -ForegroundColor Yellow "Could not talk to internet. Using default values..."
        return $Default;
    }
}

$urlbase = "https://unlimitedinf-apis.azurewebsites.net";
$VersionName = "tom.exe_$AssemblyName";
$CountName = "tom.exe_$($AssemblyName)";
$count = "0";
$DefaultCount = [math]::Floor([timespan]::new([datetime]::Now.Ticks - [datetime]::Now.Date.Ticks).TotalMinutes);
$DefaultVersion = "{0}.{1}.{2}" -f 0, [datetime]::Now.Year.ToString().Substring(2), [datetime]::Now.DayOfYear;

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
	$result = GetResponseGobble -Request $request -Default ('{{"count":{0}}}' -f $DefaultCount);

	# Get the count
	$count = $result | ConvertFrom-Json;
	$count = $count.count.ToString();
	Write-Host "Count $CountName was updated to $count";
}
# Else just get the count
else {
	$url = "$urlbase/versioning/counts?username=unlimitedinf&countName=$CountName";
	Write-Host "Retrieving current count information from $($url.SubString($urlbase.Length))";
	$request = [System.Net.WebRequest]::CreateHttp($url);
	$request.Method = "GET";
	$result = GetResponseGobble -Request $request -Default ('{{"count":{0}}}' -f $DefaultCount);

	# Get the version
	$count = $result | ConvertFrom-Json;
	$count = $count.count.ToString();
	Write-Host "Count $CountName was getted as $count";
}

$url = "$urlbase/versioning/versions?username=unlimitedinf&versionName=$VersionName";
Write-Host "Retrieving current version information from $($url.SubString($urlbase.Length))";
$request = [System.Net.WebRequest]::CreateHttp($url);
$request.Method = "GET";
$result = GetResponseGobble -Request $request -Default ('{{"version":"{0}"}}' -f $DefaultVersion);

# Get the version
$version = $result | ConvertFrom-Json;
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
