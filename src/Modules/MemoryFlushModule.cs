using System;
using System.Diagnostics;
using System.Drawing;
using VelGG_TurboBooster.Native;

namespace VelGG_TurboBooster.Modules
{
    /// <summary>
    /// MÓDULO 2: Monitoramento de Memória Física (GlobalMemoryStatusEx) e Flush Físico
    /// de Working Set via P/Invoke (EmptyWorkingSet em psapi.dll).
    /// </summary>
    internal static class MemoryFlushModule
    {
        public static (double freeGb, double totalGb, uint loadPercent) GetMemoryStatus()
        {
            var memStatus = new Win32Native.MEMORYSTATUSEX();
            if (Win32Native.GlobalMemoryStatusEx(memStatus))
            {
                double freeGb = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                double totalGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                return (freeGb, totalGb, memStatus.dwMemoryLoad);
            }
            return (0, 16, 0);
        }

        public static void ExecuteFlush(Action<string, Color> log)
        {
            log("[Módulo 2] Executando EmptyWorkingSet em todos os processos (psapi.dll)...", Color.LightSkyBlue);

            var (beforeFlush, _, _) = GetMemoryStatus();
            int flushedProcesses = 0;

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (Win32Native.EmptyWorkingSet(proc.Handle) != 0)
                        flushedProcesses++;
                }
                catch
                {
                    // Processos protegidos do Kernel (System / CSRSS) são ignorados com segurança
                }
                finally
                {
                    proc.Dispose();
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var (afterFlush, _, _) = GetMemoryStatus();
            double delta = Math.Max(0, afterFlush - beforeFlush);
            log($"  -> Working Set limpo em {flushedProcesses} processos (+{delta:F2} GB liberados no Flush).", Color.LightGreen);
        }
    }
}

