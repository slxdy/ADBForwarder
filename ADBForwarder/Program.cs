using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SharpAdbClient;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Configuration;

namespace ADBForwarder
{
    internal class PortConfiguration(List<int> ports)
    {
        public List<int> Ports { get; set; } = ports;
    }

    internal static class Program
    {
        private static readonly AdbClient Client = new();
        private static readonly AdbServer Server = new();
        private static readonly IPEndPoint EndPoint = new(IPAddress.Loopback, AdbClient.AdbServerPort);
        private static PortConfiguration _portConfiguration;

        private static void Main()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", optional: true);

            IConfiguration config = builder.Build();

            _portConfiguration = config.GetSection("PortConfiguration").Get<PortConfiguration>()
                                 ?? new PortConfiguration([9943, 9944]);

            Console.ResetColor();
            var currentDirectory = Path.GetDirectoryName(AppContext.BaseDirectory);
            if (currentDirectory == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Path error!");
                return;
            }

            var adbPath = "adb/platform-tools/{0}";
            var downloadUri = "https://dl.google.com/android/repository/platform-tools-latest-{0}.zip";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine("Platform: Linux");

                adbPath = string.Format(adbPath, "adb");
                downloadUri = string.Format(downloadUri, "linux");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("Platform: Windows");

                adbPath = string.Format(adbPath, "adb.exe");
                downloadUri = string.Format(downloadUri, "windows");
            }
            else
            {
                Console.WriteLine("Unsupported platform!");
                return;
            }

            var absoluteAdbPath = Path.Combine(currentDirectory, adbPath);
            if (!File.Exists(absoluteAdbPath))
            {
                Console.WriteLine("ADB not found, downloading...");
                DownloadADB(downloadUri).Wait();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    SetExecutable(absoluteAdbPath);
            }

            Console.WriteLine("Starting ADB Server...");
            Server.StartServer(absoluteAdbPath, false);

            Client.Connect(EndPoint);

            var monitor = new DeviceMonitor(new AdbSocket(EndPoint));
            monitor.DeviceConnected += Monitor_DeviceConnected;
            monitor.DeviceDisconnected += Monitor_DeviceDisconnected;
            monitor.Start();

            while (true)
            {
                // Main thread needs to stay alive, 100ms is acceptable idle time
                Thread.Sleep(100);
            }
        }

        private static void Monitor_DeviceConnected(object sender, DeviceDataEventArgs e)
        {
            // Prevent duplicate ports
            _portConfiguration.Ports = _portConfiguration.Ports.Distinct().ToList();
            if (e.Device.Serial.StartsWith("127.0.0.1"))
            {
                // We don't want to re-forward local device
                return;
            }

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"Connected device: {e.Device.Serial}");
            Forward(e.Device);
        }

        private static void Monitor_DeviceDisconnected(object sender, DeviceDataEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"Disconnected device: {e.Device.Serial}");
        }

        private static void Forward(DeviceData device)
        {
            // DeviceConnected calls without product set yet
            Thread.Sleep(1000);

            foreach (var deviceData in Client.GetDevices().Where(deviceData => device.Serial == deviceData.Serial))
            {
                if (deviceData.State != DeviceState.Online)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(
                        $"Skipped forwarding device: {(string.IsNullOrEmpty(deviceData.Product) ? deviceData.Serial : deviceData.Product)}");
                    return;
                }

                foreach (var port in _portConfiguration.Ports)
                {
                    Client.CreateForward(deviceData, port, port);
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Successfully forwarded device: {deviceData.Serial} [{deviceData.Product}]");

                return;
            }
        }

        private static async Task DownloadADB(string downloadUri)
        {
            using var client = new HttpClient();
            var fileStream = await client.GetStreamAsync(downloadUri);
            await using (var outputFileStream = new FileStream("adb.zip", FileMode.Create))
            {
                await fileStream.CopyToAsync(outputFileStream);
            }

            Console.WriteLine("Download successful");

            var zip = new FastZip();
            zip.ExtractZip("adb.zip", "adb", null);
            Console.WriteLine("Extraction successful");

            File.Delete("adb.zip");
        }

        private static void SetExecutable(string fileName)
        {
            Console.WriteLine("Giving adb executable permissions");

            var args = $"chmod u+x {fileName}";
            var escapedArgs = args.Replace("\"", "\\\"");

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "/bin/bash",
                Arguments = $"-c \"{escapedArgs}\""
            };

            process.Start();
            process.WaitForExit();
        }
    }
}