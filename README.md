# ⚡ VelGG_TurboBooster (.NET 8 / Win32 API)

**VelGG_TurboBooster** é um **Game Booster Extremo, Otimizador de Rede/Kernel e Custom Chromium Launcher** desenvolvido em C# (.NET 8 Windows Forms) utilizando chamadas nativas da API do Windows (`kernel32.dll`, `psapi.dll` e `user32.dll`).

Foi projetado especificamente para extrair o **máximo de FPS e menor latência possível** nos jogos em WebAssembly/WebGL **Call of Duty Zombies** (`https://vel.gg/bo1z` e `https://vel.gg/bsz`) em notebooks e PCs com 16GB de RAM, além de fornecer um sistema de **Macros/Hotkeys** para comandos de console durante a partida.

---

## 📂 Arquitetura do Projeto (Modular)

O código-fonte foi dividido em camadas claras para facilitar o entendimento, manutenção e auditoria do que cada componente faz:

```text
VelGG_TurboBooster/
├── src/
│   ├── Native/
│   │   └── Win32Native.cs              # P/Invoke nativo (GlobalMemoryStatusEx, EmptyWorkingSet, GetAsyncKeyState)
│   ├── Helpers/
│   │   └── SystemCommandHelper.cs      # Execução silenciosa de processos (sc, netsh, powercfg) e edição do Registro
│   ├── Modules/
│   │   ├── SystemDebloatModule.cs      # Módulo 1: Parada de serviços pesados, Kill List, Startup e limpeza de %TEMP%
│   │   ├── MemoryFlushModule.cs        # Módulo 2: Monitoramento de RAM e Flush físico de Working Set (psapi.dll)
│   │   ├── KernelCpuGpuModule.cs       # Módulo 3: CPU 100% Clock, Core Unparking, Kernel 100% RAM, HAGS e Sem EcoQoS
│   │   ├── NetworkOptimizerModule.cs   # Módulo 4: Google DNS IPv4/IPv6, Pilha TCP (CTCP) e Zero Nagle
│   │   ├── ChromeTurboLauncher.cs      # Módulo 5: Custom Chrome Launcher com 8GB V8 Heap e Prioridade Alta
│   │   └── HotkeyConsoleListener.cs    # Módulo 6: Listener assíncrono de Hotkeys (F1 a F5 + END) para cheats no console
│   ├── Program.cs                      # Auto-elevação para Administrador (runas) e inicialização do App
│   ├── MainForm.cs                     # Interface Gráfica Dark Gamer e orquestração dos módulos
│   └── app.manifest                    # Manifesto de compatibilidade Windows 10/11 (elevação UAC gerenciada no Program.cs)
├── VelGG_TurboBooster.csproj           # Projeto .NET 8 configurado como Self-Contained Single-File (não exige .NET instalado)
├── README.md                           # Documentação completa do projeto
└── .gitignore                          # Proteção de binários, caches locais e arquivos pessoais para publicação no GitHub
```

---

## 🛠️ O Que Cada Módulo Faz?

### 1. `SystemDebloatModule.cs` (Extermínio de Processos, Serviços e Debloat)
- **Serviços Parados:** `vgc` e `vgk` (Riot Vanguard ocioso), `DiagTrack`, `dmwappushservice`, `MapsBroker`, `lfsvc`, `WSearch`, `SysMain`, `DSAService`, `DSAUpdateService` e `esifsvc`.
- **Kill List de Processos:** Encerra processos pesados em segundo plano como `Lively`, `Rainmeter`, `vgtray`, `RiotClientServices`, `msedge`, `msedgewebview2`, `Widgets`, `OneDrive`, `Teams`, `Spotify`, `Steam`, `EpicGamesLauncher`, entre outros.
- **Limpeza Inteligente de Inicialização (Startup):** Remove entradas desnecessárias do Registro (`Run`) e pastas `Startup`, mas **preserva** `Rainmeter`, `Lively`, `Wallpaper`, drivers de áudio (`Realtek`, `Waves`, `Dolby`), touchpad e `WindowsDefender` para que voltem normalmente quando o PC reiniciar.
- **Limpeza de Temporários:** Esvazia `%TEMP%`, `C:\Windows\Temp` e `%LOCALAPPDATA%\Temp` ignorando arquivos em uso.

### 2. `MemoryFlushModule.cs` (Flush Físico de RAM via `psapi.dll`)
- Lê a memória física total e disponível em tempo real com `GlobalMemoryStatusEx` (`kernel32.dll`).
- Percorre todos os processos ativos e invoca `EmptyWorkingSet(proc.Handle)` (`psapi.dll`), forçando o Windows a liberar páginas ociosas da RAM física, seguido de `GC.Collect()`.
- Calcula e exibe na tela exatamente quantos **GB de RAM foram recuperados**.

