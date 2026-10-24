using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using VelGG_TurboBooster.Modules;

namespace VelGG_TurboBooster
{
    /// <summary>
    /// Interface Gráfica Principal V2 - Tema Call of Duty: Black Ops Zombies (Group 935 / Element 115).
    /// Layout guiado em Passos (Passo 1 -> Passo 2 -> Passo 3) para máxima clareza do usuário.
    /// </summary>
    public class MainForm : Form
    {
        // Paleta Call of Duty: Black Ops Zombies
        private static readonly Color BgBunkerDark = Color.FromArgb(11, 10, 10);
        private static readonly Color BgPanelDark = Color.FromArgb(18, 16, 16);
        private static readonly Color BgCardDark = Color.FromArgb(24, 21, 20);
        private static readonly Color ZombiesBloodRed = Color.FromArgb(198, 40, 40);
        private static readonly Color ZombiesCrimsonGlow = Color.FromArgb(255, 82, 82);
        private static readonly Color ZombiesAmberGold = Color.FromArgb(255, 179, 0);
        private static readonly Color PerkGreen = Color.FromArgb(0, 230, 118);
        private static readonly Color Element115Cyan = Color.FromArgb(0, 229, 255);
        private static readonly Color TextMuted = Color.FromArgb(175, 170, 165);

        // Monitor de RAM (HUD Topo)
        private Label lblRamBefore = null!;
        private Label lblRamNow = null!;
        private Label lblRamRecovered = null!;
        private ProgressBar pbRamLoad = null!;

        // Botões Seletores de Modo (Substitui abas brancas padrão do Windows por Cards estilo Menu COD)
        private Button btnTabCloud = null!;
        private Button btnTabLocal = null!;
        private Panel pnlCloudView = null!;
        private Panel pnlLocalView = null!;

        // Modo 1: Jogar Online (Vel.GG Cloud)
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

        // Modo 2: Engine Local & Mod Builder (MisaDev4 BO1)
        private TextBox txtLocalRepoPath = null!;
        private Label lblRepoStatus = null!;
        private Button btnDownloadRepo = null!;

        private CheckBox chkEngineFpsPatch = null!;
        private CheckBox chkModMaxScore = null!;
        private CheckBox chkModScore10x = null!;
        private CheckBox chkModUnlimitedPerks = null!;
        private CheckBox chkModBox10Pts = null!;
        private CheckBox chkModSuperAimAssist = null!;

        private Button btnApplyModsOnly = null!;
        private Button btnRestoreBackup = null!;
        private Button btnLaunchLocalEngine = null!;

        // HUD de Hotkeys & Console Terminal
        private Label lblHotkeyStatus = null!;
        private RichTextBox rtbLog = null!;

        private System.Windows.Forms.Timer ramTimer = null!;
        private double initialFreeRamGb;
        private double lastRecoveredGb;

        private readonly HotkeyConsoleListener hotkeyListener = new();

        public MainForm()
        {
            InitializeComponent();
            CaptureInitialRam();
            StartRamMonitor();
            UpdateLocalRepoStatusBadge();
            SwitchMode(isLocalEngineMode: false);

            Log("☣ TERMINAL GROUP 935 INICIALIZADO [MODO ADMINISTRADOR ATIVO].", ZombiesAmberGold);
            Log("Escolha acima entre [1. JOGAR ONLINE VEL.GG] ou [2. ENGINE LOCAL + MOD MENU] e siga os 3 passos.", PerkGreen);
        }

        private void InitializeComponent()
        {
            Text = "Call of Duty: BO1 Zombies // VelGG TurboBooster V2 & GSC Mod Menu";
            Size = new Size(840, 860);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = BgBunkerDark;
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

            // =========================================================================
            // 1. BANNER SUPERIOR ESTILO CALL OF DUTY: BLACK OPS ZOMBIES
            // =========================================================================
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = Color.FromArgb(16, 12, 12)
            };
            pnlHeader.Paint += (_, e) =>
            {
                using var pen = new Pen(ZombiesBloodRed, 2);
                e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
            };

            // Marcações de Round Zombies (IIII)
            var lblRoundTally = new Label
            {
                Text = "||||",
                Font = new Font("Consolas", 24F, FontStyle.Bold),
                ForeColor = ZombiesBloodRed,
                Location = new Point(16, 14),
                AutoSize = true
            };

