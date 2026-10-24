using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using VelGG_TurboBooster.Modules;

namespace VelGG_TurboBooster
{
    /// <summary>
    /// Interface Gráfica Principal (Dark Gamer Theme) e Orquestradora dos Módulos.
    /// </summary>
    public class MainForm : Form
    {
        private Label lblRamBefore = null!;
        private Label lblRamNow = null!;
        private Label lblRamRecovered = null!;
        private ProgressBar pbRamLoad = null!;

        private RadioButton rbBo1z = null!;
        private RadioButton rbBsz = null!;
        private RadioButton rbMultiplayer = null!;
        private TextBox txtInviteUrl = null!;

        private CheckBox chkCleanRamAndProcs = null!;
        private CheckBox chkNetworkOpt = null!;
        private CheckBox chkCpuGpuKernel = null!;
        private CheckBox chkHotkeys = null!;

        private Button btnTurboLaunch = null!;
        private Button btnOptimizeOnly = null!;
        private RichTextBox rtbLog = null!;
        private Label lblHotkeyStatus = null!;

        private System.Windows.Forms.Timer ramTimer = null!;
        private double initialFreeRamGb;
        private double lastRecoveredGb;

        private readonly HotkeyConsoleListener hotkeyListener = new();

        public MainForm()
        {
            InitializeComponent();
            CaptureInitialRam();
            StartRamMonitor();
            Log("VelGG TurboBooster inicializado com privilégios de Administrador.", Color.Cyan);
            Log("Pronto para otimização extrema de WebAssembly/WebGL (Call of Duty Zombies).", Color.LightGreen);
        }

        private void InitializeComponent()
        {
            Text = "VelGG TurboBooster - Extreme WebGL/WASM Optimizer & Launcher";
            Size = new Size(760, 740);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(14, 14, 20);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

            // Header Banner
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.FromArgb(22, 22, 32)
            };

            var lblTitle = new Label
            {
                Text = "⚡ VEL.GG TURBOBOOSTER // COD ZOMBIES EDITION",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 230, 118),
                Location = new Point(18, 10),
                AutoSize = true
            };

            var lblSubtitle = new Label
            {
                Text = "Otimizador de Kernel, Rede Zero-Nagle, Flush de RAM e Custom V8 8GB Launcher",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 165, 185),
                Location = new Point(20, 40),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            Controls.Add(pnlHeader);

            // RAM Status Group
            var grpRam = CreateDarkGroupBox("MONITOR DE MEMÓRIA FÍSICA (GlobalMemoryStatusEx)", new Point(18, 80), new Size(708, 95));

