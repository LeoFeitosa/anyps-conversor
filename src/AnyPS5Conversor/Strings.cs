using System.Globalization;

namespace AnyPS5Conversor;

public enum Language { Portuguese, English }

/// <summary>Textos da interface em português e inglês. A escolha fica salva em %APPDATA%.</summary>
public static class Strings
{
    static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AnyPS5-Conversor", "language.txt");

    public static Language Current { get; private set; } = LoadLanguage();

    public static void Set(Language language)
    {
        Current = language;
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
        ["status_libs"] = ("Instalando as bibliotecas do AnyPS5…", "Installing the AnyPS5 libraries…"),
        ["status_done"] = ("Jogo convertido. Clique em Jogar agora.", "Game converted. Click Play now."),
        ["status_failed"] = ("A conversão não deu certo.", "The conversion did not succeed."),
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
        ["err_archive_password"] = ("O arquivo {0} tem senha. Extraia com a senha e escolha a pasta.", "{0} is password protected. Extract it with the password and choose the folder."),
        ["err_archive_unsafe"] = ("O arquivo compactado tenta gravar fora da pasta de destino e foi recusado.", "The packed file tries to write outside the target folder and was refused."),
        ["err_many_games"] = ("Encontrei mais de um jogo aqui ({0}). Escolha a pasta de um deles.", "More than one game was found here ({0}). Choose the folder of one of them."),
        ["log_saved"] = ("O registro completo foi salvo em:\n{0}", "The full log was saved to:\n{0}"),
        ["err_syscall"] = ("Este jogo fala direto com o sistema do PS5 (chamadas de sistema), algo que o AnyPS5 ainda não consegue converter. Não é um problema do seu arquivo.\n\n{0}",
                           "This game talks directly to the PS5 system (system calls), which AnyPS5 cannot convert yet. Your file is not the problem.\n\n{0}"),
        ["err_unexpected"] = ("Erro inesperado: {0}", "Unexpected error: {0}"),
        ["err_cpu"] = ("Seu processador não tem instruções que os jogos de PS5 exigem ({0}). A conversão funciona, mas o jogo não vai abrir neste PC.",
                       "Your processor lacks instructions PS5 games require ({0}). Conversion works, but the game will not start on this PC."),
        ["err_game_exit"] = ("O jogo fechou com erro (código {0}).\n\nÚltimas linhas do registro:\n\n{1}",
                             "The game closed with an error (code {0}).\n\nLast lines of the log:\n\n{1}"),
        ["err_title"] = ("AnyPS5 Conversor", "AnyPS5 Converter"),

        ["pick_game"] = ("Escolha a pasta do jogo (a que contém o eboot.bin)", "Choose the game folder (the one with eboot.bin)"),
        ["pick_file"] = ("Escolha o arquivo do jogo (.zip, .rar, .7z…) ou o eboot.bin", "Choose the game file (.zip, .rar, .7z…) or eboot.bin"),
        ["file_filter"] = ("Jogo compactado ou eboot.bin", "Packed game or eboot.bin"),
        ["pick_output"] = ("Escolha onde salvar o jogo convertido", "Choose where to save the converted game"),
        ["footer_based"] = ("Baseado no AnyPS5 · GPL-2.0", "Based on AnyPS5 · GPL-2.0"),
        ["footer_disclaimer"] = ("Projeto independente, sem vínculo com a Sony.", "Independent project, not affiliated with Sony."),
        ["app_name"] = ("AnyPS5 Conversor", "AnyPS5 Converter"),
    };
}
