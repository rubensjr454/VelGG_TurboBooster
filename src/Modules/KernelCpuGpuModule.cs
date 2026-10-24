using System;
using System.Drawing;
using Microsoft.Win32;
using VelGG_TurboBooster.Helpers;

namespace VelGG_TurboBooster.Modules
{
    /// <summary>
    /// MÓDULO 3: Otimização de CPU (100% Clock + Core Unparking), GPU (HAGS + Alto Desempenho),
    /// Kernel (100% RAM Física sem SSD Cache) e Remoção de EcoQoS ("Folha Verde").
    /// </summary>
    internal static class KernelCpuGpuModule
    {
        public static void Execute(Action<string, Color> log)
        {
            log("[Módulo 3] Configurando Energia, Clock 100%, Core Unparking e Kernel Memory...", Color.LightSkyBlue);

            // 1. Energia e Clock via powercfg
            SystemCommandHelper.RunSilentCommand("powercfg", "-h off");
            SystemCommandHelper.RunSilentCommand("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
            SystemCommandHelper.RunSilentCommand("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            SystemCommandHelper.RunSilentCommand("powercfg", "-setacvalueindex scheme_current sub_processor PROCTHROTTLEMIN 100");
            SystemCommandHelper.RunSilentCommand("powercfg", "-setacvalueindex scheme_current sub_processor PROCTHROTTLEMAX 100");
            SystemCommandHelper.RunSilentCommand("powercfg", "-setacvalueindex scheme_current sub_processor CPMINCORES 100");
            SystemCommandHelper.RunSilentCommand("powercfg", "-setactive scheme_current");

            // 2. Memória e Kernel: 100% RAM Física de 16GB sem engasgos de SSD Cache
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                "DisablePagingExecutive", 1);
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                "LargeSystemCache", 0);
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control",
                "SvcHostSplitThresholdInKB", 16777216);

            // 3. GPU HAGS (Hardware Accelerated GPU Scheduling) & Win32PrioritySeparation
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                "HwSchMode", 2);
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\PriorityControl",
                "Win32PrioritySeparation", 38);

            // 4. Desativar Power Throttling globalmente (Remove EcoQoS / "Folha Verde")
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                "PowerThrottlingOff", 1);

            // 5. Definir chrome.exe como GPU de Alto Desempenho
            string? chromePath = ChromeTurboLauncher.FindChromeExecutable();
            if (!string.IsNullOrEmpty(chromePath))
            {
                SystemCommandHelper.SetRegistryString(Registry.CurrentUser,
                    @"Software\Microsoft\DirectX\UserGpuPreferences",
                    chromePath, "GpuPreference=2;");
            }

            // 6. Multimedia SystemProfile & Prioridade de Jogos
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                "SystemResponsiveness", 0);
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF));

            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "GPU Priority", 8);
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "Priority", 6);
            SystemCommandHelper.SetRegistryString(Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "Scheduling Category", "High");

            // 7. Políticas do Google Chrome (Aceleração de Hardware, Sem EcoQoS/BatterySaver, Google DoH)
            const string chromePolicyKey = @"SOFTWARE\Policies\Google\Chrome";
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine, chromePolicyKey, "HardwareAccelerationModeEnabled", 1);
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine, chromePolicyKey, "BackgroundModeEnabled", 0);
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine, chromePolicyKey, "HighEfficiencyModeEnabled", 0);
            SystemCommandHelper.SetRegistryDword(Registry.LocalMachine, chromePolicyKey, "BatterySaverModeAvailability", 0);
            SystemCommandHelper.SetRegistryString(Registry.LocalMachine, chromePolicyKey, "DnsOverHttpsMode", "automatic");
            SystemCommandHelper.SetRegistryString(Registry.LocalMachine, chromePolicyKey, "DnsOverHttpsTemplates", "https://dns.google/dns-query");

            log("  -> CPU 100% Clock, Core Unparking, HAGS=2, Sem EcoQoS e Políticas do Chrome aplicadas.", Color.LightGreen);
        }
    }
}

