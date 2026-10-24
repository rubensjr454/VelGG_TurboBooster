using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace VelGG_TurboBooster
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            if (!IsRunningAsAdministrator())
            {
                try
                {
                    var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = exePath,
                            WorkingDirectory = AppContext.BaseDirectory,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        Process.Start(startInfo);
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show(
                        "Este aplicativo requer privilégios de Administrador para otimizar o Kernel, Rede e Memória RAM.",
                        "VelGG TurboBooster - Acesso Negado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }

        private static bool IsRunningAsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}

