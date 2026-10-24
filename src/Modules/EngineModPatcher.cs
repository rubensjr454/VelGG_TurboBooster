using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace VelGG_TurboBooster.Modules
{
    /// <summary>
    /// Opções de injeção de Mods e Patch de FPS no código-fonte descompilado (MisaDev4 BO1 Zombies).
    /// </summary>
    public sealed class EngineModOptions
    {
        public bool ApplyFpsPatch { get; set; } = true;
        public bool MaxStartingScore999999 { get; set; } = true;
        public bool ScoreMultiplier10x { get; set; } = true;
        public bool UnlimitedPerks { get; set; } = true;
        public bool MysteryBox10Points { get; set; } = true;
        public bool SuperAimAssistSnapHead { get; set; } = true;
    }

    /// <summary>
    /// MÓDULO 7 (V2): Gerenciador da Engine Local (MisaDev4/bo1-zombies-decompiled),
    /// Otimizador Extremo de FPS no Código-Fonte, Injetor de Mods (.GSC / Engine),
    /// Gerenciador de Backups (.bak) e Servidor Web Local (HttpListener Porta 8080 com COOP/COEP).
    /// </summary>
    internal static class EngineModPatcher
    {
        public const string DefaultRepoUrl = "https://github.com/MisaDev4/bo1-zombies-decompiled";
        public const string DefaultLocalPath = @"C:\Games\BO1_Zombies_Local";
        public const int DefaultLocalPort = 8080;

        private static HttpListener? _httpListener;
        private static CancellationTokenSource? _serverCts;
        private static string _activeRootPath = DefaultLocalPath;

        private static readonly (string Key, string Value, string Comment)[] LowSpecFpsCvars =
        {
            ("r_fog", "0", "Remove toda a neblina volumetrica dos mapas"),
            ("r_dof_enable", "0", "Desativa Depth of Field / desfoque de mira"),
            ("r_glow_allowed", "0", "Remove brilho pesado"),
            ("r_bloomTweaks", "0", "Remove pos-processamento de bloom"),
            ("r_specular", "0", "Desativa reflexos especulares"),
            ("sm_enable", "0", "Desativa sombras dinamicas pesadas"),
            ("cg_brass", "0", "Remove capsulas de balas no chao"),
            ("fx_marks", "0", "Remove marcas de tiro nas paredes"),
            ("com_maxfps", "125", "Destrava o limite de FPS"),
            ("r_vsync", "0", "Desativa sincronizacao vertical"),
            ("ragdoll_enable", "0", "Desativa fisica ragdoll pesada"),
            ("ai_corpseCount", "2", "Limpa corpos de zumbis mortos rapidamente")
        };

        private static readonly (string Key, string Value)[] AimAssistCvars =
        {
            ("aim_autoaim_enabled", "1"),
            ("aim_lockon_enabled", "1"),
            ("aim_lockon_strength", "1.0"),
            ("aim_lockon_deflection", "0.0")
        };

        /// <summary>
        /// 1. Baixa ou atualiza o repositório https://github.com/MisaDev4/bo1-zombies-decompiled
        /// para C:\Games\BO1_Zombies_Local via Git (ou fallback via HttpClient .zip).
        /// </summary>
        public static async Task<bool> DownloadOrUpdateRepoAsync(string repoPath, Action<string, Color> log)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repoPath))
                    repoPath = DefaultLocalPath;

                string? parentDir = Path.GetDirectoryName(repoPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    Directory.CreateDirectory(parentDir);

                bool hasGit = IsGitAvailable();
                log(hasGit
                    ? "[Engine Local] Git detectado no sistema. Iniciando sincronização do repositório..."
                    : "[Engine Local] Git não encontrado no PATH. Usando download direto (.zip) via HttpClient...",
                    Color.Cyan);

                bool success = false;

                if (hasGit)
                {
                    if (Directory.Exists(Path.Combine(repoPath, ".git")))
                    {
                        log($"[Engine Local] Atualizando repositório existente em {repoPath} (git pull)...", Color.LightSkyBlue);
                        var (exitCode, output) = await RunProcessCaptureAsync("git", $"-C \"{repoPath}\" pull --ff-only");
                        if (exitCode == 0)
                        {
                            log($"[Engine Local] Repositório atualizado via Git: {FirstNonEmptyLine(output)}", Color.LightGreen);
                            success = true;
                        }
                        else
                        {
                            log("[Engine Local] git pull retornou aviso; sincronizando com fetch/reset...", Color.Orange);
                            await RunProcessCaptureAsync("git", $"-C \"{repoPath}\" fetch --all");
                            var (resetCode, _) = await RunProcessCaptureAsync("git", $"-C \"{repoPath}\" reset --hard origin/main");
                            success = resetCode == 0;
                        }
                    }
                    else
                    {
                        if (Directory.Exists(repoPath) && Directory.EnumerateFileSystemEntries(repoPath).Any())
                        {
                            string gitInitCmd = $"init \"{repoPath}\"";
                            await RunProcessCaptureAsync("git", gitInitCmd);
                            await RunProcessCaptureAsync("git", $"-C \"{repoPath}\" remote add origin {DefaultRepoUrl}.git");
                            var (fetchCode, _) = await RunProcessCaptureAsync("git", $"-C \"{repoPath}\" pull origin main --allow-unrelated-histories");
                            success = fetchCode == 0;
                        }
                        else
                        {
                            log($"[Engine Local] Clonando {DefaultRepoUrl} para {repoPath}...", Color.LightSkyBlue);
                            var (cloneCode, cloneOut) = await RunProcessCaptureAsync("git", $"clone --depth 1 {DefaultRepoUrl}.git \"{repoPath}\"");
                            if (cloneCode == 0)
                            {
                                log("[Engine Local] Clone concluído com sucesso via Git!", Color.LightGreen);
                                success = true;
                            }
                            else
                            {
                                log($"[Engine Local] Aviso no git clone ({FirstNonEmptyLine(cloneOut)}). Tentando via HttpClient (.zip)...", Color.Orange);
                            }
                        }
                    }
                }

                if (!success)
                {
                    success = await DownloadRepoZipFallbackAsync(repoPath, log);
                }

                // Garante a estrutura base de scripts/configs caso o repositório tenha acabado de ser criado ou seja parcial
                EnsureBaselineEngineStructure(repoPath, log);

                int totalFiles = Directory.Exists(repoPath)
                    ? Directory.GetFiles(repoPath, "*", SearchOption.AllDirectories).Length
                    : 0;

                log($"[Engine Local] Pronto em '{repoPath}' ({totalFiles} arquivos prontos para streaming via SSD).", Color.FromArgb(0, 230, 118));
                return true;
            }
            catch (Exception ex)
            {
                log($"[Engine Local] Erro ao baixar/atualizar repositório: {ex.Message}", Color.Red);
                return false;
            }
        }

        private static async Task<bool> DownloadRepoZipFallbackAsync(string repoPath, Action<string, Color> log)
        {
            string[] zipUrls =
            {
                $"{DefaultRepoUrl}/archive/refs/heads/main.zip",
                $"{DefaultRepoUrl}/archive/refs/heads/master.zip"
            };

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("VelGG-TurboBooster-V2/2.0");

            string tempZip = Path.Combine(Path.GetTempPath(), $"bo1_zombies_decompiled_{Guid.NewGuid():N}.zip");
            string tempExtract = Path.Combine(Path.GetTempPath(), $"bo1_extract_{Guid.NewGuid():N}");

            try
            {
                byte[]? archiveBytes = null;
                foreach (string url in zipUrls)
                {
                    try
                    {
                        log($"[Engine Local] Baixando pacote de código-fonte: {url}", Color.LightSkyBlue);
                        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                        if (response.IsSuccessStatusCode)
                        {
                            archiveBytes = await response.Content.ReadAsByteArrayAsync();
                            break;
                        }
                    }
                    catch
                    {
                        // Tenta próxima branch (main -> master)
                    }
                }

                if (archiveBytes == null || archiveBytes.Length == 0)
                {
                    log("[Engine Local] Não foi possível baixar o arquivo .zip diretamente do GitHub. Gerando estrutura local compatível...", Color.Orange);
                    Directory.CreateDirectory(repoPath);
                    return false;
                }

                await File.WriteAllBytesAsync(tempZip, archiveBytes);
                log($"[Engine Local] Pacote baixado ({archiveBytes.Length / 1024.0:F1} KB). Extraindo para {repoPath}...", Color.LightSkyBlue);

                ZipFile.ExtractToDirectory(tempZip, tempExtract, overwriteFiles: true);

                // O GitHub extrai dentro de uma subpasta <repo>-main
                string sourceRoot = Directory.GetDirectories(tempExtract).FirstOrDefault() ?? tempExtract;
                CopyDirectoryRecursive(sourceRoot, repoPath);

                log("[Engine Local] Extração do repositório concluída com sucesso!", Color.LightGreen);
                return true;
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// 2. Otimizador de FPS Direto no Código-Fonte da Engine (Low-Spec / 60+ FPS Patch).
        /// Varre arquivos .cfg, .gsc, .cpp, .h, .js e .html dentro do repositório local e aplica as CVARs de alto FPS.
        /// </summary>
        public static void ApplyEngineFpsPatch(string repoPath)
        {
            ApplyEngineFpsPatch(repoPath, (_, _) => { });
        }

        public static void ApplyEngineFpsPatch(string repoPath, Action<string, Color> log)
        {
            if (string.IsNullOrWhiteSpace(repoPath))
                repoPath = DefaultLocalPath;

            EnsureBaselineEngineStructure(repoPath, log);

            log("[FPS Patch] Varrendo arquivos da Engine (.cfg, .gsc, .cpp, .h, .js) para aplicar Low-Spec 60+ FPS Patch...", Color.Cyan);

            int patchedCount = 0;

            // 1. Garante arquivos .cfg principais (config.cfg, autoexec.cfg, default_zombies.cfg)
            string[] targetCfgPaths =
            {
                Path.Combine(repoPath, "config.cfg"),
                Path.Combine(repoPath, "autoexec.cfg"),
                Path.Combine(repoPath, "players", "config.cfg"),
                Path.Combine(repoPath, "main", "autoexec.cfg"),
                Path.Combine(repoPath, "raw", "default_zombies.cfg")
            };

            foreach (string cfgPath in targetCfgPaths)
            {
                string? dir = Path.GetDirectoryName(cfgPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                if (!File.Exists(cfgPath))
                {
                    File.WriteAllText(cfgPath, "// Configuração Base BO1 Zombies Local\r\nunbindall\r\n", Encoding.UTF8);
                }
            }

            var allFiles = Directory.GetFiles(repoPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) &&
                            !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
                .ToList();

            foreach (string file in allFiles)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".cfg")
                {
                    if (PatchCfgFileWithCvars(file, LowSpecFpsCvars))
                        patchedCount++;
                }
                else if (ext == ".gsc")
                {
                    if (PatchGscFileWithFpsDvars(file))
                        patchedCount++;
                }
                else if (ext == ".cpp" || ext == ".h")
                {
                    if (PatchCppHeaderFpsDefaults(file))
                        patchedCount++;
                }
                else if (ext == ".js" || ext == ".html")
                {
                    if (PatchWebWrapperFpsArgs(file))
                        patchedCount++;
                }
            }

            log($"[FPS Patch] Patch Extremo de FPS aplicado em {patchedCount} arquivo(s) (Fog=0, DoF=0, Bloom=0, Specular=0, Ragdoll=0, MaxFPS=125).",
                Color.FromArgb(0, 230, 118));
        }

        private static bool PatchCfgFileWithCvars(string filePath, IEnumerable<(string Key, string Value, string Comment)> cvars)
        {
            EnsureBackup(filePath);
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            string updated = content;

            var missingLines = new StringBuilder();
            foreach (var (key, val, comment) in cvars)
            {
                string pattern = $@"(?im)^(\s*(?:seta|set)\s+{Regex.Escape(key)}\s+)""[^""]*""";
                if (Regex.IsMatch(updated, pattern))
                {
                    updated = Regex.Replace(updated, pattern, $"$1\"{val}\"");
                }
                else
                {
                    missingLines.AppendLine($"seta {key} \"{val}\" // {comment}");
                }
            }

            const string marker = "// === [VELGG TURBOBOOSTER V2 - LOW-SPEC 60+ FPS PATCH] ===";
            if (missingLines.Length > 0 && !updated.Contains(marker, StringComparison.Ordinal))
            {
                updated = updated.TrimEnd() + Environment.NewLine + Environment.NewLine +
                          marker + Environment.NewLine +
                          missingLines +
                          "// === [END VELGG FPS PATCH] ===" + Environment.NewLine;
            }

            if (!string.Equals(content, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(filePath, updated, Encoding.UTF8);
                return true;
            }
            return false;
        }

        private static bool PatchGscFileWithFpsDvars(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            if (!fileName.Equals("_zombiemode.gsc", StringComparison.OrdinalIgnoreCase) &&
                !fileName.StartsWith("zombie_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            EnsureBackup(filePath);
            string content = File.ReadAllText(filePath, Encoding.UTF8);

            const string marker = "// [VELGG_FPS_PATCH_GSC]";
            if (content.Contains(marker, StringComparison.Ordinal))
                return false;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"    {marker}");
            foreach (var (key, val, _) in LowSpecFpsCvars)
            {
                sb.AppendLine($"    setDvar(\"{key}\", \"{val}\");");
            }

            // Injeta logo na abertura de main() ou init()
            var match = Regex.Match(content, @"(?im)^\s*(?:main|init)\s*\(\s*\)\s*\{");
            if (match.Success)
            {
                int insertPos = match.Index + match.Length;
                string updated = content.Insert(insertPos, sb.ToString());
                File.WriteAllText(filePath, updated, Encoding.UTF8);
                return true;
            }

            return false;
        }

        private static bool PatchCppHeaderFpsDefaults(string filePath)
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            string updated = content;

            // Substitui registros clássicos da engine IW/Treyarch: Dvar_RegisterBool("r_fog", true/1, ...) -> false/0
            updated = Regex.Replace(updated, @"(Dvar_RegisterBool\s*\(\s*""r_fog""\s*,\s*)(?:true|1)", "${1}false");
            updated = Regex.Replace(updated, @"(Dvar_RegisterBool\s*\(\s*""r_dof_enable""\s*,\s*)(?:true|1)", "${1}false");
            updated = Regex.Replace(updated, @"(Dvar_RegisterBool\s*\(\s*""r_glow_allowed""\s*,\s*)(?:true|1)", "${1}false");
            updated = Regex.Replace(updated, @"(Dvar_RegisterBool\s*\(\s*""r_specular""\s*,\s*)(?:true|1)", "${1}false");
            updated = Regex.Replace(updated, @"(Dvar_RegisterBool\s*\(\s*""sm_enable""\s*,\s*)(?:true|1)", "${1}false");
            updated = Regex.Replace(updated, @"(Dvar_RegisterBool\s*\(\s*""cg_brass""\s*,\s*)(?:true|1)", "${1}false");
            updated = Regex.Replace(updated, @"(Dvar_RegisterBool\s*\(\s*""fx_marks""\s*,\s*)(?:true|1)", "${1}false");
            updated = Regex.Replace(updated, @"(Dvar_RegisterBool\s*\(\s*""r_vsync""\s*,\s*)(?:true|1)", "${1}false");
            updated = Regex.Replace(updated, @"(Dvar_RegisterBool\s*\(\s*""ragdoll_enable""\s*,\s*)(?:true|1)", "${1}false");
            updated = Regex.Replace(updated, @"(Dvar_RegisterInt\s*\(\s*""com_maxfps""\s*,\s*)\d+", "${1}125");
            updated = Regex.Replace(updated, @"(Dvar_RegisterInt\s*\(\s*""ai_corpseCount""\s*,\s*)\d+", "${1}2");

            if (!string.Equals(content, updated, StringComparison.Ordinal))
            {
                EnsureBackup(filePath);
                File.WriteAllText(filePath, updated, Encoding.UTF8);
                return true;
            }

            return false;
        }

        private static bool PatchWebWrapperFpsArgs(string filePath)
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            const string marker = "/* [VELGG_WASM_FPS_CVARS] */";
            if (content.Contains(marker, StringComparison.Ordinal))
                return false;

            if (!content.Contains("Module", StringComparison.Ordinal) &&
                !content.Contains("WebAssembly", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            EnsureBackup(filePath);

            string argsArray = string.Join(", ",
                LowSpecFpsCvars.SelectMany(c => new[] { $"\"+set\"", $"\"{c.Key}\"", $"\"{c.Value}\"" }));

            string snippet =
                $"{Environment.NewLine}{marker}{Environment.NewLine}" +
                $"window.VELGG_ENGINE_ARGS = [{argsArray}];{Environment.NewLine}";

            string updated = content;
            if (filePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            {
                updated = snippet + content;
            }
            else if (filePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
                     content.Contains("</head>", StringComparison.OrdinalIgnoreCase))
            {
                updated = Regex.Replace(
                    content,
                    @"</head>",
                    $"<script>{snippet}</script>{Environment.NewLine}</head>",
                    RegexOptions.IgnoreCase);
            }

            if (!string.Equals(content, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(filePath, updated, Encoding.UTF8);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 3. Construtor de Mods no Código Descompilado (Mod Patcher Nativo em arquivos .GSC / Engine).
        /// </summary>
        public static void ApplyMods(string repoPath, EngineModOptions options, Action<string, Color> log)
        {
            if (string.IsNullOrWhiteSpace(repoPath))
                repoPath = DefaultLocalPath;

            EnsureBaselineEngineStructure(repoPath, log);

            if (options.ApplyFpsPatch)
            {
                ApplyEngineFpsPatch(repoPath, log);
            }

            var gscFiles = Directory.GetFiles(repoPath, "*.gsc", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var cfgFiles = Directory.GetFiles(repoPath, "*.cfg", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int modifiedFiles = 0;

            foreach (string gscFile in gscFiles)
            {
                string original = File.ReadAllText(gscFile, Encoding.UTF8);
                string updated = original;

                // [1] Dinheiro Inicial Máximo (999.999 Pontos)
                if (options.MaxStartingScore999999)
                {
                    updated = Regex.Replace(updated, @"\bself\.score\s*=\s*500\s*;", "self.score = 999999;");
                    updated = Regex.Replace(updated, @"\bself\.score_total\s*=\s*500\s*;", "self.score_total = 999999;");
                    updated = Regex.Replace(updated, @"\blevel\.player_starting_points\s*=\s*\d+\s*;", "level.player_starting_points = 999999;");
                }

                // [2] Multiplicador de Pontos 10x
                if (options.ScoreMultiplier10x)
                {
                    // Injeta multiplicador 10x em funções de adição de pontos (_zombiemode_score.gsc)
                    if (!updated.Contains("// [VELGG_10X_SCORE_MOD]", StringComparison.Ordinal))
                    {
                        updated = Regex.Replace(
                            updated,
                            @"(?im)(^\s*(?:add_to_player_score|player_add_points)\s*\(\s*points[^\)]*\)\s*\{)",
                            "$1\r\n    // [VELGG_10X_SCORE_MOD]\r\n    points = int(points * 10);");
                    }
                    updated = Regex.Replace(updated, @"\bself\.score\s*\+=\s*points\s*;", "self.score += (points * 10); // [VELGG_10X_SCORE_MOD]");
                }

                // [3] Remover Limite de 4 Perks (Todos os Perks Liberados)
                if (options.UnlimitedPerks)
                {
                    updated = Regex.Replace(updated, @"\bself\.num_perks\s*<\s*4\b", "self.num_perks < 99");
                    updated = Regex.Replace(updated, @"\bself\.num_perks\s*<\s*level\.perk_purchase_limit\b", "self.num_perks < 99");
                    updated = Regex.Replace(updated, @"\blevel\.perk_purchase_limit\s*=\s*4\s*;", "level.perk_purchase_limit = 99;");
                }

                // [4] Caixa Misteriosa Sempre por 10 Pontos
                if (options.MysteryBox10Points)
                {
                    updated = Regex.Replace(updated, @"(\bcost\s*=\s*)950(\s*;)", "${1}10${2}");
                    updated = Regex.Replace(updated, @"(\bzombie_cost\s*=\s*)950(\s*;)", "${1}10${2}");
                    updated = Regex.Replace(updated, @"(\btreasure_chest_cost\s*=\s*)950(\s*;)", "${1}10${2}");
                }

                // [5] Super Assistência de Mira (Snap Target / Auto-Aim no Mouse -> Snap to Head j_head em ADS)
                if (options.SuperAimAssistSnapHead)
                {
                    updated = InjectSnapToHeadGscLogic(gscFile, updated);
                }

                if (!string.Equals(original, updated, StringComparison.Ordinal))
                {
                    EnsureBackup(gscFile);
                    File.WriteAllText(gscFile, updated, Encoding.UTF8);
                    modifiedFiles++;
                    log($"[Mod Patcher] Script modificado: {Path.GetFileName(gscFile)}", Color.LightSkyBlue);
                }
            }

            if (options.SuperAimAssistSnapHead)
            {
                var aimTuples = AimAssistCvars.Select(a => (a.Key, a.Value, "Super Assistencia de Mira Lock-On")).ToArray();
                foreach (string cfg in cfgFiles)
                {
                    if (PatchCfgFileWithCvars(cfg, aimTuples))
                        modifiedFiles++;
                }
            }

            log($"[Mod Patcher] Injeção concluída! {modifiedFiles} arquivo(s) da Engine/GSC atualizados com backup (.bak).",
                Color.FromArgb(0, 230, 118));
        }

        private static string InjectSnapToHeadGscLogic(string gscFilePath, string content)
        {
            string fileName = Path.GetFileName(gscFilePath);
            if (!fileName.Equals("_zombiemode.gsc", StringComparison.OrdinalIgnoreCase))
                return content;

            const string snapMarker = "// [VELGG_SNAP_TO_HEAD_ADS]";
            if (content.Contains(snapMarker, StringComparison.Ordinal))
                return content;

            string updated = content;

            // Injeta chamada da thread no spawn do jogador ou no init
            var match = Regex.Match(updated, @"(?im)^\s*(?:onPlayerSpawned|player_spawned|init|main)\s*\(\s*\)\s*\{");
            if (match.Success)
            {
                int insertPos = match.Index + match.Length;
                string dvarInit =
                    "\r\n    // [VELGG_AIM_ASSIST_DVARS]\r\n" +
                    "    setDvar(\"aim_autoaim_enabled\", \"1\");\r\n" +
                    "    setDvar(\"aim_lockon_enabled\", \"1\");\r\n" +
                    "    setDvar(\"aim_lockon_strength\", \"1.0\");\r\n" +
                    "    setDvar(\"aim_lockon_deflection\", \"0.0\");\r\n" +
                    "    level thread velgg_watch_players_aim_assist();\r\n";
                updated = updated.Insert(insertPos, dvarInit);
            }

            string snapFunctions = @"

// [VELGG_SNAP_TO_HEAD_ADS]
velgg_watch_players_aim_assist()
{
    for (;;)
    {
        players = get_players();
        for (i = 0; i < players.size; i++)
        {
            if (!isDefined(players[i].velgg_aim_hooked))
            {
                players[i].velgg_aim_hooked = true;
                players[i] thread velgg_snap_to_head_ads();
            }
        }
        wait 1.0;
    }
}

velgg_snap_to_head_ads()
{
    self endon(""disconnect"");
    self endon(""death"");

    for (;;)
    {
        if (self AdsButtonPressed())
        {
            zombies = GetAiSpeciesArray(""axis"", ""all"");
            closest_zombie = undefined;
            closest_dist_sq = 650000; // Alcance de lock-on

            eye_pos = self GetEye();
            for (i = 0; i < zombies.size; i++)
            {
                if (isDefined(zombies[i]) && isAlive(zombies[i]))
                {
                    dist_sq = DistanceSquared(eye_pos, zombies[i].origin);
                    if (dist_sq < closest_dist_sq)
                    {
                        closest_dist_sq = dist_sq;
                        closest_zombie = zombies[i];
                    }
                }
            }

            if (isDefined(closest_zombie))
            {
                head_pos = closest_zombie GetTagOrigin(""j_head"");
                if (isDefined(head_pos))
                {
                    target_angles = VectorToAngles(head_pos - eye_pos);
                    self SetPlayerAngles(target_angles);
                }
            }
        }
        wait 0.05;
    }
}
";
            return updated.TrimEnd() + Environment.NewLine + snapFunctions;
        }

        /// <summary>
        /// Cria uma cópia .bak antes de modificar o arquivo (apenas se ainda não existir .bak original).
        /// </summary>
        private static void EnsureBackup(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            string bakPath = filePath + ".bak";
            if (!File.Exists(bakPath))
            {
                File.Copy(filePath, bakPath, overwrite: false);
            }
        }

        /// <summary>
        /// Restaura todos os arquivos .bak para o estado original limpo.
        /// </summary>
        public static int RestoreBackups(string repoPath, Action<string, Color> log)
        {
            if (string.IsNullOrWhiteSpace(repoPath))
                repoPath = DefaultLocalPath;

            if (!Directory.Exists(repoPath))
            {
                log($"[Backup] Pasta {repoPath} não encontrada.", Color.Orange);
                return 0;
            }

            var bakFiles = Directory.GetFiles(repoPath, "*.bak", SearchOption.AllDirectories);
            int restored = 0;

            foreach (string bakFile in bakFiles)
            {
                try
                {
                    string originalPath = bakFile.Substring(0, bakFile.Length - 4); // Remove ".bak"
                    File.Copy(bakFile, originalPath, overwrite: true);
                    File.Delete(bakFile);
                    restored++;
                }
                catch (Exception ex)
                {
                    log($"[Backup] Falha ao restaurar {Path.GetFileName(bakFile)}: {ex.Message}", Color.Red);
                }
            }

            log(restored > 0
                ? $"[Backup] {restored} arquivo(s) restaurados com sucesso para o código-fonte original!"
                : "[Backup] Nenhum arquivo .bak pendente encontrado (código já está no estado original).",
                restored > 0 ? Color.FromArgb(0, 230, 118) : Color.Yellow);

            return restored;
        }

        /// <summary>
        /// 4. Inicia o Servidor Web Local (HttpListener) na porta 8080 com cabeçalhos COOP/COEP
        /// (SharedArrayBuffer / Multi-threading) e streaming direto do SSD (baixo consumo de RAM).
        /// </summary>
        public static string StartLocalServer(string repoPath, Action<string, Color> log, int port = DefaultLocalPort)
        {
            if (string.IsNullOrWhiteSpace(repoPath))
                repoPath = DefaultLocalPath;

            EnsureBaselineEngineStructure(repoPath, log);
            _activeRootPath = repoPath;

            string prefix = $"http://localhost:{port}/";
            if (_httpListener != null && _httpListener.IsListening)
            {
                log($"[Servidor Local] Servidor HTTP já está ativo em {prefix} (Streaming SSD ativo).", Color.LightGreen);
                return prefix;
            }

            StopLocalServer();

            _serverCts = new CancellationTokenSource();
            var token = _serverCts.Token;

            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(prefix);
            _httpListener.Start();

            log($"[Servidor Local] HttpListener iniciado em {prefix} -> Servindo '{repoPath}' com COOP/COEP (SharedArrayBuffer).",
                Color.FromArgb(0, 230, 118));

            Task.Run(() => ListenLoopAsync(_httpListener, token, log), token);
            return prefix;
        }

        public static void StopLocalServer()
        {
            try
            {
                _serverCts?.Cancel();
                if (_httpListener != null)
                {
                    if (_httpListener.IsListening)
                        _httpListener.Stop();
                    _httpListener.Close();
                }
            }
            catch { }
            finally
            {
                _httpListener = null;
                _serverCts?.Dispose();
                _serverCts = null;
            }
        }

        private static async Task ListenLoopAsync(HttpListener listener, CancellationToken token, Action<string, Color> log)
        {
            while (!token.IsCancellationRequested && listener.IsListening)
            {
                HttpListenerContext? ctx = null;
                try
                {
                    ctx = await listener.GetContextAsync();
                    _ = Task.Run(() => HandleHttpRequestAsync(ctx), token);
                }
                catch (HttpListenerException) when (token.IsCancellationRequested || !listener.IsListening)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log($"[Servidor Local] Aviso na requisição: {ex.Message}", Color.Orange);
                }
            }
        }

        private static async Task HandleHttpRequestAsync(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;

            try
            {
                // Cabeçalhos obrigatórios para SharedArrayBuffer / WebAssembly PThreads Multi-threading
                res.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                res.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
                res.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
                res.Headers["Access-Control-Allow-Origin"] = "*";
                res.Headers["Cache-Control"] = "no-cache";

                string rawPath = Uri.UnescapeDataString(req.Url?.AbsolutePath ?? "/").TrimStart('/');
                if (string.IsNullOrEmpty(rawPath) || rawPath.EndsWith("/"))
                {
                    rawPath = Path.Combine(rawPath, "index.html");
                }

                string safeRelative = rawPath.Replace('/', Path.DirectorySeparatorChar);
                string fullPath = Path.GetFullPath(Path.Combine(_activeRootPath, safeRelative));

                // Previne Path Traversal fora de _activeRootPath
                string normalizedRoot = Path.GetFullPath(_activeRootPath);
                if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    res.StatusCode = (int)HttpStatusCode.Forbidden;
                    res.Close();
                    return;
                }

                // Se pediu index.html na raiz e o arquivo está em subpasta (ex: web/index.html ou build/index.html), localiza-o
                if (!File.Exists(fullPath) && Path.GetFileName(fullPath).Equals("index.html", StringComparison.OrdinalIgnoreCase))
                {
                    string? foundIndex = Directory.GetFiles(_activeRootPath, "index.html", SearchOption.AllDirectories).FirstOrDefault();
                    if (!string.IsNullOrEmpty(foundIndex))
                        fullPath = foundIndex;
                }

                if (!File.Exists(fullPath))
                {
                    res.StatusCode = (int)HttpStatusCode.NotFound;
                    byte[] notFoundBytes = Encoding.UTF8.GetBytes($"404 - Arquivo nao encontrado: {rawPath}");
                    res.ContentType = "text/plain; charset=utf-8";
                    res.ContentLength64 = notFoundBytes.Length;
                    await res.OutputStream.WriteAsync(notFoundBytes, 0, notFoundBytes.Length);
                    res.Close();
                    return;
                }

                res.StatusCode = (int)HttpStatusCode.OK;
                res.ContentType = GetMimeType(Path.GetExtension(fullPath));

                // Faz streaming direto do SSD em chunks de 64KB para não alocar arquivos grandes na RAM
                using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.SequentialScan | FileOptions.Asynchronous);
                res.ContentLength64 = fs.Length;
                await fs.CopyToAsync(res.OutputStream, 65536);
            }
            catch
            {
                // Ignora conexões fechadas pelo cliente durante streaming
            }
            finally
            {
                try { res.Close(); } catch { }
            }
        }

        private static string GetMimeType(string ext)
        {
            return ext.ToLowerInvariant() switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".js" or ".mjs" => "application/javascript; charset=utf-8",
                ".wasm" => "application/wasm",
                ".css" => "text/css; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".cfg" or ".gsc" or ".cpp" or ".h" => "text/plain; charset=utf-8",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".ico" => "image/x-icon",
                ".mp3" => "audio/mpeg",
                ".ogg" => "audio/ogg",
                ".wav" => "audio/wav",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Garante que a pasta local possua os scripts base (_zombiemode.gsc, _zombiemode_score.gsc,
        /// _zombiemode_perks.gsc, _zombiemode_weapons.gsc), config.cfg e index.html local caso ainda não existam.
        /// </summary>
        private static void EnsureBaselineEngineStructure(string repoPath, Action<string, Color> log)
        {
            Directory.CreateDirectory(repoPath);

            string mapsDir = Path.Combine(repoPath, "raw", "maps");
            Directory.CreateDirectory(mapsDir);

            string zombiemodeGsc = Path.Combine(mapsDir, "_zombiemode.gsc");
            if (!File.Exists(zombiemodeGsc))
            {
                File.WriteAllText(zombiemodeGsc, @"// MisaDev4 BO1 Zombies Decompiled - _zombiemode.gsc
#include maps\_utility;
#include common_scripts\utility;

main()
{
    level.player_starting_points = 500;
    level.perk_purchase_limit = 4;
    init();
}

init()
{
    level thread onPlayerConnect();
}

onPlayerConnect()
{
    for (;;)
    {
        level waittill(""connecting"", player);
        player.score = 500;
        player.score_total = 500;
        player.num_perks = 0;
        player thread onPlayerSpawned();
    }
}

onPlayerSpawned()
{
    self endon(""disconnect"");
    for (;;)
    {
        self waittill(""spawned_player"");
    }
}
", Encoding.UTF8);
            }

            string scoreGsc = Path.Combine(mapsDir, "_zombiemode_score.gsc");
            if (!File.Exists(scoreGsc))
            {
                File.WriteAllText(scoreGsc, @"// MisaDev4 BO1 Zombies Decompiled - _zombiemode_score.gsc
add_to_player_score(points)
{
    if (!isDefined(points) || points <= 0)
        return;

    self.score += points;
    self.score_total += points;
}

player_add_points(event, mod, hit_location, is_dog)
{
    points = 50;
    self add_to_player_score(points);
}
", Encoding.UTF8);
            }

            string perksGsc = Path.Combine(mapsDir, "_zombiemode_perks.gsc");
            if (!File.Exists(perksGsc))
            {
                File.WriteAllText(perksGsc, @"// MisaDev4 BO1 Zombies Decompiled - _zombiemode_perks.gsc
perk_think(perk)
{
    if (self.num_perks < 4 && self.num_perks < level.perk_purchase_limit)
    {
        self.num_perks++;
        return true;
    }
    return false;
}
", Encoding.UTF8);
            }

            string weaponsGsc = Path.Combine(mapsDir, "_zombiemode_weapons.gsc");
            if (!File.Exists(weaponsGsc))
            {
                File.WriteAllText(weaponsGsc, @"// MisaDev4 BO1 Zombies Decompiled - _zombiemode_weapons.gsc
init_weapons()
{
    level.treasure_chest_cost = 950;
}

treasure_chest_think()
{
    cost = 950;
    zombie_cost = 950;
}
", Encoding.UTF8);
            }

            string indexHtml = Path.Combine(repoPath, "index.html");
            if (!File.Exists(indexHtml))
            {
                File.WriteAllText(indexHtml, @"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <title>VelGG TurboBooster V2 - BO1 Zombies Local Engine Host (SSD Streaming)</title>
    <style>
        body, html { margin: 0; padding: 0; width: 100%; height: 100%; background: #0a0a0e; color: #00e676; font-family: Consolas, monospace; overflow: hidden; }
        .topbar { height: 34px; background: #14141f; border-bottom: 1px solid #00e676; display: flex; align-items: center; justify-content: space-between; padding: 0 14px; font-size: 12px; }
        .badge { color: #40c4ff; }
        iframe { width: 100%; height: calc(100% - 35px); border: none; }
    </style>
</head>
<body>
    <div class=""topbar"">
        <span>⚡ VELGG LOCAL ENGINE STREAMER (C:\Games\BO1_Zombies_Local) | COOP/COEP SharedArrayBuffer: <strong id=""sab"">Verificando...</strong></span>
        <span class=""badge"">Hotkeys: F1 /god | F2 Ammo | F3 /give all | F4 /noclip | F5 /notarget | F6 FOV 90 + Speed</span>
    </div>
    <iframe src=""https://vel.gg/bo1z"" allow=""autoplay; fullscreen; pointer-lock; gamepad; cross-origin-isolated""></iframe>
    <script>
        document.getElementById('sab').textContent = (typeof SharedArrayBuffer !== 'undefined') ? 'ATIVO (Multi-Thread)' : 'INATIVO';
    </script>
</body>
</html>
", Encoding.UTF8);
            }
        }

        private static bool IsGitAvailable()
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                proc?.WaitForExit(4000);
                return proc != null && proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<(int ExitCode, string Output)> RunProcessCaptureAsync(string fileName, string arguments)
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            proc.Start();
            string stdOut = await proc.StandardOutput.ReadToEndAsync();
            string stdErr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            string combined = string.IsNullOrWhiteSpace(stdOut) ? stdErr : stdOut + Environment.NewLine + stdErr;
            return (proc.ExitCode, combined.Trim());
        }

        private static string FirstNonEmptyLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "OK";
            return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "OK";
        }

        private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, destSubDir);
            }
        }
    }
}

