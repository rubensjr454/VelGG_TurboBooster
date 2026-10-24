using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using VelGG_TurboBooster.Helpers;

namespace VelGG_TurboBooster.Modules
{
    /// <summary>
    /// MÓDULO 1: Extermínio de Processos em Segundo Plano, Parada de Serviços Pesados,
    /// Debloat de Inicialização (Startup) e Limpeza de Arquivos Temporários.
    /// </summary>
    internal static class SystemDebloatModule
    {
        private static readonly string[] ServicesToStop =
        {
            "vgc", "vgk", "DiagTrack", "dmwappushservice", "MapsBroker",
            "lfsvc", "WSearch", "SysMain", "DSAService", "DSAUpdateService", "esifsvc"
        };

        private static readonly string[] ProcessKillList =
        {
            "vgtray", "RiotClientServices", "RiotClientUx",
            "Lively", "Lively.UI.WinUI", "LivelySubProcess", "CefSharp.BrowserSubprocess", "mpv",
            "Rainmeter", "TranslucentTB", "WhatsApp", "RobloxPlayerBeta",
            "msedge", "msedgewebview2", "WidgetService", "Widgets",
            "PhoneExperienceHost", "GameBar", "OneDrive", "Teams",
            "SkypeApp", "Spotify", "Steam", "EpicGamesLauncher",
            "SearchHost", "SurSvc"
        };

        private static readonly string[] ProtectedStartupKeywords =
        {
            "Rainmeter", "Lively", "Wallpaper", "SecurityHealth", "WindowsDefender",
            "Realtek", "RtkAud", "Waves", "Dolby", "SynTPEnh", "Elan", "Samsung"
        };

        public static void Execute(Action<string, Color> log)
        {
            StopHeavyServices(log);
            KillBackgroundProcesses(log);
            CleanStartupEntries(log);
            CleanTempDirectories(log);
        }

        private static void StopHeavyServices(Action<string, Color> log)
        {
            log("[Módulo 1] Parando serviços pesados e anti-cheat ocioso (Vanguard/Telemetria)...", Color.LightSkyBlue);

            foreach (string svcName in ServicesToStop)
            {
                SystemCommandHelper.RunSilentCommand("sc", $"stop \"{svcName}\"");
                log($"  -> Comando de parada enviado ao serviço: {svcName}", Color.Gray);
            }
        }

        private static void KillBackgroundProcesses(Action<string, Color> log)
        {
            log("[Módulo 1] Encerrando processos em segundo plano (Kill List)...", Color.LightSkyBlue);

            int killedCount = 0;
            foreach (string procName in ProcessKillList)
            {
                try
                {
                    var procs = Process.GetProcessesByName(procName);
                    foreach (var p in procs)
                    {
                        try
                        {
                            p.Kill(entireProcessTree: true);
                            killedCount++;
                        }
                        catch { }
                        finally
                        {
                            p.Dispose();
                        }
                    }
                }
                catch { }
            }

            log($"  -> {killedCount} processos de background encerrados.", Color.LightGreen);
        }

        private static void CleanStartupEntries(Action<string, Color> log)
        {
            log("[Módulo 1] Limpando chaves de Inicialização (preservando Lively, Rainmeter, Áudio e Segurança)...", Color.LightSkyBlue);

            bool IsProtected(string text) =>
                ProtectedStartupKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

            void CleanRunRegistryKey(RegistryKey root, string subKeyPath)
            {
                try
                {
                    using var key = root.OpenSubKey(subKeyPath, writable: true);
                    if (key == null) return;

                    foreach (string valueName in key.GetValueNames())
                    {
                        string valData = key.GetValue(valueName)?.ToString() ?? string.Empty;
                        if (!IsProtected(valueName) && !IsProtected(valData))
                        {
                            try { key.DeleteValue(valueName, false); } catch { }
                        }
                    }
                }
                catch { }
            }

            CleanRunRegistryKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
            CleanRunRegistryKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
            CleanRunRegistryKey(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run");

            string[] startupFolders =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
            };

            foreach (string folder in startupFolders)
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) continue;
                try
                {
                    foreach (string file in Directory.GetFiles(folder))
                    {
                        string fileName = Path.GetFileName(file);
                        if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!IsProtected(fileName))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                }
                catch { }
            }
        }

        private static void CleanTempDirectories(Action<string, Color> log)
        {
            log("[Módulo 1] Limpando diretórios Temporários (%TEMP%, Windows\\Temp)...", Color.LightSkyBlue);

            string[] tempPaths =
            {
                Path.GetTempPath(),
                @"C:\Windows\Temp",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp")
            };

            foreach (string dir in tempPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(dir)) continue;

                try
                {
                    foreach (string file in Directory.GetFiles(dir))
                    {
                        try { File.Delete(file); } catch { }
                    }

                    foreach (string subDir in Directory.GetDirectories(dir))
                    {
                        try { Directory.Delete(subDir, recursive: true); } catch { }
                    }
                }
                catch { }
            }
        }
    }
}

