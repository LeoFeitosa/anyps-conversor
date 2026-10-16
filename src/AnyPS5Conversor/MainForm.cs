using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using static AnyPS5Conversor.Strings;

namespace AnyPS5Conversor;

public sealed class MainForm : Form
{
    const string UpstreamUrl = "https://github.com/boykopovar/AnyPS5";
    const string ProjectUrl = "https://github.com/LeoFeitosa/anyps5-conversor";

    static readonly string AppVersion =
        (Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
        .Split('+')[0];

    readonly Label subtitle = Theme.Label("", Theme.Subtitle, Theme.HeaderMuted);
    readonly RoundButton portugueseButton = Theme.SecondaryButton();
    readonly RoundButton englishButton = Theme.SecondaryButton();

    readonly Card gameCard = new();
    readonly StepBadge gameBadge = new(1);
    readonly Label gameTitle = Theme.Label("", Theme.CardTitle, Theme.Text);
    readonly Label gameHint = Theme.Label("", Theme.Body, Theme.Muted);
    readonly DropZone dropZone = new();
    readonly Label dropText = Theme.Label("", Theme.CardTitle, Theme.Text, ContentAlignment.MiddleCenter);
    readonly RoundButton browseFolderButton = Theme.SecondaryButton();
    readonly RoundButton browseFileButton = Theme.SecondaryButton();
    readonly Panel gamePanel = new() { BackColor = Theme.Surface, Visible = false };
    readonly PictureBox gameIcon = new() { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Background };
    readonly Label gameName = Theme.Label("", Theme.GameName, Theme.Text);
    readonly Label gameMeta = Theme.Label("", Theme.Body, Theme.Muted);
    readonly Label gameStatus = Theme.Label("", Theme.Body, Theme.Success);
    readonly RoundButton changeButton = Theme.SecondaryButton();
    readonly Label legalHint = Theme.Label("", Theme.Small, Theme.Muted);

    readonly Card outputCard = new();
    readonly StepBadge outputBadge = new(2);
    readonly Label outputTitle = Theme.Label("", Theme.CardTitle, Theme.Text);
    readonly Label outputHint = Theme.Label("", Theme.Body, Theme.Muted);
    readonly TextBox outputBox = new() { ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Body, BackColor = Theme.Input, ForeColor = Theme.Text };
    readonly RoundButton outputButton = Theme.SecondaryButton();
    readonly CheckBox recycleSourceBox = new() { ForeColor = Theme.Text, BackColor = Theme.Surface, Font = Theme.Body };

    readonly Card convertCard = new();
    readonly StepBadge convertBadge = new(3);
    readonly Label convertTitle = Theme.Label("", Theme.CardTitle, Theme.Text);
    readonly RoundButton convertButton = Theme.AccentButton();
    readonly Panel donePanel = new() { BackColor = Theme.Surface, Visible = false };
    readonly RoundButton playButton = Theme.AccentButton();
    readonly RoundButton openButton = Theme.SecondaryButton();
    readonly GameProgress progressBar = new();
    readonly Label statusLabel = Theme.Label("", Theme.Body, Theme.Muted);
    readonly Label percentLabel = Theme.Label("", Theme.CardTitle, Theme.Text, ContentAlignment.TopRight);
    readonly LinkLabel detailsLink = Theme.Link();
    readonly TextBox logBox = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = false, Visible = false,
        Font = new Font("Consolas", 9f), BackColor = Theme.Input, ForeColor = Theme.Muted,
        BorderStyle = BorderStyle.None,
    };

    readonly LinkLabel upstreamLink = Theme.Link();
    readonly LinkLabel projectLink = Theme.Link();
    readonly LinkLabel licensesLink = Theme.Link();
    readonly Label disclaimer = Theme.Label("", Theme.Small, Theme.Muted);
    readonly Label versionLabel = Theme.Label("", Theme.Small, Theme.Muted, ContentAlignment.TopRight);

    GameInfo? game;
    string? extractedFolder;
    string? extractionParent;
    string? sourceInput;
    string? convertedExe;
    CancellationTokenSource? running;
    string statusKey = "status_idle";
    object[] statusArgs = [];
    bool statusIsError;
    readonly string[] missingCpu = Converter.MissingCpuFeatures();

    const int BaseHeight = 734;
    const int LogHeight = 190;

