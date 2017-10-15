# Tom Postler, 2017-10-14
# Hit the apis to create version info. Also create a count.
# 

param(
    [Parameter(Mandatory=$true)][string]$AssemblyName,
    [Parameter(Mandatory=$true)][string]$Version,
	[Parameter(Mandatory=$true)][string]$Count
)

if ($env:UnlimitedinfApiToken -eq $null) {
    Write-Host -ForegroundColor Red "Envrionment variable UnlimitedinfApiToken is not found!";
	exit 1;
}

$urlbase = "https://unlimitedinf-apis.azurewebsites.net";
$VCName = "tom.exe_$AssemblyName";

# Create the version
$url = "$urlbase/versioning/versions";
Write-Host "Updating version information at $($url.SubString($urlbase.Length))";
Write-Host "  (This may take a few moments if the API has fallen asleep)";
$request = [System.Net.WebRequest]::CreateHttp($url);
$request.ContentType = "application/json";
$request.Method = "POST";
$request.Headers.Add("Authorization", "Token $env:UnlimitedinfApiToken");
$reqStream = New-Object System.IO.StreamWriter $request.GetRequestStream();
$reqStream.Write(('{{"username":"unlimitedinf","name":"{0}","version":"{1}"}}' -f $VCName, $Version));
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
Write-Host "Version $VCName was created at $version";


# Create the count
$url = "$urlbase/versioning/counts";
Write-Host "Resetting count information at $($url.SubString($urlbase.Length))";
$request = [System.Net.WebRequest]::CreateHttp($url);
$request.ContentType = "application/json";
$request.Method = "POST";
$request.Headers.Add("Authorization", "Token $env:UnlimitedinfApiToken");
$reqStream = New-Object System.IO.StreamWriter $request.GetRequestStream();
$reqStream.Write(('{{"username":"unlimitedinf","name":"{0}","count":{1}}}' -f $VCName, $Count));
$reqStream.Close();
$response = $request.GetResponse();

# Check for failure
if (!($response.StatusCode -eq [System.Net.HttpStatusCode]::OK)) {
    Write-Host "Request failed with $($response.StatusCode)";
    exit 1;
}

# Get the count
$count = (New-Object System.IO.StreamReader $response.GetResponseStream()).ReadToEnd() | ConvertFrom-Json;
$count = $count.count;
Write-Host "Count $VCName was created at $count";
