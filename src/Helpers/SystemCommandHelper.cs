using System.Diagnostics;
using Microsoft.Win32;

namespace VelGG_TurboBooster.Helpers
{
    /// <summary>
    /// Utilitários para execução silenciosa de comandos de sistema e edição segura do Registro do Windows.
    /// </summary>
    internal static class SystemCommandHelper
    {
        public static void RunSilentCommand(string fileName, string arguments, int timeoutMs = 3500)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                proc?.WaitForExit(timeoutMs);
            }
            catch
            {
                // Ignora falhas de comando individual para não interromper o fluxo de otimização
            }
        }

        public static void SetRegistryDword(RegistryKey root, string subKeyPath, string valueName, int data)
        {
            try
            {
                using var key = root.CreateSubKey(subKeyPath, writable: true);
                key?.SetValue(valueName, data, RegistryValueKind.DWord);
            }
            catch { }
        }

        public static void SetRegistryString(RegistryKey root, string subKeyPath, string valueName, string data)
        {
            try
            {
                using var key = root.CreateSubKey(subKeyPath, writable: true);
                key?.SetValue(valueName, data, RegistryValueKind.String);
            }
            catch { }
        }
    }
}

