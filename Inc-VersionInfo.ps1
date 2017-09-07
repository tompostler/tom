# Tom Postler, 2017-09-07
# Hit the apis to increment version info.

param(
    [Parameter(Mandatory=$true)][string]$AssemblyName,
    [Parameter(Mandatory=$true)][string]$VersionInc = "patch"
)

if ($env:UnlimitedinfApiToken -eq $null) {
    Write-Host -ForegroundColor Red "Envrionment variable UnlimitedinfApiToken is not found!";
	exit 1;
}

$urlbase = "https://unlimitedinf-apis.azurewebsites.net";
$VersionName = "tom.exe_$AssemblyName";

$url = "$urlbase/versioning/versions";
Write-Host "Updating version information at $($url.SubString($urlbase.Length))";
Write-Host "  (This may take a few moments if the API has fallen asleep)";
$request = [System.Net.WebRequest]::CreateHttp($url);
$request.ContentType = "application/json";
$request.Method = "PUT";
$request.Headers.Add("Authorization", "Token $env:UnlimitedinfApiToken");
$reqStream = New-Object System.IO.StreamWriter $request.GetRequestStream();
$reqStream.Write(('{{"username":"unlimitedinf","name":"{0}","inc":"{1}"}}' -f $VersionName, $VersionInc));
$reqStream.Close();
$response = $request.GetResponse();

# Check for failure
if (!($response.StatusCode -eq [System.Net.HttpStatusCode]::OK)) {
    Write-Host "Request failed with $($response.StatusCode)";
    exit 1;
}

# Get the version
$version = (New-Object System.IO.StreamReader $response.GetResponseStream()).ReadToEnd() | ConvertFrom-Json;
$version = $version.version;
Write-Host "Version $VersionName was updated to $version";
