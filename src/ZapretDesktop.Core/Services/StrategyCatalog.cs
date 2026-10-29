using ZapretDesktop.Core.Models;

namespace ZapretDesktop.Core.Services;

/// <summary>
/// Встроенные стратегии обхода DPI.
/// Основаны на рабочих наборах Flowseal / bol-van zapret (general, ALT, ALT2, ALT3, FAKE).
/// </summary>
public static class StrategyCatalog
{
    public const string CategoryAll = "Все";
    public const string CategoryUniversal = "Универсальные";
    public const string CategoryDiscord = "Discord";
    public const string CategoryYouTube = "YouTube";
    public const string CategorySimple = "Простые";

    /// <summary>
    /// Встроенные стратегии, которые дублируют набор Flowseal.
    /// Когда из GitHub загружены актуальные стратегии, эти пункты убираются из списка.
    /// </summary>
    public static readonly string[] FlowsealLegacyIds = { "general", "alt", "alt2", "alt3", "alt4" };

    private const string Excludes =
        "--hostlist-exclude=\"{LISTS}\\list-exclude.txt\" --hostlist-exclude=\"{LISTS}\\list-exclude-user.txt\" " +
        "--ipset-exclude=\"{LISTS}\\ipset-exclude.txt\" --ipset-exclude=\"{LISTS}\\ipset-exclude-user.txt\"";

    private const string Wf =
        "--wf-tcp=80,443,2053,2083,2087,2096,8443,{GAME_TCP} --wf-udp=443,19294-19344,50000-50100,{GAME_UDP}";

