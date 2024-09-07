using System.Reflection;

namespace Unlimitedinf.Tom
{
    internal static class VersionProvider
    {
        private const string versionFileUrl = "https://unlimitedinf.blob.core.windows.net/tom-version/txt";
        private static readonly FileInfo versionFile = new(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "unlimitedinf",
                "tom",
                "version.txt"));

        public static async Task TryReportIfUpdateIsRequiredAsync()
        {
            // Get current version
            string sourceVersion = Version.TryParse(ThisAssembly.AssemblyInformationalVersion?.Split('-', '+')?.FirstOrDefault(), out Version parsed) ? parsed.ToString(fieldCount: 3) : "0.0.0";

            // Check to see if the file exists and written within the last day, then use it
            if (versionFile.Exists && DateTime.Now.Subtract(versionFile.LastWriteTime).TotalDays < 1)
            {
                using FileStream versionFileStream = versionFile.OpenRead();
                using StreamReader versionFileReader = new(versionFileStream);
                string targetVersion = (await versionFileReader.ReadToEndAsync()).Trim();

                ReportUpdateIfNecessary(sourceVersion, targetVersion, $"{versionFile.FullName} (last written {DateTime.Now.Subtract(versionFile.LastWriteTime).TotalHours:0.0}h ago)");
            }
            else
            {
                // We need to try to fetch a newer version of the file
                versionFile.Directory.Create();
                versionFile.Delete();
                HttpClient client = new();
                try
                {
                    string targetVersion = await client.GetStringAsync(versionFileUrl);

                    using StreamWriter tokenFileWriter = versionFile.CreateText();
                    tokenFileWriter.Write(targetVersion);

                    ReportUpdateIfNecessary(sourceVersion, targetVersion, versionFileUrl);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"tom v{sourceVersion}. Could not determine target version: {ex.Message}\n");
                }
            }
        }

        private static void ReportUpdateIfNecessary(string sourceVersionStr, string targetVersionStr, string suffix)
        {
            // Could not parse version
            if (string.Equals(sourceVersionStr, "0.0.0", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"tom v{sourceVersionStr}. Target version v{targetVersionStr} from {suffix}");
                Console.Error.WriteLine("Could not parse version properly from source assembly.");
            }

            // Running in debug locally
            else if (string.Equals(sourceVersionStr, "1.0.0", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"tom v{sourceVersionStr}. Target version v{targetVersionStr} from {suffix}");
                Console.Error.WriteLine("Version indicates running in local debugging.");
            }

            // Might need to update
            else if (!string.Equals(sourceVersionStr, targetVersionStr, StringComparison.OrdinalIgnoreCase))
            {
                // Try to parse the versions for comparison
                if (Version.TryParse(sourceVersionStr, out Version sourceVersion)
                    && Version.TryParse(targetVersionStr, out Version targetVersion)
                    && sourceVersion >= targetVersion)
                {
                    // We could parse it and the source version is higher than the target
                }
                else
                {
                    Console.Error.WriteLine($"tom v{sourceVersionStr}. Target version v{targetVersionStr} from {suffix}");
                    Console.Error.WriteLine("Update tom as a tool with 'dotnet tool update Unlimitedinf.Tom --global'");
                }
            }
        }
    }
}