### 3. `KernelCpuGpuModule.cs` (CPU 100%, GPU HAGS, Kernel 100% RAM e Sem EcoQoS)
- **Energia e Clock:** Desativa hibernação (`powercfg -h off`), ativa o plano de Alto Desempenho/Ultimate Performance e trava `PROCTHROTTLEMIN 100`, `PROCTHROTTLEMAX 100` e `CPMINCORES 100` (Core Unparking).
- **Memória e Kernel:** Define `DisablePagingExecutive = 1`, `LargeSystemCache = 0` e `SvcHostSplitThresholdInKB = 16777216` para manter drivers e sistema 100% na RAM física de 16GB sem engasgos de paginação em SSD.
- **GPU e Fim da "Folha Verde" (EcoQoS):**
  - Ativa `HwSchMode = 2` (HAGS) e `Win32PrioritySeparation = 38`.
  - Desativa o Power Throttling global (`PowerThrottlingOff = 1`).
  - Força o `chrome.exe` na GPU dedicada/alto desempenho (`GpuPreference=2;`).
  - Ajusta `SystemResponsiveness = 0`, `NetworkThrottlingIndex = 0xFFFFFFFF`, `GPU Priority = 8` e `Priority = 6` para jogos.
  - Configura as políticas do Chrome (`HardwareAccelerationModeEnabled = 1`, sem modo de eficiência/economia de bateria e DNS-over-HTTPS do Google).

### 4. `NetworkOptimizerModule.cs` (Rede Co-op Multiplayer + Google DNS + Zero Nagle)
- Configura **Google DNS IPv4** (`8.8.8.8` / `8.8.4.4`) e **IPv6** (`2001:4860:4860::8888`) nas interfaces de rede ativas.
- Ajusta a pilha TCP (`rss=enabled`, `rsc=disabled`, `ecncapability=disabled`, `timestamps=disabled`, `congestionprovider=ctcp`) e executa `ipconfig /flushdns`.
- Desativa o **Algoritmo de Nagle** em todas as interfaces TCP/IP (`TcpAckFrequency = 1`, `TCPNoDelay = 1`, `TcpDelAckTicks = 0`) para reduzir o ping no Co-op Multiplayer.

### 5. `ChromeTurboLauncher.cs` (Custom Launcher com 8GB V8 Heap + Prioridade Alta)
- Localiza automaticamente o `chrome.exe`, fecha instâncias antigas e inicia o jogo em modo App dedicado (`--app=<URL>`) com **8GB de Heap V8** (`--js-flags="--max-old-space-size=8192"`), `--enable-zero-copy`, `--enable-raw-draw`, `--disable-frame-rate-limit` e sem throttling em background.
- Aguarda 3 segundos após o carregamento e eleva todos os processos `chrome` para `ProcessPriorityClass.High`.

### 6. `HotkeyConsoleListener.cs` (Macros de Console / Cheats em Partida)
Monitora as teclas em background via `GetAsyncKeyState` (`user32.dll`) e envia automaticamente a abertura do console (`` ` ``), o comando e `ENTER` (com bipe sonoro de confirmação):

| Tecla | Comando Enviado | Efeito na Partida |
| :---: | :--- | :--- |
| **F1** | `/god` | Vida Infinita (God Mode) |
| **F2** | `/sf_use_ignoreammo 1` | Munição Infinita sem recarregar |
| **F3** | `/give all` | Todas as Armas + Ray Gun |
| **F4** | `/noclip` | Atravessar Paredes e Portas |
| **F5** | `/notarget` | Invisível para os Zumbis |
| **END** | *(Desativar)* | Encerra o listener de Hotkeys com um bipe grave |

---

## 🚀 Como Compilar e Gerar o Executável Único (`.exe`)

O projeto já está configurado por padrão como **Self-Contained Single-File** (`<SelfContained>true</SelfContained>` e `<PublishSingleFile>true</PublishSingleFile>`), ou seja, **quem for executar o `.exe` não precisa ter o .NET instalado no PC**.

Para compilar e gerar o executável único pronto para distribuição, abra o PowerShell ou Prompt de Comando na raiz do projeto e execute:

```powershell
dotnet publish -c Release -o .
```

O arquivo final `VelGG_TurboBooster.exe` (100% independente, com o runtime embutido e comprimido) será gerado diretamente na raiz do projeto.

