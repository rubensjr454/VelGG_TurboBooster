using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VelGG_TurboBooster.Native;

namespace VelGG_TurboBooster.Modules
{
    /// <summary>
    /// MÓDULO 6: Listener Assíncrono de Hotkeys (GetAsyncKeyState em user32.dll)
    /// para envio de Comandos de Console/Cheats na partida (F1 a F5 + END).
    /// </summary>
    internal sealed class HotkeyConsoleListener : IDisposable
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public void Start(Control uiInvoker, Action<bool> onStatusChanged, Action<string, Color> log)
        {
            if (_isRunning)
                return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _isRunning = true;

            onStatusChanged(true);
            log("[Módulo 6] Listener de Hotkeys ATIVO (F1-F5 para Console Cheats | END para desativar).", Color.Gold);

            Task.Run(async () =>
            {
                var keyCommands = new Dictionary<int, string>
                {
                    { Win32Native.VK_F1, "/god" },
                    { Win32Native.VK_F2, "/sf_use_ignoreammo 1" },
                    { Win32Native.VK_F3, "/give all" },
                    { Win32Native.VK_F4, "/noclip" },
                    { Win32Native.VK_F5, "/notarget" }
                };

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if ((Win32Native.GetAsyncKeyState(Win32Native.VK_END) & 0x8000) != 0)
                        {
                            Console.Beep(300, 400);
                            _isRunning = false;
                            onStatusChanged(false);
                            log("[Módulo 6] Listener de Hotkeys DESATIVADO pelo usuário (Tecla END).", Color.Orange);
                            break;
                        }

                        foreach (var kvp in keyCommands)
                        {
                            if ((Win32Native.GetAsyncKeyState(kvp.Key) & 0x8000) != 0)
                            {
                                Console.Beep(900, 150);
                                SendConsoleCommand(uiInvoker, kvp.Value);
                                log($"[Macro] Comando enviado: {kvp.Value}", Color.Yellow);
                                await Task.Delay(350, token);
                            }
                        }

                        await Task.Delay(25, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch { }
                }
            }, token);
        }

        private static void SendConsoleCommand(Control uiInvoker, string command)
        {
            if (uiInvoker.InvokeRequired)
            {
                uiInvoker.BeginInvoke(new Action(() => SendConsoleCommand(uiInvoker, command)));
                return;
            }

            SendKeys.SendWait("`");
            Thread.Sleep(60);
            SendKeys.SendWait(command);
            SendKeys.SendWait("{ENTER}");
            Thread.Sleep(40);
            SendKeys.SendWait("`");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _isRunning = false;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}