            var lblTitle = new Label
            {
                Text = "CALL OF DUTY: ZOMBIES // TURBOBOOSTER V2",
                Font = new Font("Segoe UI", 15.5F, FontStyle.Bold),
                ForeColor = ZombiesAmberGold,
                Location = new Point(88, 10),
                AutoSize = true
            };

            var lblSubtitle = new Label
            {
                Text = "GROUP 935 TACTICAL LAUNCHER  •  OTIMIZADOR DE KERNEL/RAM/REDE  •  MOD MENU (.GSC) & ENGINE LOCAL",
                Font = new Font("Consolas", 8.5F, FontStyle.Bold),
                ForeColor = TextMuted,
                Location = new Point(90, 44),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblRoundTally);
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            Controls.Add(pnlHeader);

            // =========================================================================
            // 2. HUD DE MEMÓRIA FÍSICA (BARRA TÁTICA NO TOPO)
            // =========================================================================
            var grpRam = CreateZombiesGroupBox("☣ TELEMETRIA DE MEMÓRIA RAM EM TEMPO REAL (PSAPI.DLL)", new Point(18, 84), new Size(788, 82), ZombiesAmberGold);

            lblRamBefore = new Label
            {
                Text = "RAM Inicial: -- GB",
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 183, 77),
                Location = new Point(16, 24),
                AutoSize = true
            };

            lblRamNow = new Label
            {
                Text = "RAM Livre Agora: -- GB",
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                ForeColor = PerkGreen,
                Location = new Point(260, 24),
                AutoSize = true
            };

            lblRamRecovered = new Label
            {
                Text = "RAM Liberada pelo Booster: +0.00 GB",
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                ForeColor = Element115Cyan,
                Location = new Point(515, 24),
                AutoSize = true
            };

            pbRamLoad = new ProgressBar
            {
                Location = new Point(16, 50),
                Size = new Size(755, 18),
                Style = ProgressBarStyle.Continuous
            };

            grpRam.Controls.Add(lblRamBefore);
            grpRam.Controls.Add(lblRamNow);
            grpRam.Controls.Add(lblRamRecovered);
            grpRam.Controls.Add(pbRamLoad);
            Controls.Add(grpRam);

            // =========================================================================
            // 3. SELETOR DE MODO PRINCIPAL (2 BOTÕES GRANDES ESTILO MENU COD ZOMBIES)
            // =========================================================================
            var lblChooseMode = new Label
            {
                Text = "▼ SELECIONE COMO VOCÊ QUER JOGAR HOJE:",
                Font = new Font("Consolas", 9.5F, FontStyle.Bold),
                ForeColor = ZombiesAmberGold,
                Location = new Point(18, 173),
                AutoSize = true
            };
            Controls.Add(lblChooseMode);

