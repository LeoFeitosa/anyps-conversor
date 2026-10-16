using System.Globalization;

namespace AnyPSConversor;

public enum Language { Portuguese, English }

/// <summary>Textos da interface em português e inglês. A escolha fica salva em %APPDATA%.</summary>
public static class Strings
{
    static readonly string SettingsFile = Path.Combine(
        Settings.Folder, "language.txt");

    public static Language Current { get; private set; } = LoadLanguage();

    /// <param name="save">Falso só para a imagem do README: troca o idioma sem gravar a escolha do usuário.</param>
    public static void Set(Language language, bool save = true)
    {
        Current = language;
        if (!save)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, language == Language.Portuguese ? "pt" : "en");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public static string T(string key, params object[] args)
    {
        if (!Texts.TryGetValue(key, out var text))
            return key;
        var value = Current == Language.Portuguese ? text.Pt : text.En;
        return args.Length == 0 ? value : string.Format(value, args);
    }

    static Language LoadLanguage()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return File.ReadAllText(SettingsFile).Trim() == "pt" ? Language.Portuguese : Language.English;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? Language.Portuguese : Language.English;
    }

    static readonly Dictionary<string, (string Pt, string En)> Texts = new()
    {
        ["app_subtitle"] = ("Converta jogos e apps de PS5 para rodar no Windows", "Convert PS5 games and apps to run on Windows"),

        ["step1_title"] = ("Escolha o jogo", "Choose the game"),
        ["step1_hint"] = ("A pasta do jogo (onde fica o eboot.bin) ou um arquivo .zip, .rar ou .7z com ela.", "The game folder (where eboot.bin is) or a .zip, .rar or .7z file containing it."),
        ["drop_text"] = ("Arraste a pasta ou o arquivo do jogo para cá", "Drag the game folder or file here"),
        ["drop_or"] = ("ou", "or"),
        ["browse_folder"] = ("Escolher pasta…", "Choose folder…"),
        ["browse_file"] = ("Escolher arquivo…", "Choose file…"),
        ["change"] = ("Trocar…", "Change…"),
        ["game_id"] = ("Código do título: {0}", "Title ID: {0}"),
        ["game_ready"] = ("Pronto para converter", "Ready to convert"),
        ["game_no_param"] = ("Falta o arquivo sce_sys/param.json. Alguns jogos precisam dele para abrir.", "The sce_sys/param.json file is missing. Some games need it to start."),
        ["legal_hint"] = ("Nenhum jogo é incluído. Use apenas cópias suas ou homebrew.", "No games are included. Use only your own copies or homebrew."),

        ["step2_title"] = ("Onde salvar", "Where to save"),
        ["step2_hint"] = ("Uma pasta nova com o jogo convertido será criada aqui.", "A new folder with the converted game will be created here."),

        ["recycle_source"] = ("Ao terminar, mover os arquivos originais do jogo para a Lixeira (fica só a versão convertida)",
                              "When done, move the original game files to the Recycle Bin (keep only the converted version)"),
        ["step3_title"] = ("Converter", "Convert"),
        ["convert"] = ("Converter para Windows", "Convert to Windows"),
        ["cancel"] = ("Cancelar", "Cancel"),
        ["show_details"] = ("Mostrar detalhes", "Show details"),
        ["hide_details"] = ("Ocultar detalhes", "Hide details"),
        ["status_idle"] = ("Escolha um jogo para começar.", "Choose a game to begin."),
        ["status_ready"] = ("Tudo certo. Clique em Converter para Windows.", "All set. Click Convert to Windows."),
        ["status_preparing"] = ("Preparando as ferramentas…", "Preparing the tools…"),
        ["status_unwrapping"] = ("Lendo o executável do jogo…", "Reading the game executable…"),
        ["status_extracting"] = ("Extraindo o arquivo…", "Extracting the file…"),
        ["status_relinking"] = ("Gerando o executável do Windows…", "Building the Windows executable…"),
        ["status_copying"] = ("Copiando os arquivos do jogo…", "Copying the game files…"),
        ["step_extracting"] = ("Extraindo {0}", "Extracting {0}"),
        ["step_reading"] = ("Lendo {0}", "Reading {0}"),
        ["step_stage0"] = ("Analisando as instruções · {0}", "Scanning instructions · {0}"),
        ["step_stage1"] = ("Procurando chamadas de sistema · {0}", "Looking for system calls · {0}"),
        ["step_stage2"] = ("Convertendo as bibliotecas do jogo · {0}", "Converting the game libraries · {0}"),
        ["step_stage3"] = ("Montando o executável · {0}", "Assembling the executable · {0}"),
        ["step_stage4"] = ("Gravando o executável · {0}", "Writing the executable · {0}"),
        ["step_libs"] = ("Instalando as bibliotecas do AnyPS5", "Installing the AnyPS5 libraries"),
        ["step_copying"] = ("Copiando {0}", "Copying {0}"),
        ["step_moving"] = ("Movendo {0}", "Moving {0}"),
        ["eta_under_minute"] = ("· menos de 1 minuto restante", "· less than a minute left"),
        ["eta_minutes"] = ("· cerca de {0} min restantes", "· about {0} min left"),
        ["status_done"] = ("Jogo convertido. Clique em Jogar agora.", "Game converted. Click Play now."),
        ["status_done_recycled"] = ("Jogo convertido. Os arquivos originais foram para a Lixeira.", "Game converted. The original files were moved to the Recycle Bin."),
        ["status_failed"] = ("A conversão não deu certo.", "The conversion did not succeed."),
        ["status_cancelling"] = ("Cancelando e apagando o que já foi gerado…", "Cancelling and removing what was already created…"),
        ["status_cancelled"] = ("Conversão cancelada.", "Conversion cancelled."),

        ["done_title"] = ("Jogo convertido com sucesso", "Game converted successfully"),
        ["play"] = ("▶  Jogar agora", "▶  Play now"),
        ["open_folder"] = ("Abrir pasta", "Open folder"),

        ["err_no_eboot"] = ("Não encontrei o eboot.bin nesta pasta. Escolha a pasta do jogo que contém o arquivo eboot.bin.",
                            "eboot.bin was not found in this folder. Choose the game folder that contains eboot.bin."),
        ["err_not_executable"] = ("O arquivo {0} não é um executável de PS5 reconhecido.", "{0} is not a recognized PS5 executable."),
        ["err_encrypted"] = ("O arquivo {0} está criptografado. Este programa só converte jogos extraídos já descriptografados.",
                             "{0} is encrypted. This program only converts games that were dumped already decrypted."),
        ["err_unwrap"] = ("Não foi possível ler o arquivo {0}: {1}", "Could not read {0}: {1}"),
        ["err_relinker"] = ("O conversor do AnyPS5 recusou este jogo:\n\n{0}", "The AnyPS5 converter rejected this game:\n\n{0}"),
        ["err_archive"] = ("Não foi possível abrir o arquivo {0}: {1}", "Could not open {0}: {1}"),
        ["password_title"] = ("Arquivo protegido por senha", "Password-protected file"),
        ["password_message"] = ("O arquivo {0} tem senha. Digite a senha para extrair o jogo. Ela só é usada para abrir o arquivo e não fica salva.",
                                "{0} is password protected. Type the password to extract the game. It is only used to open the file and is not saved."),
        ["password_wrong"] = ("Senha incorreta. Confira e tente de novo.", "Wrong password. Check it and try again."),
        ["password_show"] = ("Mostrar senha", "Show password"),
        ["password_ok"] = ("Extrair", "Extract"),
        ["err_archive_unsafe"] = ("O arquivo compactado tenta gravar fora da pasta de destino e foi recusado.", "The packed file tries to write outside the target folder and was refused."),
        ["err_no_eboot_archive"] = ("O arquivo {0} foi extraído, mas não tem o eboot.bin de um jogo. Ele pode estar incompleto ou ter outro conteúdo. Escolha um arquivo ou uma pasta com o jogo completo.",
                                    "{0} was extracted, but it does not contain a game's eboot.bin. It may be incomplete or hold something else. Choose a file or folder with the complete game."),
        ["err_many_games"] = ("Encontrei mais de um jogo aqui ({0}). Escolha a pasta de um deles.", "More than one game was found here ({0}). Choose the folder of one of them."),
        ["log_saved"] = ("O registro completo foi salvo em:\n{0}", "The full log was saved to:\n{0}"),
        ["err_syscall"] = ("Este jogo fala direto com o sistema do PS5 (chamadas de sistema), algo que o AnyPS5 ainda não consegue converter. Não é um problema do seu arquivo.\n\n{0}",
                           "This game talks directly to the PS5 system (system calls), which AnyPS5 cannot convert yet. Your file is not the problem.\n\n{0}"),
        ["err_recycle_unsafe"] = ("Os arquivos originais foram mantidos: {0} é uma pasta protegida ou contém o destino da conversão.",
                                  "The original files were kept: {0} is a protected folder or contains the conversion output."),
        ["err_recycle"] = ("O jogo foi convertido, mas não foi possível mover {0} para a Lixeira: {1}",
                           "The game was converted, but {0} could not be moved to the Recycle Bin: {1}"),
        ["err_disk_space"] = ("Não há espaço livre suficiente no disco {1} para continuar, e nada foi alterado.\n\nNecessário: {0}\nDisponível: {2}\n\nLibere pelo menos {3} e tente de novo.",
                              "There is not enough free space on drive {1} to continue, and nothing was changed.\n\nRequired: {0}\nAvailable: {2}\n\nFree at least {3} and try again."),
        ["err_disk_full"] = ("O disco {0} ficou cheio durante o processo. Os arquivos incompletos foram apagados. Libere espaço e tente de novo.",
                             "Drive {0} filled up during the process. The incomplete files were deleted. Free some space and try again."),
        ["err_unexpected"] = ("Erro inesperado: {0}", "Unexpected error: {0}"),
        ["err_cpu"] = ("Seu processador não tem instruções que os jogos de PS5 exigem ({0}). A conversão funciona, mas o jogo não vai abrir neste PC.",
                       "Your processor lacks instructions PS5 games require ({0}). Conversion works, but the game will not start on this PC."),
        ["err_game_exit"] = ("O jogo fechou com erro (código {0}).\n\nÚltimas linhas do registro:\n\n{1}",
                             "The game closed with an error (code {0}).\n\nLast lines of the log:\n\n{1}"),
        ["err_heading"] = ("Atenção", "Attention"),

        ["pick_game"] = ("Escolha a pasta do jogo (a que contém o eboot.bin)", "Choose the game folder (the one with eboot.bin)"),
        ["pick_file"] = ("Escolha o arquivo do jogo (.zip, .rar, .7z…) ou o eboot.bin", "Choose the game file (.zip, .rar, .7z…) or eboot.bin"),
        ["file_filter"] = ("Jogo compactado ou eboot.bin", "Packed game or eboot.bin"),
        ["pick_output"] = ("Escolha onde salvar o jogo convertido", "Choose where to save the converted game"),
        ["footer_based"] = ("Baseado no AnyPS5 · GPL-2.0", "Based on AnyPS5 · GPL-2.0"),
        ["footer_disclaimer"] = ("Projeto independente, sem vínculo com a Sony.", "Independent project, not affiliated with Sony."),
        ["footer_licenses"] = ("Licenças", "Licenses"),
        ["licenses_title"] = ("Licenças de terceiros", "Third-party licenses"),
        ["app_name"] = ("AnyPS Conversor", "AnyPS Converter"),
    };
}
