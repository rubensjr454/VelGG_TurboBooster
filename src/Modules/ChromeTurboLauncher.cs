using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VelGG_TurboBooster.Modules
{
    /// <summary>
    /// MÓDULO 5: Custom Chrome Launcher com 8GB Heap V8 (--max-old-space-size=8192),
    /// Flags de Desempenho Extremo para WebGL/WebAssembly e Elevação para ProcessPriorityClass.High.
    /// </summary>
    internal static class ChromeTurboLauncher
    {
        public static string? FindChromeExecutable()
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        public static async Task LaunchAsync(string targetUrl, Action<string, Color> log)
        {
            string? chromeExe = FindChromeExecutable();
            if (string.IsNullOrEmpty(chromeExe) || !File.Exists(chromeExe))
            {
                log("[ERRO] Google Chrome (chrome.exe) não encontrado nos caminhos padrão!", Color.Red);
                MessageBox.Show("O Google Chrome não foi encontrado no sistema.", "Chrome Não Encontrado",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            log("[Módulo 5] Encerrando instâncias antigas do Chrome para aplicar flags V8 8GB...", Color.LightSkyBlue);
            foreach (var proc in Process.GetProcessesByName("chrome"))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                finally { proc.Dispose(); }
            }

            await Task.Delay(800);

            string flags =
                $"--app={targetUrl} " +
                "--start-maximized " +
                "--disable-extensions " +
                "--disable-plugins " +
                "--ignore-gpu-blocklist " +
                "--enable-gpu-rasterization " +
                "--enable-zero-copy " +
                "--enable-raw-draw " +
                "--disable-gpu-driver-bug-workarounds " +
                "--disable-frame-rate-limit " +
                "--disable-background-timer-throttling " +
                "--disable-renderer-backgrounding " +
                "--disable-backgrounding-occluded-windows " +
                "--webrtc-max-cpu-consumption-percentage=100 " +
                "--enable-quic " +
                "--disable-features=UseEcoQoSForBackgroundProcess,HighEfficiencyModeAvailable,BatterySaverModeAvailable,WebRtcHideLocalIpsWithMdns " +
                "--js-flags=\"--max-old-space-size=8192\"";

            log($"[Módulo 5] Iniciando Chrome Turbo em: {targetUrl}", Color.Cyan);

            Process.Start(new ProcessStartInfo
            {
                FileName = chromeExe,
                Arguments = flags,
                UseShellExecute = false
            });

            log("[Módulo 5] Aguardando 3 segundos para elevar PriorityClass para High...", Color.LightSkyBlue);
            await Task.Delay(3000);

            int elevatedCount = 0;
            foreach (var proc in Process.GetProcessesByName("chrome"))
            {
                try
                {
                    proc.PriorityClass = ProcessPriorityClass.High;
                    elevatedCount++;
                }
                catch { }
                finally
                {
                    proc.Dispose();
                }
            }

            log($"  -> {elevatedCount} processos do Chrome definidos como ProcessPriorityClass.High!", Color.LightGreen);
        }
    }
}

