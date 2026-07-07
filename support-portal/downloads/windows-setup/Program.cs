using System.Diagnostics;
using System.Net;
using System.Text;

const string clientDownloadUrl = "https://github.com/rustdesk/rustdesk/releases/download/1.4.9/rustdesk-1.4.9-x86_64.exe";
const string supportHost = "support.im1os.com";
const string serverKey = "0t5yzcyqDFe2yj4X50zdRemrGtotzXD2Pxj8P4b43zI=";

try
{
    Console.Title = "iM1 Remote Support Setup";
    Console.WriteLine();
    Console.WriteLine("iM1 Remote Support");
    Console.WriteLine("Preparing the Remote Support application...");
    Console.WriteLine();

    var tempPath = Path.GetTempPath();
    var workDir = Path.Combine(tempPath, "im1-remote-support");
    var clientPath = Path.Combine(workDir, "iM1-Remote-Support.exe");
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var configDir = Path.Combine(appData, "RustDesk", "config");
    var configPath = Path.Combine(configDir, "RustDesk2.toml");

    Directory.CreateDirectory(workDir);
    Directory.CreateDirectory(configDir);

    Console.WriteLine("Downloading the Remote Support application...");
    using var httpClient = new HttpClient();
    using var response = await httpClient.GetAsync(clientDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();
    await using (var remoteStream = await response.Content.ReadAsStreamAsync())
    await using (var localStream = File.Create(clientPath))
    {
        await remoteStream.CopyToAsync(localStream);
    }

    Console.WriteLine("Applying iM1 Remote Support server settings...");
    var config = $"""
        rendezvous_server = '{supportHost}'
        nat_type = 1
        serial = 0

        [options]
        custom-rendezvous-server = '{supportHost}'
        relay-server = '{supportHost}'
        key = '{serverKey}'
        """;

    await File.WriteAllTextAsync(configPath, config, new UTF8Encoding(false));

    var launchPath = ResolveLaunchPath(clientPath);
    TryImportConfig(launchPath, configPath);

    Console.WriteLine("Opening Remote Support...");
    Process.Start(new ProcessStartInfo
    {
        FileName = launchPath,
        UseShellExecute = true
    });

    Console.WriteLine();
    Console.WriteLine("Remote Support is ready.");
    Console.WriteLine("Provide your Support ID to your iM1 technician, then click Allow when prompted.");
    Thread.Sleep(TimeSpan.FromSeconds(5));
    return 0;
}
catch (Exception exception)
{
    Console.WriteLine();
    Console.WriteLine("Setup could not complete.");
    Console.WriteLine(exception.Message);
    Console.WriteLine();
    Console.WriteLine("Please contact your iM1 technician.");
    Console.WriteLine("Press any key to close.");
    Console.ReadKey(intercept: true);
    return 1;
}

static string ResolveLaunchPath(string downloadedClientPath)
{
    var candidates = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RustDesk", "rustdesk.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "RustDesk", "rustdesk.exe")
    };

    return candidates.FirstOrDefault(File.Exists) ?? downloadedClientPath;
}

static void TryImportConfig(string launchPath, string configPath)
{
    try
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = launchPath,
            ArgumentList = { "--import-config", configPath },
            UseShellExecute = true
        });

        if (process is not null && !process.WaitForExit(milliseconds: 5000))
        {
            process.CloseMainWindow();
        }
    }
    catch
    {
        // The TOML file has already been written; continue and launch the client.
    }
}