            btnTabCloud = new Button
            {
                Text = "🌐 MODO 1: JOGAR ONLINE (VEL.GG CLOUD)\r\nSem baixar nada • Otimiza PC + Abre Jogo Rápido",
                Location = new Point(18, 194),
                Size = new Size(388, 52),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTabCloud.FlatAppearance.BorderSize = 2;
            btnTabCloud.Click += (_, _) => SwitchMode(isLocalEngineMode: false);

            btnTabLocal = new Button
            {
                Text = "☣ MODO 2: ENGINE LOCAL + MOD MENU (.GSC)\r\nRoda do SSD (Menos RAM) • +60 FPS Patch • 999k Pontos & Perks",
                Location = new Point(418, 194),
                Size = new Size(388, 52),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTabLocal.FlatAppearance.BorderSize = 2;
            btnTabLocal.Click += (_, _) => SwitchMode(isLocalEngineMode: true);

            Controls.Add(btnTabCloud);
            Controls.Add(btnTabLocal);

            // =========================================================================
            // 4. PAINÉIS DE CONTEÚDO DOS DOIS MODOS (PASSO 1 -> PASSO 2 -> PASSO 3)
            // =========================================================================
            pnlCloudView = new Panel
            {
                Location = new Point(18, 254),
                Size = new Size(788, 365),
                BackColor = BgPanelDark
            };

            pnlLocalView = new Panel
            {
                Location = new Point(18, 254),
                Size = new Size(788, 365),
                BackColor = BgPanelDark,
                Visible = false
            };

            BuildCloudModePanel(pnlCloudView);
            BuildLocalEngineModePanel(pnlLocalView);

            Controls.Add(pnlCloudView);
            Controls.Add(pnlLocalView);

            // =========================================================================
            // 5. BARRA TÁTICA DE ATALHOS (CHEATS EM PARTIDA) + LOG TERMINAL
            // =========================================================================
            var pnlHotkeysBar = new Panel
            {
                Location = new Point(18, 625),
                Size = new Size(788, 30),
                BackColor = Color.FromArgb(26, 18, 18)
            };
            pnlHotkeysBar.Paint += (_, e) =>
            {
                using var pen = new Pen( Color.FromArgb(90, 35, 35), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlHotkeysBar.Width - 1, pnlHotkeysBar.Height - 1);
            };

            lblHotkeyStatus = new Label
            {
                Text = "🎮 CHEATS NA PARTIDA [INATIVO]:  [F1] God Mode  |  [F2] Munição Infinita  |  [F3] Todas Armas  |  [F4] Atravessar Parede  |  [F5] Invisível  |  [F6] FOV 90 + Speed  |  [END] Desligar",
                Location = new Point(8, 7),
                Size = new Size(772, 18),
                Font = new Font("Consolas", 8.25F, FontStyle.Bold),
                ForeColor = ZombiesAmberGold
            };
            pnlHotkeysBar.Controls.Add(lblHotkeyStatus);
            Controls.Add(pnlHotkeysBar);

            rtbLog = new RichTextBox
            {
                Location = new Point(18, 660),
                Size = new Size(788, 145),
                BackColor = Color.FromArgb(8, 8, 10),
                ForeColor = PerkGreen,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                ReadOnly = true
            };
            Controls.Add(rtbLog);
        }

        private void SwitchMode(bool isLocalEngineMode)
        {
            pnlCloudView.Visible = !isLocalEngineMode;
            pnlLocalView.Visible = isLocalEngineMode;

            if (!isLocalEngineMode)
            {
                btnTabCloud.BackColor = Color.FromArgb(48, 18, 18);
                btnTabCloud.ForeColor = ZombiesAmberGold;
                btnTabCloud.FlatAppearance.BorderColor = ZombiesCrimsonGlow;

                btnTabLocal.BackColor = BgCardDark;
                btnTabLocal.ForeColor = TextMuted;
                btnTabLocal.FlatAppearance.BorderColor = Color.FromArgb(55, 50, 50);
            }
            else
            {
                btnTabLocal.BackColor = Color.FromArgb(48, 18, 18);
                btnTabLocal.ForeColor = ZombiesAmberGold;
                btnTabLocal.FlatAppearance.BorderColor = ZombiesCrimsonGlow;

                btnTabCloud.BackColor = BgCardDark;
                btnTabCloud.ForeColor = TextMuted;
                btnTabCloud.FlatAppearance.BorderColor = Color.FromArgb(55, 50, 50);

                UpdateLocalRepoStatusBadge();
            }
        }

        private void BuildCloudModePanel(Panel container)
        {
            // PASSO 1: ESCOLHER O JOGO / SALA
            var grpStep1 = CreateZombiesGroupBox(
                "PASSO 1 — ESCOLHA QUAL ZOMBIES VOCÊ VAI JOGAR:",
                new Point(10, 6),
                new Size(768, 118),
                ZombiesCrimsonGlow);

            rbBo1z = new RadioButton
            {
                Text = "🎯 Call of Duty: Black Ops 1 Zombies — Jogar Solo ou Criar Sala Co-op  (vel.gg/bo1z)",
                Location = new Point(18, 26),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            rbBsz = new RadioButton
            {
                Text = "🧟 Call of Duty: BSZ Zombies — Jogar Solo ou Criar Sala Co-op  (vel.gg/bsz)",
                Location = new Point(18, 54),
                AutoSize = true,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };

            rbMultiplayer = new RadioButton
            {
                Text = "🤝 Entrar na Sala de um Amigo (Cole o link do Lobby ao lado):",
                Location = new Point(18, 82),
                AutoSize = true,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };

            txtInviteUrl = new TextBox
            {
                Location = new Point(415, 80),
                Size = new Size(334, 25),
                BackColor = Color.FromArgb(28, 24, 24),
                ForeColor = ZombiesAmberGold,
                BorderStyle = BorderStyle.FixedSingle,
                Enabled = false,
                PlaceholderText = "Ex: https://vel.gg/bo1z?lobby=...",
                Font = new Font("Consolas", 9.5F, FontStyle.Regular)
            };

            rbMultiplayer.CheckedChanged += (_, _) =>
            {
                txtInviteUrl.Enabled = rbMultiplayer.Checked;
                if (rbMultiplayer.Checked)
                    txtInviteUrl.Focus();
            };

            grpStep1.Controls.Add(rbBo1z);
            grpStep1.Controls.Add(rbBsz);
            grpStep1.Controls.Add(rbMultiplayer);
            grpStep1.Controls.Add(txtInviteUrl);
            container.Controls.Add(grpStep1);

            // PASSO 2: OTIMIZAÇÕES DO SISTEMA
            var grpStep2 = CreateZombiesGroupBox(
                "PASSO 2 — MARQUE AS OTIMIZAÇÕES DE FPS, REDE E TRAPAÇAS (RECOMENDADO DEIXAR TUDO MARCADO):",
                new Point(10, 132),
                new Size(768, 148),
                ZombiesAmberGold);

            chkCleanRamAndProcs = new CheckBox
            {
                Text = "🧹 Limpeza Extrema de Memória RAM + Fechar Apps Pesados (Riot Vanguard, Lively Wallpaper, Rainmeter, Edge, etc.)",
                Location = new Point(18, 28),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Regular)
            };

            chkNetworkOpt = new CheckBox
            {
                Text = "🌐 Reduzir Ping no Co-op (Ativar Google DNS 8.8.8.8 + Pilha CTCP + Desativar Algoritmo de Nagle)",
                Location = new Point(18, 56),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Regular)
            };

            chkCpuGpuKernel = new CheckBox
            {
                Text = "⚡ Destravar Processador (100% Clock sem Core Parking) + Placa de Vídeo em Alto Desempenho (HAGS + Sem EcoQoS)",
                Location = new Point(18, 84),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Regular)
            };