    public static List<Preset> GetDefaults() => new()
    {
        new Preset
        {
            Id = "general",
            Name = "General (Fake + MultiSplit)",
            Description = "Базовая универсальная стратегия: Discord (TCP/UDP), YouTube, Telegram и остальные сервисы из списков.",
            Category = CategoryUniversal,
            IsBuiltIn = true,
            Arguments =
                Wf + " " +
                $"--filter-udp=443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" {Excludes} " +
                "--dpi-desync=fake --dpi-desync-repeats=6 --dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                "--filter-udp=19294-19344,50000-50100 --filter-l7=discord,stun --dpi-desync=fake " +
                "--dpi-desync-fake-discord=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-fake-stun=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-repeats=6 --new " +
                "--filter-tcp=2053,2083,2087,2096,8443 --hostlist-domains=discord.media --dpi-desync=multisplit " +
                "--dpi-desync-split-seqovl=681 --dpi-desync-split-pos=1 --dpi-desync-split-seqovl-pattern=\"{BIN}\\tls_clienthello_www_google_com.bin\" --new " +
                $"--filter-tcp=443 --hostlist=\"{Lists("list-google.txt")}\" --ip-id=zero --dpi-desync=multisplit " +
                "--dpi-desync-split-seqovl=681 --dpi-desync-split-pos=1 --dpi-desync-split-seqovl-pattern=\"{BIN}\\tls_clienthello_www_google_com.bin\" --new " +
                $"--filter-tcp=80,443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" {Excludes} " +
                "--dpi-desync=multisplit --dpi-desync-split-seqovl=568 --dpi-desync-split-pos=1 --dpi-desync-split-seqovl-pattern=\"{BIN}\\tls_clienthello_4pda_to.bin\" --new " +
                $"--filter-udp=443 --ipset=\"{Lists("ipset-all.txt")}\" {Excludes} --dpi-desync=fake --dpi-desync-repeats=6 " +
                "--dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                $"--filter-tcp=80,443,8443 --ipset=\"{Lists("ipset-all.txt")}\" {Excludes} --dpi-desync=multisplit " +
                "--dpi-desync-split-seqovl=568 --dpi-desync-split-pos=1 --dpi-desync-split-seqovl-pattern=\"{BIN}\\tls_clienthello_4pda_to.bin\" --new " +
                $"--filter-tcp={{GAME_TCP}} --ipset=\"{Lists("ipset-all.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude-user.txt")}\" " +
                "--dpi-desync=multisplit --dpi-desync-any-protocol=1 --dpi-desync-cutoff=n3 --dpi-desync-split-seqovl=568 " +
                "--dpi-desync-split-pos=1 --dpi-desync-split-seqovl-pattern=\"{BIN}\\tls_clienthello_4pda_to.bin\" --new " +
                $"--filter-udp={{GAME_UDP}} --ipset=\"{Lists("ipset-all.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude-user.txt")}\" " +
                "--dpi-desync=fake --dpi-desync-repeats=12 --dpi-desync-any-protocol=1 " +
                "--dpi-desync-fake-unknown-udp=\"{BIN}\\ACTIVE_GAME_UDP.bin\" --dpi-desync-cutoff=n2"
        },
        new Preset
        {
            Id = "alt",
            Name = "ALT (Fake + FakeSplit)",
            Description = "Альтернатива для провайдеров, где не работает multiSplit. Fake + fakeSplit с фулингом ts.",
            Category = CategoryUniversal,
            IsBuiltIn = true,
            Arguments =
                Wf + " " +
                $"--filter-udp=443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" {Excludes} " +
                "--dpi-desync=fake --dpi-desync-repeats=6 --dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                "--filter-udp=19294-19344,50000-50100 --filter-l7=discord,stun --dpi-desync=fake " +
                "--dpi-desync-fake-discord=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-fake-stun=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-repeats=6 --new " +
                "--filter-tcp=2053,2083,2087,2096,8443 --hostlist-domains=discord.media --dpi-desync=fake,fakedsplit " +
                "--dpi-desync-repeats=6 --dpi-desync-fooling=ts --dpi-desync-fakedsplit-pattern=0x00 " +
                "--dpi-desync-fake-tls=\"{BIN}\\tls_clienthello_www_google_com.bin\" --new " +
                $"--filter-tcp=443 --hostlist=\"{Lists("list-google.txt")}\" --ip-id=zero --dpi-desync=fake,fakedsplit " +
                "--dpi-desync-repeats=6 --dpi-desync-fooling=ts --dpi-desync-fakedsplit-pattern=0x00 " +
                "--dpi-desync-fake-tls=\"{BIN}\\tls_clienthello_www_google_com.bin\" --new " +
                $"--filter-tcp=80,443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" {Excludes} " +
                "--dpi-desync=fake,fakedsplit --dpi-desync-repeats=6 --dpi-desync-fooling=ts --dpi-desync-fakedsplit-pattern=0x00 " +
                "--dpi-desync-fake-tls=\"{BIN}\\stun.bin\" --dpi-desync-fake-tls=\"{BIN}\\tls_clienthello_www_google_com.bin\" " +
                "--dpi-desync-fake-http=\"{BIN}\\tls_clienthello_max_ru.bin\" --new " +
                $"--filter-udp=443 --ipset=\"{Lists("ipset-all.txt")}\" {Excludes} --dpi-desync=fake --dpi-desync-repeats=6 " +
                "--dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                $"--filter-tcp=80,443,8443 --ipset=\"{Lists("ipset-all.txt")}\" {Excludes} --dpi-desync=fake,fakedsplit " +
                "--dpi-desync-repeats=6 --dpi-desync-fooling=ts --dpi-desync-fakedsplit-pattern=0x00 " +
                "--dpi-desync-fake-tls=\"{BIN}\\stun.bin\" --dpi-desync-fake-tls=\"{BIN}\\tls_clienthello_www_google_com.bin\" " +
                "--dpi-desync-fake-http=\"{BIN}\\tls_clienthello_max_ru.bin\" --new " +
                $"--filter-tcp={{GAME_TCP}} --ipset=\"{Lists("ipset-all.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude-user.txt")}\" " +
                "--dpi-desync=fake,fakedsplit --dpi-desync-repeats=6 --dpi-desync-any-protocol=1 --dpi-desync-cutoff=n4 " +
                "--dpi-desync-fooling=ts --dpi-desync-fakedsplit-pattern=0x00 " +
                "--dpi-desync-fake-tls=\"{BIN}\\stun.bin\" --dpi-desync-fake-tls=\"{BIN}\\tls_clienthello_www_google_com.bin\" " +
                "--dpi-desync-fake-http=\"{BIN}\\tls_clienthello_max_ru.bin\" --dpi-desync-fake-unknown=\"{BIN}\\stun.bin\" " +
                "--dpi-desync-fake-unknown=\"{BIN}\\tls_clienthello_www_google_com.bin\" --new " +
                $"--filter-udp={{GAME_UDP}} --ipset=\"{Lists("ipset-all.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude-user.txt")}\" " +
                "--dpi-desync=fake --dpi-desync-repeats=12 --dpi-desync-any-protocol=1 " +
                "--dpi-desync-fake-unknown-udp=\"{BIN}\\ACTIVE_GAME_UDP.bin\" --dpi-desync-cutoff=n3"
        },
        new Preset
        {
            Id = "alt2",
            Name = "ALT2 (MultiSplit 652)",
            Description = "MultiSplit с другими параметрами нарезки (652 / pos 2). Хорошо помогает на части провайдеров.",
            Category = CategoryUniversal,
            IsBuiltIn = true,
            Arguments =
                Wf + " " +
                $"--filter-udp=443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" {Excludes} " +
                "--dpi-desync=fake --dpi-desync-repeats=6 --dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                "--filter-udp=19294-19344,50000-50100 --filter-l7=discord,stun --dpi-desync=fake " +
                "--dpi-desync-fake-discord=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-fake-stun=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-repeats=6 --new " +
                "--filter-tcp=2053,2083,2087,2096,8443 --hostlist-domains=discord.media --dpi-desync=multisplit " +
                "--dpi-desync-split-seqovl=652 --dpi-desync-split-pos=2 --dpi-desync-split-seqovl-pattern=\"{BIN}\\tls_clienthello_www_google_com.bin\" --new " +
                $"--filter-tcp=443 --hostlist=\"{Lists("list-google.txt")}\" --ip-id=zero --dpi-desync=multisplit " +
                "--dpi-desync-split-seqovl=652 --dpi-desync-split-pos=2 --dpi-desync-split-seqovl-pattern=\"{BIN}\\tls_clienthello_www_google_com.bin\" --new " +
                $"--filter-tcp=80,443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" {Excludes} " +
                "--dpi-desync=multisplit --dpi-desync-split-seqovl=652 --dpi-desync-split-pos=2 " +
                "--dpi-desync-split-seqovl-pattern=\"{BIN}\\tls_clienthello_www_google_com.bin\" --new " +
                $"--filter-udp=443 --ipset=\"{Lists("ipset-all.txt")}\" {Excludes} --dpi-desync=fake --dpi-desync-repeats=6 " +
                "--dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                $"--filter-tcp=80,443,8443 --ipset=\"{Lists("ipset-all.txt")}\" {Excludes} --dpi-desync=multisplit " +
                "--dpi-desync-split-seqovl=652 --dpi-desync-split-pos=2 --dpi-desync-split-seqovl-pattern=\"{BIN}\\tls_clienthello_www_google_com.bin\" --new " +
                $"--filter-tcp={{GAME_TCP}} --ipset=\"{Lists("ipset-all.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude-user.txt")}\" " +
                "--dpi-desync=multisplit --dpi-desync-any-protocol=1 --dpi-desync-cutoff=n3 --dpi-desync-split-seqovl=652 " +
                "--dpi-desync-split-pos=2 --dpi-desync-split-seqovl-pattern=\"{BIN}\\tls_clienthello_www_google_com.bin\" --new " +
                $"--filter-udp={{GAME_UDP}} --ipset=\"{Lists("ipset-all.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude-user.txt")}\" " +
                "--dpi-desync=fake --dpi-desync-repeats=12 --dpi-desync-any-protocol=1 " +
                "--dpi-desync-fake-unknown-udp=\"{BIN}\\ACTIVE_GAME_UDP.bin\" --dpi-desync-cutoff=n2"
        },
        new Preset
        {
            Id = "alt3",
            Name = "ALT3 (HostFakeSplit)",
            Description = "hostFakeSplit с подменой SNI на разрешённый домен. Для жёсткого DPI.",
            Category = CategoryUniversal,
            IsBuiltIn = true,
            Arguments =
                Wf + " " +
                $"--filter-udp=443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" {Excludes} " +
                "--dpi-desync=fake --dpi-desync-repeats=6 --dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                "--filter-udp=19294-19344,50000-50100 --filter-l7=discord,stun --dpi-desync=fake " +
                "--dpi-desync-fake-discord=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-fake-stun=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-repeats=6 --new " +
                "--filter-tcp=2053,2083,2087,2096,8443 --hostlist-domains=discord.media --dpi-desync=fake,hostfakesplit " +
                "--dpi-desync-fake-tls-mod=rnd,dupsid,sni=www.google.com --dpi-desync-hostfakesplit-mod=host=www.google.com,altorder=1 --dpi-desync-fooling=ts --new " +
                $"--filter-tcp=443 --hostlist=\"{Lists("list-google.txt")}\" --ip-id=zero --dpi-desync=fake,hostfakesplit " +
                "--dpi-desync-fake-tls-mod=rnd,dupsid,sni=www.google.com --dpi-desync-hostfakesplit-mod=host=www.google.com,altorder=1 --dpi-desync-fooling=ts --new " +
                $"--filter-tcp=80,443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" {Excludes} " +
                "--dpi-desync=fake,hostfakesplit --dpi-desync-fake-tls-mod=rnd,dupsid,sni=ya.ru " +
                "--dpi-desync-hostfakesplit-mod=host=ya.ru,altorder=1 --dpi-desync-fooling=ts " +
                "--dpi-desync-fake-http=\"{BIN}\\tls_clienthello_max_ru.bin\" --new " +
                $"--filter-udp=443 --ipset=\"{Lists("ipset-all.txt")}\" {Excludes} --dpi-desync=fake --dpi-desync-repeats=6 " +
                "--dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                $"--filter-tcp=80,443,8443 --ipset=\"{Lists("ipset-all.txt")}\" {Excludes} --dpi-desync=fake,hostfakesplit " +
                "--dpi-desync-fake-tls-mod=rnd,dupsid,sni=ya.ru --dpi-desync-hostfakesplit-mod=host=ya.ru,altorder=1 " +
                "--dpi-desync-fooling=ts --dpi-desync-fake-http=\"{BIN}\\tls_clienthello_max_ru.bin\" --new " +
                $"--filter-tcp={{GAME_TCP}} --ipset=\"{Lists("ipset-all.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude-user.txt")}\" " +
                "--dpi-desync=fake,hostfakesplit --dpi-desync-any-protocol=1 --dpi-desync-cutoff=n4 " +
                "--dpi-desync-fake-tls-mod=rnd,dupsid,sni=ya.ru --dpi-desync-hostfakesplit-mod=host=ya.ru,altorder=1 " +
                "--dpi-desync-fooling=ts --dpi-desync-fake-http=\"{BIN}\\tls_clienthello_max_ru.bin\" " +
                "--dpi-desync-fake-unknown=\"{BIN}\\tls_clienthello_max_ru.bin\" --new " +
                $"--filter-udp={{GAME_UDP}} --ipset=\"{Lists("ipset-all.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude-user.txt")}\" " +
                "--dpi-desync=fake --dpi-desync-repeats=10 --dpi-desync-any-protocol=1 " +
                "--dpi-desync-fake-unknown-udp=\"{BIN}\\ACTIVE_GAME_UDP.bin\" --dpi-desync-cutoff=n4"
        },
        new Preset
        {
            Id = "alt4",
            Name = "ALT4 (Fake + MultiDisorder)",
            Description = "Агрессивная стратегия: fake + multiDisorder с badseq. Для самых сложных случаев.",
            Category = CategoryUniversal,
            IsBuiltIn = true,
            Arguments =
                Wf + " " +
                $"--filter-udp=443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" {Excludes} " +
                "--dpi-desync=fake --dpi-desync-repeats=11 --dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                "--filter-udp=19294-19344,50000-50100 --filter-l7=discord,stun --dpi-desync=fake " +
                "--dpi-desync-fake-discord=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-fake-stun=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-repeats=6 --new " +
                "--filter-tcp=2053,2083,2087,2096,8443 --hostlist-domains=discord.media --dpi-desync=fake,multidisorder " +
                "--dpi-desync-split-pos=1,midsld --dpi-desync-repeats=11 --dpi-desync-fooling=badseq " +
                "--dpi-desync-fake-tls=0x00000000 --dpi-desync-fake-tls=! " +
                "--dpi-desync-fake-tls-mod=rnd,dupsid,sni=www.google.com --new " +
                $"--filter-tcp=443 --hostlist=\"{Lists("list-google.txt")}\" --ip-id=zero --dpi-desync=fake,multidisorder " +
                "--dpi-desync-split-pos=1,midsld --dpi-desync-repeats=11 --dpi-desync-fooling=badseq " +
                "--dpi-desync-fake-tls=0x00000000 --dpi-desync-fake-tls=! " +
                "--dpi-desync-fake-tls-mod=rnd,dupsid,sni=www.google.com --new " +
                $"--filter-tcp=80,443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" {Excludes} " +
                "--dpi-desync=fake,multidisorder --dpi-desync-split-pos=1,midsld --dpi-desync-repeats=11 " +
                "--dpi-desync-fooling=badseq --dpi-desync-fake-tls=0x00000000 --dpi-desync-fake-tls=! " +
                "--dpi-desync-fake-tls-mod=rnd,dupsid,sni=www.google.com --dpi-desync-fake-http=\"{BIN}\\tls_clienthello_max_ru.bin\" --new " +
                $"--filter-udp=443 --ipset=\"{Lists("ipset-all.txt")}\" {Excludes} --dpi-desync=fake --dpi-desync-repeats=11 " +
                "--dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                $"--filter-tcp=80,443,8443 --ipset=\"{Lists("ipset-all.txt")}\" {Excludes} --dpi-desync=fake,multidisorder " +
                "--dpi-desync-split-pos=1,midsld --dpi-desync-repeats=11 --dpi-desync-fooling=badseq " +
                "--dpi-desync-fake-tls=0x00000000 --dpi-desync-fake-tls=! " +
                "--dpi-desync-fake-tls-mod=rnd,dupsid,sni=www.google.com --dpi-desync-fake-http=\"{BIN}\\tls_clienthello_max_ru.bin\" --new " +
                $"--filter-tcp={{GAME_TCP}} --ipset=\"{Lists("ipset-all.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude-user.txt")}\" " +
                "--dpi-desync=fake,multidisorder --dpi-desync-any-protocol=1 --dpi-desync-cutoff=n4 " +
                "--dpi-desync-split-pos=1,midsld --dpi-desync-repeats=11 --dpi-desync-fooling=badseq " +
                "--dpi-desync-fake-tls=0x00000000 --dpi-desync-fake-tls=! " +
                "--dpi-desync-fake-tls-mod=rnd,dupsid,sni=www.google.com " +
                "--dpi-desync-fake-http=\"{BIN}\\tls_clienthello_max_ru.bin\" --dpi-desync-fake-unknown=0x00000000 " +
                "--dpi-desync-fake-unknown=\"{BIN}\\tls_clienthello_max_ru.bin\" --new " +
                $"--filter-udp={{GAME_UDP}} --ipset=\"{Lists("ipset-all.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude.txt")}\" --ipset-exclude=\"{Lists("ipset-exclude-user.txt")}\" " +
                "--dpi-desync=fake --dpi-desync-repeats=10 --dpi-desync-any-protocol=1 " +
                "--dpi-desync-fake-unknown-udp=\"{BIN}\\ACTIVE_GAME_UDP.bin\" --dpi-desync-cutoff=n2"
        },
        new Preset
        {
            Id = "simple-fake",
            Name = "Simple Fake",
            Description = "Простая и быстрая стратегия на fake. Меньше всего грузит систему.",
            Category = CategorySimple,
            IsBuiltIn = true,
            Arguments =
                "--wf-tcp=80,443,{GAME_TCP} --wf-udp=443,{GAME_UDP} " +
                "--dpi-desync=fake --dpi-desync-repeats=6 --dpi-desync-fooling=ts " +
                "--dpi-desync-fake-tls=\"{BIN}\\tls_clienthello_www_google_com.bin\" " +
                "--dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\""
        },
        new Preset
        {
            Id = "simple-split2",
            Name = "Universal (Fake + Split2)",
            Description = "Простая схема, которая чаще всего заводится: fake + split2 с md5sig. Хороша для Discord и YouTube.",
            Category = CategoryUniversal,
            IsBuiltIn = true,
            Arguments =
                "--wf-tcp=80,443 --wf-udp=443,50000-65535 " +
                "--dpi-desync=fake,split2 --dpi-desync-repeats=6 --dpi-desync-fooling=md5sig " +
                $"--hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\""
        },
        new Preset
        {
            Id = "discord-voice-simple",
            Name = "Discord Voice (Simple)",
            Description = "Облегчённый вариант для голоса Discord: fake + split2, порты 3478-3481 и 50000-65535.",
            Category = CategoryDiscord,
            IsBuiltIn = true,
            Arguments =
                "--wf-tcp=80,443 --wf-udp=443,3478-3481,50000-65535 " +
                "--dpi-desync=fake,split2 --dpi-desync-repeats=3 " +
                $"--hostlist=\"{Lists("list-general.txt")}\""
        },
        new Preset
        {
            Id = "youtube-simple",
            Name = "YouTube (Simple Fake)",
            Description = "Простой обход замедления YouTube: fake TLS для доменов Google.",
            Category = CategoryYouTube,
            IsBuiltIn = true,
            Arguments =
                "--wf-tcp=80,443 --wf-udp=443 " +
                "--dpi-desync=fake --dpi-desync-fake-tls=\"{BIN}\\tls_clienthello_www_google_com.bin\" " +
                $"--hostlist=\"{Lists("list-google.txt")}\""
        },
        new Preset
        {
            Id = "discord-voice",
            Name = "Discord Voice",
            Description = "Только голос/медиа Discord: UDP 19294-19344, 50000-50100 и STUN.",
            Category = CategoryDiscord,
            IsBuiltIn = true,
            Arguments =
                "--wf-udp=443,19294-19344,50000-50100,{GAME_UDP} --wf-tcp=80,443 " +
                "--filter-udp=19294-19344,50000-50100 --filter-l7=discord,stun --dpi-desync=fake " +
                "--dpi-desync-fake-discord=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" " +
                "--dpi-desync-fake-stun=\"{BIN}\\ACTIVE_DISCORD_UDP.bin\" --dpi-desync-repeats=6 --new " +
                $"--filter-udp=443 --hostlist=\"{Lists("list-general.txt")}\" --hostlist=\"{Lists("list-general-user.txt")}\" " +
                "--dpi-desync=fake --dpi-desync-repeats=6 --dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\""
        },
        new Preset
        {
            Id = "youtube-quic",
            Name = "YouTube (QUIC + Fake TLS)",
            Description = "Отдельная стратегия для YouTube: обход QUIC (UDP 443) и TLS через fake.",
            Category = CategoryYouTube,
            IsBuiltIn = true,
            Arguments =
                "--wf-tcp=443 --wf-udp=443 " +
                $"--filter-udp=443 --hostlist=\"{Lists("list-google.txt")}\" --dpi-desync=fake --dpi-desync-repeats=6 " +
                "--dpi-desync-fake-quic=\"{BIN}\\quic_initial_www_google_com.bin\" --new " +
                $"--filter-tcp=443 --hostlist=\"{Lists("list-google.txt")}\" --ip-id=zero --dpi-desync=fake,multisplit " +
                "--dpi-desync-repeats=6 --dpi-desync-fooling=badseq --dpi-desync-badseq-increment=1000 " +
                "--dpi-desync-fake-tls=\"{BIN}\\tls_clienthello_www_google_com.bin\""
        }
    };

    private static string Lists(string fileName) => "{LISTS}\\" + fileName;
}