    /// <param name="initialGame">Pasta solta sobre o ícone do .exe, se houver.</param>
    public MainForm(string? initialGame = null)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, BaseHeight);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        Font = Theme.Body;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        var header = new HeaderPanel { Bounds = new Rectangle(0, 0, 760, 84) };
        header.Controls.Add(subtitle);
        subtitle.SetBounds(80, 52, 480, 22);
        portugueseButton.SetBounds(614, 26, 56, 30);
        portugueseButton.Text = "PT";
        portugueseButton.Click += (_, _) => ChangeLanguage(Language.Portuguese);
        englishButton.SetBounds(676, 26, 56, 30);
        englishButton.Text = "EN";
        englishButton.Click += (_, _) => ChangeLanguage(Language.English);
        header.Controls.AddRange([portugueseButton, englishButton]);

        // 1. Jogo
        gameCard.SetBounds(24, 104, 712, 204);
        PlaceStep(gameCard, gameBadge, gameTitle, gameHint);
        dropZone.SetBounds(20, 78, 672, 96);
        dropText.SetBounds(0, 14, 672, 26);
        browseFolderButton.SetBounds(150, 48, 180, 34);
        browseFolderButton.Click += async (_, _) => await PickFolderAsync();
        browseFileButton.SetBounds(342, 48, 180, 34);
        browseFileButton.Click += async (_, _) => await PickFileAsync();
        dropZone.Controls.AddRange([dropText, browseFolderButton, browseFileButton]);
        gamePanel.SetBounds(20, 78, 672, 96);
        gameIcon.SetBounds(0, 4, 88, 88);
        gameName.SetBounds(104, 6, 440, 28);
        gameName.AutoEllipsis = true;
        gameMeta.SetBounds(104, 36, 440, 22);
        gameStatus.SetBounds(104, 62, 560, 22);
        gameStatus.AutoEllipsis = true;
        changeButton.SetBounds(552, 4, 120, 34);
        changeButton.Click += (_, _) => ResetGame();
        gamePanel.Controls.AddRange([gameIcon, gameName, gameMeta, gameStatus, changeButton]);
        legalHint.SetBounds(20, 178, 672, 20);
        gameCard.Controls.AddRange([dropZone, gamePanel, legalHint]);

        // 2. Destino
        outputCard.SetBounds(24, 324, 712, 140);
        PlaceStep(outputCard, outputBadge, outputTitle, outputHint);
        outputBox.SetBounds(20, 74, 532, 28);
        outputBox.Text = Settings.Output;
        outputButton.SetBounds(564, 70, 128, 34);
        outputButton.Click += (_, _) => PickOutput();
        recycleSourceBox.SetBounds(20, 108, 672, 24);
        outputCard.Controls.AddRange([outputBox, outputButton, recycleSourceBox]);

        // 3. Converter
        convertCard.SetBounds(24, 480, 712, 192);
        PlaceStep(convertCard, convertBadge, convertTitle, null);
        convertButton.SetBounds(20, 62, 672, 50);
        convertButton.Font = Theme.BigButton;
        convertButton.Click += async (_, _) => await ConvertOrCancelAsync();
        donePanel.SetBounds(20, 62, 672, 50);
        playButton.SetBounds(0, 0, 400, 50);
        playButton.Font = Theme.BigButton;
        playButton.Click += async (_, _) => await PlayAsync();
        openButton.SetBounds(412, 0, 260, 50);
        openButton.Click += (_, _) => OpenFolder();
        donePanel.Controls.AddRange([playButton, openButton]);
        progressBar.SetBounds(20, 126, 600, 14);
        percentLabel.SetBounds(626, 120, 66, 26);
        statusLabel.SetBounds(20, 148, 672, 36);
        detailsLink.SetBounds(560, 18, 132, 20);
        detailsLink.TextAlign = ContentAlignment.TopRight;
        detailsLink.LinkClicked += (_, _) => ToggleDetails();
        convertCard.Controls.AddRange([convertButton, donePanel, progressBar, percentLabel, statusLabel, detailsLink]);

        logBox.SetBounds(24, 684, 712, LogHeight - 12);

        var footer = new Panel { BackColor = Theme.Footer, Dock = DockStyle.Bottom, Height = 46 };
        upstreamLink.SetBounds(24, 6, 360, 18);
        upstreamLink.LinkClicked += (_, _) => OpenUrl(UpstreamUrl);
        projectLink.SetBounds(400, 6, 120, 18);
        projectLink.Text = "GitHub";
        projectLink.LinkClicked += (_, _) => OpenUrl(ProjectUrl);
        // Os avisos de licença vão dentro do .exe: quem baixa só o executável também os recebe.
        licensesLink.SetBounds(400, 25, 120, 18);
        licensesLink.LinkClicked += (_, _) => MessageDialog.Show(this, T("licenses_title"), Converter.ThirdPartyNotices, warning: false);
        disclaimer.SetBounds(24, 25, 370, 18);
        versionLabel.SetBounds(476, 6, 260, 36);
        versionLabel.Text = $"v{AppVersion}\nAnyPS5 {Converter.AnyPS5Version}";
        footer.Controls.AddRange([upstreamLink, projectLink, licensesLink, disclaimer, versionLabel]);

        Controls.AddRange([header, gameCard, outputCard, convertCard, logBox, footer]);
        EnableDrop(this);
        ApplyTexts();
        ResumeLayout(false);
        if (initialGame is not null)
            Shown += async (_, _) => await LoadGameAsync(initialGame);
        FormClosed += (_, _) =>
        {
            Archives.TryDelete(Archives.TempRoot);
            DiscardExtraction();
        };
    }

    static void PlaceStep(Card card, StepBadge badge, Label title, Label? hint)
    {
        badge.SetBounds(20, 18, 32, 32);
        title.SetBounds(64, 16, 480, 28);
        card.Controls.AddRange([badge, title]);
        if (hint is null)
            return;
        hint.SetBounds(64, 44, 628, 22);
        card.Controls.Add(hint);
    }

    void ApplyTexts()
    {
        Text = T("app_name");
        subtitle.Text = T("app_subtitle");
        portugueseButton.Primary = Current == Language.Portuguese;
        englishButton.Primary = Current == Language.English;
        gameTitle.Text = T("step1_title");
        gameHint.Text = T("step1_hint");
        dropText.Text = T("drop_text");
        browseFolderButton.Text = T("browse_folder");
        browseFileButton.Text = T("browse_file");
        changeButton.Text = T("change");
        legalHint.Text = T("legal_hint");
        outputTitle.Text = T("step2_title");
        outputHint.Text = T("step2_hint");
        outputButton.Text = T("change");
        recycleSourceBox.Text = T("recycle_source");
        convertTitle.Text = T("step3_title");
        convertButton.Text = running is null ? T("convert") : T("cancel");
        playButton.Text = T("play");
        openButton.Text = T("open_folder");
        detailsLink.Text = logBox.Visible ? T("hide_details") : T("show_details");
        upstreamLink.Text = T("footer_based");
        disclaimer.Text = T("footer_disclaimer");
        licensesLink.Text = T("footer_licenses");
        if (game is not null)
            ShowGameTexts(game);
        SetStatus(statusKey, statusIsError, statusArgs);
        Invalidate(true);
    }

    void ChangeLanguage(Language language)
    {
        Set(language);
        ApplyTexts();
    }

    void SetStatus(string key, bool error = false, params object[] args)
    {
        statusKey = key;
        statusArgs = args;
        statusIsError = error;
        var text = T(key, args);
        if (key is "status_idle" or "status_ready" && missingCpu.Length > 0)
        {
            text = T("err_cpu", string.Join(", ", missingCpu));
            error = true;
        }
        statusLabel.Text = text;
        statusLabel.ForeColor = error ? Theme.Error : key is "status_done" or "status_done_recycled" ? Theme.Success : Theme.Muted;
    }

    async Task PickFolderAsync()
    {
        if (running is not null)
            return;
        using var dialog = new FolderBrowserDialog { Description = T("pick_game"), UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await LoadGameAsync(dialog.SelectedPath);
    }

    async Task PickFileAsync()
    {
        if (running is not null)
            return;
        var patterns = string.Join(";", Archives.Extensions.Select(e => "*" + e).Append("eboot.bin"));
        using var dialog = new OpenFileDialog { Title = T("pick_file"), Filter = $"{T("file_filter")}|{patterns}" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await LoadGameAsync(dialog.FileName);
    }

    void PickOutput()
    {
        if (running is not null)
            return;
        using var dialog = new FolderBrowserDialog { Description = T("pick_output"), UseDescriptionForTitle = true, SelectedPath = outputBox.Text };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        outputBox.Text = dialog.SelectedPath;
        Settings.Output = dialog.SelectedPath;
        Archives.CleanStaleTemp(dialog.SelectedPath);
    }

    [DllImport("user32.dll")]
    static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("dwmapi.dll")]
    static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

    struct RECT { public int Left, Top, Right, Bottom; }

    /// <summary>
    /// Imagem da janela inicial para o README. A janela abre fora da área visível e o Windows a desenha
    /// inteira, com a barra de título; a pasta de saída mostrada é um exemplo, nunca a de quem gera.
    /// </summary>
    public static void SaveScreenshot(string file)
    {
        using var form = new MainForm { StartPosition = FormStartPosition.Manual, Location = new Point(-8000, -8000), ShowInTaskbar = false };
        form.outputBox.Text = @"C:\Users\Jogador\AnyPS5";
        form.Show();
        Application.DoEvents();
        GetWindowRect(form.Handle, out var window);
        // A borda invisível de redimensionamento fica de fora: só o que aparece na tela.
        DwmGetWindowAttribute(form.Handle, 9, out var visible, Marshal.SizeOf<RECT>());
        using var full = new Bitmap(window.Right - window.Left, window.Bottom - window.Top);
        using (var g = Graphics.FromImage(full))
        {
            var hdc = g.GetHdc();
            PrintWindow(form.Handle, hdc, 2);
            g.ReleaseHdc(hdc);
        }
        var crop = new Rectangle(visible.Left - window.Left, visible.Top - window.Top, visible.Right - visible.Left, visible.Bottom - visible.Top);
        using var image = full.Clone(crop, full.PixelFormat);
        image.Save(file, System.Drawing.Imaging.ImageFormat.Png);
    }

    /// <summary>Carrega uma pasta, um eboot.bin ou um arquivo compactado com o jogo.</summary>
    async Task LoadGameAsync(string path)
    {
        if (running is not null)
            return;
        running = new CancellationTokenSource();
        SetBusy(true);
        try
        {
            string? fallbackName = null;
            string? extracted = null;
            if (Archives.IsArchive(path))
            {
                fallbackName = Path.GetFileName(path);
                foreach (var extension in Archives.Extensions.Concat([".tar"]))
                    if (fallbackName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                        fallbackName = fallbackName[..^extension.Length];
                progressBar.Value = 0;
                SetStatus("status_extracting", false, 0);
                var progress = new Progress<int>(p =>
                {
                    if (running?.IsCancellationRequested == true)
                        return;
                    progressBar.Value = p * 10;
                    percentLabel.Text = $"{p}%";
                    SetStatus("status_extracting", false, p);
                });
                var token = running.Token;
                string? password = null;
                while (true)
                {
                    try
                    {
                        var attempt = password;
                        var workRoot = Archives.WorkRoot(outputBox.Text);
                        extracted = await Task.Run(() => Archives.Extract(path, attempt, workRoot, progress, token), token);
                        break;
                    }
                    catch (PasswordException e)
                    {
                        password = PasswordDialog.Ask(this, Path.GetFileName(path), e.Wrong);
                        if (password is null)
                        {
                            SetStatus(game is null ? "status_idle" : "status_ready");
                            return;
                        }
                        SetStatus("status_extracting", false, 0);
                    }
                }
            }

            GameInfo loaded;
            try
            {
                loaded = await Task.Run(() => GameInfo.Load(extracted ?? path, fallbackName));
            }
            catch (UserException e) when (extracted is not null && e.Key == "err_no_eboot")
            {
                Archives.TryDelete(extracted);
                throw new UserException("err_no_eboot_archive", Path.GetFileName(path));
            }
            catch
            {
                Archives.TryDelete(extracted);
                throw;
            }
            DiscardExtraction();
            extractedFolder = extracted;
            extractionParent = extracted is null ? null : outputBox.Text;
            game = loaded;
            sourceInput = path;
            convertedExe = null;
            ShowGame(game);
            donePanel.Visible = false;
            convertButton.Visible = true;
            progressBar.Value = 0;
            percentLabel.Text = "";
            gameBadge.Done = true;
            convertBadge.Done = outputBadge.Done = false;
            SetStatus("status_ready");
        }
        catch (OperationCanceledException)
        {
            SetStatus("status_cancelled", true);
        }
        catch (UserException e)
        {
            SetStatus(game is null ? "status_idle" : "status_ready");
            ShowError(T(e.Key, e.Args));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetStatus(game is null ? "status_idle" : "status_ready");
            ShowError(T("err_unexpected", e.Message));
        }
        finally
        {
            // Extração que não virou jogo (sem eboot.bin, senha cancelada, erro): não deixa a pasta de trabalho
            // para trás, a menos que ela ainda guarde a extração do jogo carregado antes.
            if (Archives.IsArchive(path) && extractionParent != outputBox.Text)
                Archives.DeleteWorkRoot(outputBox.Text);
            percentLabel.Text = "";
            progressBar.Value = 0;
            running.Dispose();
            running = null;
            SetBusy(false);
        }
    }

    /// <summary>Volta para a área de soltar para escolher outro jogo.</summary>
    void ResetGame()
    {
        if (running is not null)
            return;
        game = null;
        sourceInput = null;
        convertedExe = null;
        gameIcon.Image?.Dispose();
        gameIcon.Image = null;
        gamePanel.Visible = false;
        dropZone.Visible = true;
        donePanel.Visible = false;
        convertButton.Visible = true;
        progressBar.Value = 0;
        gameBadge.Done = outputBadge.Done = convertBadge.Done = false;
        DiscardExtraction();
        SetStatus("status_idle");
    }

    void SetBusy(bool busy)
    {
        progressBar.Animated = busy;
        convertButton.Text = busy ? T("cancel") : T("convert");
        convertButton.Primary = !busy;
        convertButton.Enabled = true;
        // O idioma pode mudar a qualquer momento, inclusive no meio de uma extração ou conversão.
        browseFolderButton.Enabled = browseFileButton.Enabled = changeButton.Enabled = outputButton.Enabled = !busy;
        UseWaitCursor = false;
    }

    void ShowGame(GameInfo info)
    {
        dropZone.Visible = false;
        gamePanel.Visible = true;
        ShowGameTexts(info);
        // O ícone fica em memória: a pasta de onde veio (a extração) é apagada depois da conversão.
        gameIcon.Image?.Dispose();
        gameIcon.Image = info.IconPath is null ? null : Image.FromStream(new MemoryStream(File.ReadAllBytes(info.IconPath)));
    }

    void ShowGameTexts(GameInfo info)
    {
        gameName.Text = info.Name;
        gameMeta.Text = T("game_id", info.TitleId);
        gameStatus.Text = info.HasParamJson ? T("game_ready") : T("game_no_param");
        gameStatus.ForeColor = info.HasParamJson ? Theme.Success : Theme.Warning;
    }

    async Task ConvertOrCancelAsync()
    {
        if (running is not null)
        {
            // Um clique basta: o botão trava até a limpeza terminar e a janela voltar ao normal.
            running.Cancel();
            convertButton.Enabled = false;
            SetStatus("status_cancelling");
            return;
        }
        if (game is null)
        {
            await PickFolderAsync();
            if (game is null)
                return;
        }

        running = new CancellationTokenSource();
        SetBusy(true);
        logBox.Clear();
        var logFile = ConversionLogPath(game);
        var finished = false;
        var progress = new Progress<ConversionProgress>(p =>
        {
            // Avisos atrasados da conversão não podem sobrescrever o status final nem o "cancelando".
            if (finished || running?.IsCancellationRequested == true)
                return;
            progressBar.Value = (int)(Math.Clamp(p.Fraction, 0, 1) * 1000);
            percentLabel.Text = $"{(int)(Math.Clamp(p.Fraction, 0, 1) * 100)}%";
            SetStatus(p.StatusKey, false, p.Args);
        });
        void Log(string line)
        {
            try
            {
                File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}");
            }
            catch (IOException) { }
            BeginInvoke(() => logBox.AppendText(line + Environment.NewLine));
        }
        Log($"AnyPS5 Conversor {AppVersion} | {game.Name} ({game.TitleId}) | {game.Folder} -> {outputBox.Text}");

        try
        {
            Directory.CreateDirectory(outputBox.Text);
            convertedExe = await Converter.ConvertAsync(game, outputBox.Text, extractedFolder is not null, progress, Log, running.Token);
            finished = true;
            progressBar.Value = 1000;
            percentLabel.Text = "100%";
            convertBadge.Done = outputBadge.Done = true;
            convertButton.Visible = false;
            donePanel.Visible = true;
            SetStatus("status_done");

            // A extração só servia de entrada para o relinker; o jogo convertido já tem a sua cópia em app0/.
            DiscardExtraction();
            if (recycleSourceBox.Checked && sourceInput is not null)
                RecycleSource(SourceCleanup.SourceToRecycle(sourceInput, game.Folder), Log);

            // Deu certo: o registro continua em "Mostrar detalhes", mas não fica ocupando o disco.
            File.Delete(logFile);
        }
        catch (OperationCanceledException)
        {
            SetStatus("status_cancelled", true);
        }
        catch (UserException e)
        {
            Log("ERROR " + T(e.Key, e.Args));
            SetStatus("status_failed", true);
            ShowError(T(e.Key, e.Args) + "\n\n" + T("log_saved", logFile));
        }
        catch (Exception e)
        {
            Log(e.ToString());
            SetStatus("status_failed", true);
            ShowError(T("err_unexpected", e.Message) + "\n\n" + T("log_saved", logFile));
        }
        finally
        {
            running.Dispose();
            running = null;
            SetBusy(false);
            if (convertedExe is null)
                percentLabel.Text = "";
            // Se a conversão falhou depois de mover a extração para app0/, ela ficou incompleta: pede o jogo de novo.
            if (convertedExe is null && extractedFolder is not null && game is not null && !File.Exists(Path.Combine(game.Folder, "eboot.bin")))
                ResetGame();
        }
    }

    async Task PlayAsync()
    {
        if (convertedExe is null)
            return;
        var folder = Path.GetDirectoryName(convertedExe)!;
        playButton.Enabled = false;
        try
        {
            using var process = Process.Start(new ProcessStartInfo(convertedExe) { WorkingDirectory = folder, UseShellExecute = true })!;
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
                return;
            var log = Path.Combine(folder, "anyps5.log");
            var tail = File.Exists(log) ? string.Join(Environment.NewLine, File.ReadLines(log).TakeLast(12)) : "—";
            ShowError(T("err_game_exit", process.ExitCode, tail));
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException)
        {
            ShowError(T("err_unexpected", e.Message));
        }
        finally
        {
            playButton.Enabled = true;
        }
    }

    void OpenFolder()
    {
        if (convertedExe is not null)
            Process.Start("explorer.exe", $"/select,\"{convertedExe}\"");
    }

    void ToggleDetails()
    {
        logBox.Visible = !logBox.Visible;
        ClientSize = new Size(ClientSize.Width, LogicalToDeviceUnits(logBox.Visible ? BaseHeight + LogHeight : BaseHeight));
        detailsLink.Text = logBox.Visible ? T("hide_details") : T("show_details");
    }

    /// <summary>Registro da conversão; só fica no disco se ela falhar.</summary>
    static string ConversionLogPath(GameInfo info)
    {
        Directory.CreateDirectory(Converter.LogFolder);
        return Path.Combine(Converter.LogFolder, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{info.SafeName}.log");
    }

    /// <summary>Apaga a extração atual e a pasta de trabalho oculta no local onde ela foi feita.</summary>
    void DiscardExtraction()
    {
        Archives.TryDelete(extractedFolder);
        if (extractionParent is not null)
            Archives.DeleteWorkRoot(extractionParent);
        extractedFolder = extractionParent = null;
    }

    /// <summary>Opção do usuário: manda o arquivo compactado ou a pasta do jogo para a Lixeira.</summary>
    void RecycleSource(string source, Action<string> log)
    {
        if (!SourceCleanup.IsSafeToRecycle(source, outputBox.Text, [AppContext.BaseDirectory]))
        {
            log("source kept, protected location: " + source);
            ShowError(T("err_recycle_unsafe", source));
            return;
        }
        try
        {
            if (File.Exists(source))
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(source,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            else if (Directory.Exists(source))
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(source,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            log("moved to the Recycle Bin: " + source);
            SetStatus("status_done_recycled");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            log("source kept: " + e.Message);
            ShowError(T("err_recycle", source, e.Message));
        }
    }

    void ShowError(string message) =>
        MessageDialog.Show(this, T("err_heading"), message);

    static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    void EnableDrop(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += (_, e) =>
        {
            var ok = running is null && e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
            e.Effect = ok ? DragDropEffects.Copy : DragDropEffects.None;
            dropZone.Highlight = ok;
        };
        control.DragLeave += (_, _) => dropZone.Highlight = false;
        control.DragDrop += (_, e) =>
        {
            dropZone.Highlight = false;
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
                BeginInvoke(async () => await LoadGameAsync(paths[0]));
        };
        foreach (Control child in control.Controls)
            EnableDrop(child);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }
}