            chkHotkeys = new CheckBox
            {
                Text = "🎮 Ativar Teclas de Trapaça / Console na Partida (F1: Vida Infinita | F2: Munição | F3: Armas | F4: Noclip | F5: Invisível | F6: FOV 90)",
                Location = new Point(18, 112),
                AutoSize = true,
                Checked = true,
                ForeColor = ZombiesAmberGold,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold)
            };

            grpStep2.Controls.Add(chkCleanRamAndProcs);
            grpStep2.Controls.Add(chkNetworkOpt);
            grpStep2.Controls.Add(chkCpuGpuKernel);
            grpStep2.Controls.Add(chkHotkeys);
            container.Controls.Add(grpStep2);

            // PASSO 3: BOTÃO GIGANTE DE INICIAR
            btnTurboLaunch = new Button
            {
                Text = "☣ PASSO 3: OTIMIZAR TUDO E INICIAR ZOMBIES AGORA (MODO TURBO 8GB RAM)",
                Location = new Point(10, 292),
                Size = new Size(555, 60),
                BackColor = ZombiesBloodRed,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTurboLaunch.FlatAppearance.BorderColor = ZombiesAmberGold;
            btnTurboLaunch.FlatAppearance.BorderSize = 2;
            btnTurboLaunch.Click += async (_, _) => await ExecuteBoosterAsync(launchGame: true);

            btnOptimizeOnly = new Button
            {
                Text = "🔧 Só Otimizar meu PC\r\n(Sem abrir o jogo)",
                Location = new Point(575, 292),
                Size = new Size(203, 60),
                BackColor = BgCardDark,
                ForeColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOptimizeOnly.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 100);
            btnOptimizeOnly.Click += async (_, _) => await ExecuteBoosterAsync(launchGame: false);

            container.Controls.Add(btnTurboLaunch);
            container.Controls.Add(btnOptimizeOnly);
        }

        private void BuildLocalEngineModePanel(Panel container)
        {
            // PASSO 1: BAIXAR / SINCRONIZAR O JOGO NO SSD
            var grpRepo = CreateZombiesGroupBox(
                "PASSO 1 — BAIXAR OU ATUALIZAR A ENGINE LOCAL NO SEU SSD (SÓ PRECISA BAIXAR 1 VEZ):",
                new Point(10, 6),
                new Size(768, 74),
                Element115Cyan);

            var lblPath = new Label
            {
                Text = "Pasta no SSD:",
                Location = new Point(14, 32),
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            txtLocalRepoPath = new TextBox
            {
                Text = EngineModPatcher.DefaultLocalPath,
                Location = new Point(98, 29),
                Size = new Size(235, 25),
                BackColor = Color.FromArgb(26, 24, 24),
                ForeColor = Element115Cyan,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F, FontStyle.Regular)
            };
            txtLocalRepoPath.TextChanged += (_, _) => UpdateLocalRepoStatusBadge();

            lblRepoStatus = new Label
            {
                Text = "[VERIFICANDO...]",
                Location = new Point(340, 32),
                Size = new Size(135, 20),
                Font = new Font("Consolas", 8.5F, FontStyle.Bold),
                ForeColor = ZombiesAmberGold
            };

            btnDownloadRepo = new Button
            {
                Text = "📥 1. Baixar / Atualizar Jogo Local (GitHub)",
                Location = new Point(480, 23),
                Size = new Size(274, 36),
                BackColor = Color.FromArgb(0, 120, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDownloadRepo.FlatAppearance.BorderSize = 0;
            btnDownloadRepo.Click += async (_, _) => await DownloadOrUpdateLocalEngineAsync();

            grpRepo.Controls.Add(lblPath);
            grpRepo.Controls.Add(txtLocalRepoPath);
            grpRepo.Controls.Add(lblRepoStatus);
            grpRepo.Controls.Add(btnDownloadRepo);
            container.Controls.Add(grpRepo);

            // PASSO 2: MOD MENU (.GSC) + PATCH DE 60+ FPS
            var grpMods = CreateZombiesGroupBox(
                "PASSO 2 — ESCOLHA OS MODS (.GSC) E O PATCH DE FPS PARA INJETAR NO JOGO:",
                new Point(10, 86),
                new Size(768, 204),
                ZombiesAmberGold);

            chkEngineFpsPatch = new CheckBox
            {
                Text = "🚀 PATCH EXTREMO 60+ FPS: Remover Neblina (Fog), Desfoque (DoF), Brilho (Bloom), Sombras, Cápsulas e Destravar 125 FPS",
                Location = new Point(16, 25),
                AutoSize = true,
                Checked = true,
                ForeColor = PerkGreen,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            chkModMaxScore = new CheckBox
            {
                Text = "💰 Dinheiro Inicial Máximo: Começar a partida já no Round 1 com 999.999 Pontos (self.score = 999999)",
                Location = new Point(16, 53),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            chkModScore10x = new CheckBox
            {
                Text = "📈 Multiplicador de Pontos 10x: Ganhar 10 vezes mais pontos por cada tiro e zumbi eliminado",
                Location = new Point(16, 81),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            chkModUnlimitedPerks = new CheckBox
            {
                Text = "🍺 Sem Limite de 4 Perks: Permitir comprar TODAS as máquinas de Perk-a-Cola do mapa ao mesmo tempo",
                Location = new Point(16, 109),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            chkModBox10Pts = new CheckBox
            {
                Text = "🎁 Caixa Misteriosa por 10 Pontos: Reduzir o preço da Mystery Box de 950 para apenas 10 pontos",
                Location = new Point(16, 137),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            chkModSuperAimAssist = new CheckBox
            {
                Text = "🎯 Super Assistência de Mira (Auto-Aim / Snap to Head): Puxa a mira para a cabeça do Zumbi ao segurar o botão direito (ADS)",
                Location = new Point(16, 165),
                AutoSize = true,
                Checked = true,
                ForeColor = ZombiesAmberGold,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            grpMods.Controls.Add(chkEngineFpsPatch);
            grpMods.Controls.Add(chkModMaxScore);
            grpMods.Controls.Add(chkModScore10x);
            grpMods.Controls.Add(chkModUnlimitedPerks);
            grpMods.Controls.Add(chkModBox10Pts);
            grpMods.Controls.Add(chkModSuperAimAssist);
            container.Controls.Add(grpMods);

            // PASSO 3: APLICAR MODS E JOGAR DIRETO DO SSD
            btnLaunchLocalEngine = new Button
            {
                Text = "☣ PASSO 3: APLICAR MODS E INICIAR JOGO LOCAL (BAIXO USO DE RAM)",
                Location = new Point(10, 298),
                Size = new Size(440, 56),
                BackColor = ZombiesBloodRed,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnLaunchLocalEngine.FlatAppearance.BorderColor = ZombiesAmberGold;
            btnLaunchLocalEngine.FlatAppearance.BorderSize = 2;
            btnLaunchLocalEngine.Click += async (_, _) => await LaunchLocalEngineGameAsync();

            btnApplyModsOnly = new Button
            {
                Text = "⚙️ Só Injetar Mods\r\n(Sem abrir o jogo)",
                Location = new Point(458, 298),
                Size = new Size(155, 56),
                BackColor = BgCardDark,
                ForeColor = Element115Cyan,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.75F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnApplyModsOnly.FlatAppearance.BorderColor = Color.FromArgb(0, 140, 180);
            btnApplyModsOnly.Click += async (_, _) => await ApplyEnginePatchesAndModsAsync();

            btnRestoreBackup = new Button
            {
                Text = "♻️ Restaurar Original\r\n(Remover Mods)",
                Location = new Point(621, 298),
                Size = new Size(157, 56),
                BackColor = Color.FromArgb(42, 22, 22),
                ForeColor = Color.FromArgb(255, 138, 128),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.75F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRestoreBackup.FlatAppearance.BorderColor = Color.FromArgb(160, 60, 60);
            btnRestoreBackup.Click += (_, _) =>
            {
                string repoPath = txtLocalRepoPath.Text.Trim();
                EngineModPatcher.RestoreBackups(repoPath, Log);
                UpdateLocalRepoStatusBadge();
            };

            container.Controls.Add(btnLaunchLocalEngine);
            container.Controls.Add(btnApplyModsOnly);
            container.Controls.Add(btnRestoreBackup);
        }

        private void UpdateLocalRepoStatusBadge()
        {
            if (lblRepoStatus == null || txtLocalRepoPath == null)
                return;

            string path = txtLocalRepoPath.Text.Trim();
            if (Directory.Exists(path) && Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length > 0)
            {
                lblRepoStatus.Text = "✔ PRONTO NO SSD";
                lblRepoStatus.ForeColor = PerkGreen;
            }
            else
            {
                lblRepoStatus.Text = "⚠ CLIQUE EM BAIXAR";
                lblRepoStatus.ForeColor = ZombiesAmberGold;
            }
        }

        private static GroupBox CreateZombiesGroupBox(string title, Point location, Size size, Color titleColor)
        {
            return new GroupBox
            {
                Text = title,
                Location = location,
                Size = size,
                BackColor = BgCardDark,
                ForeColor = titleColor,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold)
            };
        }

        private void CaptureInitialRam()
        {
            var (freeGb, totalGb, load) = MemoryFlushModule.GetMemoryStatus();
            initialFreeRamGb = freeGb;
            lblRamBefore.Text = $"RAM Inicial: {initialFreeRamGb:F2} GB";
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
            lblRamNow.Text = $"RAM Livre Agora: {freeGb:F2} / {totalGb:F1} GB";
            double diff = Math.Max(0, freeGb - initialFreeRamGb);
            if (diff > lastRecoveredGb)
                lastRecoveredGb = diff;

            lblRamRecovered.Text = $"RAM Liberada: +{Math.Max(diff, lastRecoveredGb):F2} GB";
            pbRamLoad.Value = (int)Math.Clamp(load, 0, 100);
        }

        private EngineModOptions CollectModOptions()
        {
            return new EngineModOptions
            {
                ApplyFpsPatch = chkEngineFpsPatch.Checked,
                MaxStartingScore999999 = chkModMaxScore.Checked,
                ScoreMultiplier10x = chkModScore10x.Checked,
                UnlimitedPerks = chkModUnlimitedPerks.Checked,
                MysteryBox10Points = chkModBox10Pts.Checked,
                SuperAimAssistSnapHead = chkModSuperAimAssist.Checked
            };
        }

        private async Task DownloadOrUpdateLocalEngineAsync()
        {
            SetButtonsEnabled(false);
            try
            {
                string repoPath = txtLocalRepoPath.Text.Trim();
                await Task.Run(() => EngineModPatcher.DownloadOrUpdateRepoAsync(repoPath, Log));
                UpdateLocalRepoStatusBadge();
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private async Task ApplyEnginePatchesAndModsAsync()
        {
            SetButtonsEnabled(false);
            try
            {
                string repoPath = txtLocalRepoPath.Text.Trim();
                var options = CollectModOptions();
                await Task.Run(() => EngineModPatcher.ApplyMods(repoPath, options, Log));
                UpdateLocalRepoStatusBadge();
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private async Task LaunchLocalEngineGameAsync()
        {
            SetButtonsEnabled(false);
            try
            {
                string repoPath = txtLocalRepoPath.Text.Trim();
                var options = CollectModOptions();

                Log("=== ☣ INICIANDO ENGINE LOCAL + MOD BUILDER + SERVIDOR SSD (PORTA 8080) ===", ZombiesAmberGold);

                // 1. Aplica Patch Extremo de FPS e Mods selecionados nos arquivos locais
                await Task.Run(() => EngineModPatcher.ApplyMods(repoPath, options, Log));
                UpdateLocalRepoStatusBadge();

                // 2. Executa otimizações de RAM, CPU/GPU e Rede marcadas
                await RunSystemOptimizationsAsync();

                // 3. Sobe o HttpListener local na porta 8080 com COOP/COEP (SharedArrayBuffer)
                string localServerUrl = EngineModPatcher.StartLocalServer(repoPath, Log, EngineModPatcher.DefaultLocalPort);

                // 4. Ativa o Listener de Hotkeys F1 a F6
                if (chkHotkeys.Checked)
                {
                    hotkeyListener.Start(this, UpdateHotkeyStatusLabel, Log);
                }

                // 5. Lança o Custom Chrome Launcher com 8GB V8 Heap apontando para http://localhost:8080/
                await ChromeTurboLauncher.LaunchAsync(localServerUrl, Log);

                Log("=== ☣ ENGINE LOCAL EM EXECUÇÃO! STREAMING DIRETO DO SSD ATIVO ===", PerkGreen);
            }
            catch (Exception ex)
            {
                Log($"[ERRO Engine Local] {ex.Message}", Color.Red);
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private async Task ExecuteBoosterAsync(bool launchGame)
        {
            string targetUrl = ResolveTargetUrl();
            if (launchGame && string.IsNullOrWhiteSpace(targetUrl))
            {
                MessageBox.Show("Cole o link do Lobby Multiplayer enviado pelo seu amigo no Passo 1!", "Link do Lobby Vazio",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetButtonsEnabled(false);

            try
            {
                Log("=== ☣ INICIANDO PROTOCOLO DE OTIMIZAÇÃO TURBO (GROUP 935) ===", ZombiesAmberGold);

                await RunSystemOptimizationsAsync();

                if (chkHotkeys.Checked)
                {
                    hotkeyListener.Start(this, UpdateHotkeyStatusLabel, Log);
                }

                if (launchGame)
                {
                    await ChromeTurboLauncher.LaunchAsync(targetUrl, Log);
                }

                Log("=== ☣ OTIMIZAÇÃO CONCLUÍDA! BOA CAÇADA AOS ZUMBIS! ===", PerkGreen);
            }
            catch (Exception ex)
            {
                Log($"[ERRO] {ex.Message}", Color.Red);
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private async Task RunSystemOptimizationsAsync()
        {
            var (beforeFree, _, _) = MemoryFlushModule.GetMemoryStatus();
            initialFreeRamGb = beforeFree;
            lblRamBefore.Text = $"RAM Inicial: {initialFreeRamGb:F2} GB";

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
            Log($"[RAM] Memória física livre agora: {afterFree:F2} GB (Recuperado: +{recovered:F2} GB).", Element115Cyan);
        }

        private void SetButtonsEnabled(bool enabled)
        {
            btnTurboLaunch.Enabled = enabled;
            btnOptimizeOnly.Enabled = enabled;
            btnDownloadRepo.Enabled = enabled;
            btnApplyModsOnly.Enabled = enabled;
            btnRestoreBackup.Enabled = enabled;
            btnLaunchLocalEngine.Enabled = enabled;
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
                ? "🎮 CHEATS NA PARTIDA [ATIVO]:  [F1] God Mode  |  [F2] Munição Infinita  |  [F3] Todas Armas  |  [F4] Noclip  |  [F5] Invisível  |  [F6] FOV 90 + Speed  |  [END] Parar"
                : "🎮 CHEATS NA PARTIDA [INATIVO]:  Marque a opção de Trapaças no Passo 2 e inicie para ativar os atalhos F1 a F6";
            lblHotkeyStatus.ForeColor = active ? PerkGreen : ZombiesAmberGold;
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
            EngineModPatcher.StopLocalServer();
            ramTimer?.Stop();
            base.OnFormClosing(e);
        }
    }
}
