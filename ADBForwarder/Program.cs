using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using SharpAdbClient;
using ICSharpCode.SharpZipLib.Zip;

namespace ADBForwarder
{
    internal static class Program
    {
        private static readonly string[] DeviceNames =
        {
            "monterey", // Oculus Quest 1
            "hollywood", // Oculus Quest 2
            "eureka", // Oculus Quest 3
            "pacific", // Oculus Go
            "a7h10", // Pico neo 3 (+link)
            "phoenix_ovs", // Pico 4
            "vr_monterey", // Edge case for linux, Quest 1
            "vr_hollywood", // Edge case for linux, Oculus Quest 2
            "vr_eureka", // Edge case for linux, Oculus Quest 3
            "vr_pacific", // Edge case for linux, Oculus Go
            "vr_a7h10", // Edge case for linux, Pico neo 3 (+link)
            "vr_phoenix_ovs" // Edge case for linux, Pico 4
        };

        private static readonly AdbClient Client = new();
        private static readonly AdbServer Server = new();
        private static readonly IPEndPoint EndPoint = new(IPAddress.Loopback, AdbClient.AdbServerPort);

        private static void Main()
        {
            Console.ResetColor();
            var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
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
                Console.WriteLine("ADB not found, downloading in the background...");
                DownloadADB(downloadUri);

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
                if (!DeviceNames.Contains(deviceData.Product.ToLower()))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(
                        $"Skipped forwarding device: {(string.IsNullOrEmpty(deviceData.Product) ? deviceData.Serial : deviceData.Product)}");
                    return;
                }

                Client.CreateForward(deviceData, 9943, 9943);
                Client.CreateForward(deviceData, 9944, 9944);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Successfully forwarded device: {deviceData.Serial} [{deviceData.Product}]");

                return;
            }
        }

        private static void DownloadADB(string downloadUri)
        {
            using var web = new WebClient();
            web.DownloadFile(downloadUri, "adb.zip");
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