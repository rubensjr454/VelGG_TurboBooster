using System;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using VelGG_TurboBooster.Helpers;

namespace VelGG_TurboBooster.Modules
{
    /// <summary>
    /// MÓDULO 4: Otimização de Rede para Co-op Multiplayer, Google DNS (IPv4 e IPv6),
    /// Tuning da Pilha TCP (CTCP/RSS) e Desativação do Algoritmo de Nagle (Zero Delay).
    /// </summary>
    internal static class NetworkOptimizerModule
    {
        public static void Execute(Action<string, Color> log)
        {
            log("[Módulo 4] Aplicando Google DNS IPv4/IPv6, TCP Stack e Zero Nagle...", Color.LightSkyBlue);

            // 1. Configurar DNS IPv4 (8.8.8.8 / 8.8.4.4) e IPv6 (2001:4860:4860::8888) nas interfaces ativas
            try
            {
                var activeInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var ni in activeInterfaces)
                {
                    string ifaceName = ni.Name;
                    SystemCommandHelper.RunSilentCommand("netsh", $"interface ipv4 set dnsservers name=\"{ifaceName}\" source=static address=8.8.8.8 register=primary validate=no");
                    SystemCommandHelper.RunSilentCommand("netsh", $"interface ipv4 add dnsservers name=\"{ifaceName}\" address=8.8.4.4 index=2 validate=no");
                    SystemCommandHelper.RunSilentCommand("netsh", $"interface ipv6 set dnsservers name=\"{ifaceName}\" source=static address=2001:4860:4860::8888 register=primary validate=no");
                }
            }
            catch { }

            // 2. Otimizações da pilha TCP e Flush DNS
            SystemCommandHelper.RunSilentCommand("netsh", "int tcp set global rss=enabled");
            SystemCommandHelper.RunSilentCommand("netsh", "int tcp set global rsc=disabled");
            SystemCommandHelper.RunSilentCommand("netsh", "int tcp set global ecncapability=disabled");
            SystemCommandHelper.RunSilentCommand("netsh", "int tcp set global timestamps=disabled");
            SystemCommandHelper.RunSilentCommand("netsh", "int tcp set supplemental Internet congestionprovider=ctcp");
            SystemCommandHelper.RunSilentCommand("ipconfig", "/flushdns");

            // 3. Desativar Algoritmo de Nagle em todas as subchaves de Tcpip\Parameters\Interfaces
            int interfacesTuned = 0;
            try
            {
                using var interfacesKey = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", writable: true);

                if (interfacesKey != null)
                {
                    foreach (string subKeyName in interfacesKey.GetSubKeyNames())
                    {
                        try
                        {
                            using var subKey = interfacesKey.OpenSubKey(subKeyName, writable: true);
                            if (subKey != null)
                            {
                                subKey.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                                subKey.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                                subKey.SetValue("TcpDelAckTicks", 0, RegistryValueKind.DWord);
                                interfacesTuned++;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            log($"  -> Google DNS ativo, CTCP habilitado e Algoritmo de Nagle desativado em {interfacesTuned} interfaces.", Color.LightGreen);
        }
    }
}

