using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using HomePodStreamer.Utils;

namespace HomePodStreamer.Services
{
    public class WslOwntoneService : IOwntoneHostService
    {
        private const string WslDistro = "Ubuntu";
        private readonly IOwntoneApiClient _apiClient;
        private Process? _owntoneProcess;

        public bool IsRunning { get; private set; }

        public WslOwntoneService(IOwntoneApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task StartAsync()
        {
            try
            {
                // Kill any leftover owntone processes
                await RunWslAsync("pkill -f owntone || true", asRoot: true);
                await RunWslAsync("pkill -f socat || true", asRoot: true);

                // Copy config file to WSL
                var scriptsDir = FindScriptsDir();
                var confSource = Path.Combine(scriptsDir, "owntone.conf");
                if (File.Exists(confSource))
                {
                    var wslConfPath = ToWslPath(confSource);
                    await RunWslAsync($"cp {wslConfPath} /etc/owntone.conf", asRoot: true);
                    Logger.Info("Copied owntone.conf to WSL");
                }

                // Start owntone via the startup script in background
                var scriptPath = Path.Combine(scriptsDir, "start-owntone.sh");
                if (!File.Exists(scriptPath))
                {
                    throw new FileNotFoundException($"start-owntone.sh not found at: {scriptPath}");
                }

                var wslScriptPath = ToWslPath(scriptPath);
                Logger.Info($"Starting owntone in WSL ({WslDistro}) via {wslScriptPath}");

                _owntoneProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "wsl",
                        Arguments = $"-d {WslDistro} -u root -- bash {wslScriptPath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                _owntoneProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Logger.Info($"[owntone] {e.Data}");
                };
                _owntoneProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Logger.Info($"[owntone-err] {e.Data}");
                };

                _owntoneProcess.Start();
                _owntoneProcess.BeginOutputReadLine();
                _owntoneProcess.BeginErrorReadLine();

                IsRunning = true;
                Logger.Info("Owntone WSL process started");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start owntone in WSL", ex);
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                Logger.Info("Stopping owntone in WSL");

                // Kill the WSL wrapper process first to stop the script
                if (_owntoneProcess != null && !_owntoneProcess.HasExited)
                {
                    _owntoneProcess.Kill();
                    _owntoneProcess.Dispose();
                    _owntoneProcess = null;
                }

                // Kill all related processes in a single command
                await RunWslAsync(
                    "pkill -f owntone; pkill -f socat; pkill -f avahi-publish; pkill -f 'python3 -c'; rm -f /srv/media/pcm_input; true",
                    asRoot: true);

                IsRunning = false;
                Logger.Info("Owntone stopped");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to stop owntone in WSL", ex);
            }
        }

        public async Task WaitForHealthyAsync(int timeoutSeconds = 60)
        {
            Logger.Info($"Waiting for owntone to become healthy (timeout: {timeoutSeconds}s)");

            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (await _apiClient.IsAvailableAsync())
                {
                    Logger.Info("Owntone is healthy and responding on localhost:3689");
                    return;
                }

                await Task.Delay(2000);
            }

            throw new TimeoutException(
                $"Owntone did not become healthy within {timeoutSeconds} seconds. " +
                "Ensure WSL2 Ubuntu is running with owntone installed.");
        }

        private static string FindScriptsDir()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var scriptsDir = Path.Combine(baseDir, "scripts");

            // Fallback: check relative to project root (development)
            if (!Directory.Exists(scriptsDir))
            {
                var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
                scriptsDir = Path.Combine(projectRoot, "scripts");
            }

            return scriptsDir;
        }

        private static string ToWslPath(string windowsPath)
        {
            // Convert C:\Users\foo\bar to /mnt/c/Users/foo/bar
            var fullPath = Path.GetFullPath(windowsPath);
            var drive = char.ToLower(fullPath[0]);
            var rest = fullPath.Substring(2).Replace('\\', '/');
            return $"/mnt/{drive}{rest}";
        }

        private async Task<(int ExitCode, string Output, string Error)> RunWslAsync(string command, bool asRoot = false)
        {
            var userArg = asRoot ? "-u root " : "";
            var processInfo = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = $"-d {WslDistro} {userArg}-- bash -c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode, output, error);
        }
    }
}