            lblRamBefore = new Label
            {
                Text = "RAM Livre (Antes): -- GB",
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 183, 77),
                Location = new Point(16, 28),
                AutoSize = true
            };

            lblRamNow = new Label
            {
                Text = "RAM Livre (Agora): -- GB",
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 230, 118),
                Location = new Point(255, 28),
                AutoSize = true
            };

            lblRamRecovered = new Label
            {
                Text = "Recuperado: +0.00 GB",
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(64, 196, 255),
                Location = new Point(505, 28),
                AutoSize = true
            };

            pbRamLoad = new ProgressBar
            {
                Location = new Point(16, 58),
                Size = new Size(675, 20),
                Style = ProgressBarStyle.Continuous
            };

            grpRam.Controls.Add(lblRamBefore);
            grpRam.Controls.Add(lblRamNow);
            grpRam.Controls.Add(lblRamRecovered);
            grpRam.Controls.Add(pbRamLoad);
            Controls.Add(grpRam);

            // Seletor de Jogo / Modo
            var grpMode = CreateDarkGroupBox("SELETOR DE JOGO / MODO (VEL.GG)", new Point(18, 185), new Size(708, 130));

            rbBo1z = new RadioButton
            {
                Text = "1. Jogar BO1 Zombies (Solo / Criar Lobby)  [https://vel.gg/bo1z]",
                Location = new Point(18, 28),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke
            };

            rbBsz = new RadioButton
            {
                Text = "2. Jogar BSZ Zombies (Solo / Criar Lobby)  [https://vel.gg/bsz]",
                Location = new Point(18, 56),
                AutoSize = true,
                ForeColor = Color.WhiteSmoke
            };

            rbMultiplayer = new RadioButton
            {
                Text = "3. Entrar em Lobby Multiplayer (Link de Convite):",
                Location = new Point(18, 86),
                AutoSize = true,
                ForeColor = Color.WhiteSmoke
            };

            txtInviteUrl = new TextBox
            {
                Location = new Point(350, 84),
                Size = new Size(340, 25),
                BackColor = Color.FromArgb(30, 30, 44),
                ForeColor = Color.FromArgb(0, 230, 118),
                BorderStyle = BorderStyle.FixedSingle,
                Enabled = false,
                PlaceholderText = "vel.gg/bo1z?lobby=..."
            };

            rbMultiplayer.CheckedChanged += (_, _) =>
            {
                txtInviteUrl.Enabled = rbMultiplayer.Checked;
                if (rbMultiplayer.Checked)
                    txtInviteUrl.Focus();
            };

            grpMode.Controls.Add(rbBo1z);
            grpMode.Controls.Add(rbBsz);
            grpMode.Controls.Add(rbMultiplayer);
            grpMode.Controls.Add(txtInviteUrl);
            Controls.Add(grpMode);

            // Checkboxes de Módulos
            var grpModules = CreateDarkGroupBox("MÓDULOS DE OTIMIZAÇÃO EXTREMA", new Point(18, 325), new Size(708, 145));

            chkCleanRamAndProcs = new CheckBox
            {
                Text = "Limpeza Extrema de RAM e Processos (Inclui fechar Riot Vanguard, Lively Wallpaper e Rainmeter)",
                Location = new Point(18, 28),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke
            };

            chkNetworkOpt = new CheckBox
            {
                Text = "Otimização de Rede + Google DNS (8.8.8.8 / DoH) + Zero Nagle",
                Location = new Point(18, 56),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke
            };

            chkCpuGpuKernel = new CheckBox
            {
                Text = "Desempenho Máximo de CPU (100% Clock) e GPU (HAGS + Sem EcoQoS)",
                Location = new Point(18, 84),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke
            };

            chkHotkeys = new CheckBox
            {
                Text = "Ativar Hotkeys de Console/Cheats na Partida (F1 a F5)  [END para desativar]",
                Location = new Point(18, 112),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(255, 213, 79)
            };

            grpModules.Controls.Add(chkCleanRamAndProcs);
            grpModules.Controls.Add(chkNetworkOpt);
            grpModules.Controls.Add(chkCpuGpuKernel);
            grpModules.Controls.Add(chkHotkeys);
            Controls.Add(grpModules);

            // Botões de Ação
            btnTurboLaunch = new Button
            {
                Text = "🚀 OTIMIZAR TUDO E INICIAR JOGO (MODO TURBO)",
                Location = new Point(18, 480),
                Size = new Size(495, 48),
                BackColor = Color.FromArgb(0, 200, 83),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTurboLaunch.FlatAppearance.BorderSize = 0;
            btnTurboLaunch.Click += async (_, _) => await ExecuteBoosterAsync(launchGame: true);

            btnOptimizeOnly = new Button
            {
                Text = "🛠️ Apenas Otimizar PC",
                Location = new Point(525, 480),
                Size = new Size(201, 48),
                BackColor = Color.FromArgb(40, 44, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOptimizeOnly.FlatAppearance.BorderColor = Color.FromArgb(80, 90, 120);
            btnOptimizeOnly.Click += async (_, _) => await ExecuteBoosterAsync(launchGame: false);

            Controls.Add(btnTurboLaunch);
            Controls.Add(btnOptimizeOnly);

            // Console de Log e Status de Hotkeys
            lblHotkeyStatus = new Label
            {
                Text = "Hotkeys: INATIVO (F1: /god | F2: /sf_use_ignoreammo 1 | F3: /give all | F4: /noclip | F5: /notarget | END: Parar)",
                Location = new Point(18, 536),
                Size = new Size(708, 20),
                Font = new Font("Consolas", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 165, 185)
            };
            Controls.Add(lblHotkeyStatus);

            rtbLog = new RichTextBox
            {
                Location = new Point(18, 560),
                Size = new Size(708, 125),
                BackColor = Color.FromArgb(10, 10, 14),
                ForeColor = Color.FromArgb(0, 230, 118),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                ReadOnly = true
            };
            Controls.Add(rtbLog);
        }

        private static GroupBox CreateDarkGroupBox(string title, Point location, Size size)
        {
            return new GroupBox
            {
                Text = title,
                Location = location,
                Size = size,
                ForeColor = Color.FromArgb(0, 229, 255),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
        }

        private void CaptureInitialRam()
        {
            var (freeGb, totalGb, load) = MemoryFlushModule.GetMemoryStatus();
            initialFreeRamGb = freeGb;
            lblRamBefore.Text = $"RAM Livre (Antes): {initialFreeRamGb:F2} GB";
            UpdateRamLabels(freeGb, totalGb, load);
        }

        private void StartRamMonitor()
        {
            ramTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            ramTimer.Tick += (_, _) =>
            {
                var (freeGb, totalGb, load) = MemoryFlushModule.GetMemoryStatus();
                UpdateRamLabels(freeGb, totalGb, load);
            };
            ramTimer.Start();
        }

        private void UpdateRamLabels(double freeGb, double totalGb, uint load)
        {
            lblRamNow.Text = $"RAM Livre (Agora): {freeGb:F2} / {totalGb:F1} GB";
            double diff = Math.Max(0, freeGb - initialFreeRamGb);
            if (diff > lastRecoveredGb)
                lastRecoveredGb = diff;

            lblRamRecovered.Text = $"Recuperado: +{Math.Max(diff, lastRecoveredGb):F2} GB";
            pbRamLoad.Value = (int)Math.Clamp(load, 0, 100);
        }

        private async Task ExecuteBoosterAsync(bool launchGame)
        {
            string targetUrl = ResolveTargetUrl();
            if (launchGame && string.IsNullOrWhiteSpace(targetUrl))
            {
                MessageBox.Show("Informe um link válido de convite para o Lobby Multiplayer!", "URL Inválida",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnTurboLaunch.Enabled = false;
            btnOptimizeOnly.Enabled = false;

            try
            {
                var (beforeFree, _, _) = MemoryFlushModule.GetMemoryStatus();
                initialFreeRamGb = beforeFree;
                lblRamBefore.Text = $"RAM Livre (Antes): {initialFreeRamGb:F2} GB";

                Log("=== INICIANDO PROTOCOLO DE OTIMIZAÇÃO TURBO ===", Color.Yellow);

                if (chkCleanRamAndProcs.Checked)
                {
                    await Task.Run(() =>
                    {
                        SystemDebloatModule.Execute(Log);
                        MemoryFlushModule.ExecuteFlush(Log);
                    });
                }

                if (chkCpuGpuKernel.Checked)
                {
                    await Task.Run(() => KernelCpuGpuModule.Execute(Log));
                }

                if (chkNetworkOpt.Checked)
                {
                    await Task.Run(() => NetworkOptimizerModule.Execute(Log));
                }

                var (afterFree, total, load) = MemoryFlushModule.GetMemoryStatus();
                double recovered = Math.Max(0, afterFree - beforeFree);
                lastRecoveredGb = recovered;
                UpdateRamLabels(afterFree, total, load);
                Log($"[RAM] Memória física livre agora: {afterFree:F2} GB (Recuperado: +{recovered:F2} GB).", Color.Cyan);

                if (chkHotkeys.Checked)
                {
                    hotkeyListener.Start(this, UpdateHotkeyStatusLabel, Log);
                }

                if (launchGame)
                {
                    await ChromeTurboLauncher.LaunchAsync(targetUrl, Log);
                }

                Log("=== OTIMIZAÇÃO CONCLUÍDA COM SUCESSO! ===", Color.FromArgb(0, 230, 118));
            }
            catch (Exception ex)
            {
                Log($"[ERRO] {ex.Message}", Color.Red);
            }
            finally
            {
                btnTurboLaunch.Enabled = true;
                btnOptimizeOnly.Enabled = true;
            }
        }

        private string ResolveTargetUrl()
        {
            if (rbBo1z.Checked)
                return "https://vel.gg/bo1z";
            if (rbBsz.Checked)
                return "https://vel.gg/bsz";

            string raw = txtInviteUrl.Text.Trim();
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                raw = "https://" + raw;
            }
            return raw;
        }

        private void UpdateHotkeyStatusLabel(bool active)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateHotkeyStatusLabel(active)));
                return;
            }

            lblHotkeyStatus.Text = active
                ? "Hotkeys: [ATIVO] -> F1: /god | F2: /sf_use_ignoreammo 1 | F3: /give all | F4: /noclip | F5: /notarget | END: Parar"
                : "Hotkeys: [INATIVO] (Pressione Otimizar com a opção marcada para reativar)";
            lblHotkeyStatus.ForeColor = active ? Color.FromArgb(0, 230, 118) : Color.FromArgb(160, 165, 185);
        }

        private void Log(string message, Color color)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Log(message, color)));
                return;
            }

            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = color;
            rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            rtbLog.SelectionColor = rtbLog.ForeColor;
            rtbLog.ScrollToCaret();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            hotkeyListener.Dispose();
            ramTimer?.Stop();
            base.OnFormClosing(e);
        }
    }
}
