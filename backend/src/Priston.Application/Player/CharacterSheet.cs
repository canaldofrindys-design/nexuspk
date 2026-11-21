namespace Priston.Application.Player;

/// <summary>Um item equipado no personagem: posição (1 a 21 no jogo), nome do lugar, nome do item, ItemCode e o código do arquivo ("WA193").</summary>
public sealed record CharacterEquipment(int Slot, string SlotName, string Name, int Code, string? FileId = null);

/// <summary>Resistências do personagem (as que o jogo mostra na janela de atributos).</summary>
public sealed record CharacterResistances(int Organic, int Fire, int Ice, int Lightning, int Poison);

/// <summary>
/// Ficha do personagem lida do arquivo dele no servidor do jogo (<c>DataServer\UserData\&lt;n&gt;\&lt;nick&gt;.dat</c>).
/// O servidor grava o arquivo quando o personagem salva ou sai do jogo: <paramref name="SavedUtc"/> é essa hora,
/// e mapa e experiência são os daquele momento. <paramref name="ExperiencePercent"/>: 0 a 100 no nível atual (null no nível máximo).
/// </summary>
public sealed record CharacterSheet(
    string Name,
    int Level,
    long Experience,
    double? ExperiencePercent,
    int Strength,
    int Spirit,
    int Talent,
    int Agility,
    int Health,
    int StatPoints,
    int AttackRating,
    int DamageMin,
    int DamageMax,
    int AttackSpeed,
    int Critical,
    int Defense,
    int Block,
    int Absorption,
    int MoveSpeed,
    int Mana,
    int Stamina,
    int Weight,
    int MaxWeight,
    long Gold,
    CharacterResistances Resistances,
    int? MapId,
    string? MapName,
    DateTimeOffset SavedUtc,
    IReadOnlyList<CharacterEquipment> Equipment,
    int ClassCode = 0);

/// <summary>Arquivo do personagem no servidor do jogo. null quando não existe; pasta fora do alcance: lança.</summary>
public interface ICharacterFileReader
{
    Task<CharacterSheet?> ReadAsync(string characterName, CancellationToken ct);

    /// <summary>
    /// Nome, classe e nível de todos os arquivos de personagem, pelo nome (sem diferenciar maiúsculas). Usado pelo ranking:
    /// a LevelTable pode estar atrás do arquivo (personagem novo, nível dado pela equipe). Pasta fora do alcance: lança.
    /// </summary>
    Task<IReadOnlyDictionary<string, CharacterFileLevel>> ReadAllLevelsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, CharacterFileLevel>>(new Dictionary<string, CharacterFileLevel>());
}

/// <summary>Classe (0xC4) e nível (0xC8) gravados no arquivo do personagem.</summary>
public sealed record CharacterFileLevel(string Name, int ClassCode, int Level);

/// <summary>Tabelas do jogo usadas na ficha: experiência por nível, nomes dos mapas e dos lugares do equipamento.</summary>
public static class CharacterGameTables
{
    /// <summary>
    /// Experiência total para chegar a cada nível (índice = nível), copiada da tabela do servidor (open_the_server.exe),
    /// a mesma do cliente. O último nível com próxima meta é <see cref="MaxLevel"/> - 1.
    /// </summary>
    public static readonly IReadOnlyList<long> LevelExperience =
    [
        0L, 0L, 1000L, 2500L, 5000L, 9500L,
        17100L, 29925L, 51471L, 87500L, 140001L, 212801L,
        306434L, 416750L, 537608L, 672010L, 833293L, 1024950L,
        1250439L, 1515533L, 1824702L, 2182343L, 2592624L, 3059296L,
        3591614L, 4195005L, 4874596L, 5635033L, 6480288L, 7413449L,
        8443919L, 9575404L, 10820207L, 12194373L, 13706475L, 15378665L,
        17224105L, 19273774L, 21548079L, 24069204L, 26861232L, 29950274L,
        33364605L, 37134805L, 41293904L, 45836233L, 50878219L, 56474823L,
        62687054L, 69582630L, 77236719L, 85809995L, 95334904L, 105917079L,
        117673875L, 130735675L, 145312702L, 161515069L, 179523999L, 199540925L,
        221789738L, 246630189L, 274252770L, 304969080L, 339125617L, 377107687L,
        419532302L, 466729685L, 519236775L, 577650912L, 642636640L, 715254581L,
        796078348L, 886035202L, 986157179L, 1097592941L, 1222169740L, 1360886005L,
        1515346567L, 1687338402L, 1878851311L, 2104313468L, 2356831084L, 2639650815L,
        2956408912L, 3311177982L, 3708519340L, 4153541661L, 4651966660L, 5210202659L,
        6252243191L, 7502691829L, 9003230195L, 10803876234L, 12964651481L, 15557581778L,
        18669098134L, 22402917760L, 26883501313L, 32260201575L, 38712241890L, 46454690268L,
        55745628322L, 66894753987L, 80273704784L, 96328445741L, 115594134890L, 138712961868L,
        166455554241L, 199746665090L, 239695998108L, 287635197729L, 345162237275L, 414194684730L,
        497033621676L, 596440346012L, 715728415214L, 858874098257L, 1030648917908L, 1236778701490L,
        1484134441788L, 1780961330146L, 2137153596175L, 2564584315410L, 3077501178492L, 3693001414190L,
        4431601697028L, 5317922036434L, 6381506443721L, 7657807732465L, 9189369278958L, 11027243134749L,
        13232691761699L, 15879230114039L, 19055076136847L, 22866091364216L, 27439309637060L, 32927171564472L,
        39512605877366L, 47415127052839L, 56898152463407L, 67708801431454L, 79896385689115L, 93478771256264L,
        108435374657266L, 124700680855855L, 142158776175674L, 160639417078511L, 179916147127932L, 199706923312004L,
        219677615643204L, 239448601051092L, 260998975145690L, 284488882908802L, 310092882370594L, 338001241783947L,
        368421353544502L, 401579275363507L, 437721410146222L, 477116337059381L, 520056807394725L, 566861920060250L,
        617879492865672L, 673488647223582L, 734102625473704L, 800171861766337L, 872187329325307L, 950684188964584L,
        1036245765971396L, 1129507884908821L, 1231163594550614L, 1341968318060169L, 1462745466685584L, 1594392558687286L,
        1737887888969141L, 1894297798976363L, 2064784600884235L, 2250615214963816L, 2453170584310559L, 2673955936898509L,
        2914611971219374L, 3176927048629117L, 3462850483005737L, 3774507026476253L, 4114212658859115L, 4484491798156435L,
        4888096059990514L, 5328024705389660L, 5807546928874729L, 6330226152473454L, 6899946506196064L, 7520941691753709L,
        8197826444011542L, 8935630823972580L, 9739837598130112L, 10616422981961822L, 11571901050338385L, 12613372144868839L,
        13748575637907034L, 14985947445318667L, 16334682715397347L, 17804804159783108L, 19407236534163587L, 21153887822238309L,
        23057737726239756L, 25132934121601334L, 27394898192545454L, 29860439029874544L, 32547878542563252L, 35477187611393944L,
        38670134496419398L, 42150446601097143L, 45943986795195885L, 50078945606763514L, 54586050711372230L, 59498795275395730L,
        64853686850181345L, 70690518666697666L, 77052665346700455L, 83987405227903495L, 91546271698414809L, 99785436151272141L,
        108766125404886633L, 118555076691326429L, 129225033593545807L, 140855286616964929L, 153532262412491772L,
    ];

    public static int MaxLevel => LevelExperience.Count - 1;

    /// <summary>Nome de cada mapa pelo número do campo, como o cliente do jogo mostra.</summary>
    public static readonly IReadOnlyList<string> MapNames =
    [
        "Mata das Acácias",
        "Floresta Bamboo",
        "Jardim da Liberdade",
        "Ricarten",
        "Refúgio dos Anciões",
        "Castelo dos Perdidos",
        "Vila Ruinen",
        "Terra Maldita",
        "Terra Esquecida",
        "Navisko",
        "Oásis",
        "Campo de Batalha dos Anciões",
        "Terra Proibida",
        "Calabouço Antigo 1º Andar",
        "Calabouço Antigo 2º Andar",
        "Calabouço Antigo 3º Andar",
        "Sala do GM",
        "Floresta dos Espíritos",
        "Floresta das Ilusões",
        "Vale Tranquilo",
        "Estrada dos Ventos",
        "Pillai",
        "Templo Maldito 1º Andar",
        "Templo Maldito 2º Andar",
        "Caverna dos Cogumelos",
        "Caverna das Abelhas",
        "Santuário Sombrio",
        "Estrada de Ferro do Caos",
        "Coração de Perum",
        "Eura",
        "SoD",
        "Vale Galubia",
        "Sala de Quest",
        "Castelo Antigo (BC)",
        "Lago da Ganância",
        "Santuário Congelado",
        "Caverna do Kelvezu",
        "Ilha Perdida",
        "Templo Perdido",
        "Mapa 2D",
        "Torre sem Fim 1º Andar",
        "Torre sem Fim 2º Andar",
        "Templo Maldito 3º Andar",
        "Torre sem Fim 3º Andar",
        "Mina de Gelo 1º Andar",
        "T.D.P Ardor",
        "Laboratório Secreto",
        "Arma Secreta",
        "Abismo do Mar 1º Andar",
        "Iron 1",
        "Ruína dos Mares",
        "RC 1.50",
    ];

    /// <summary>Porcentagem da experiência dentro do nível (0 a 100); null no nível máximo ou fora da tabela.</summary>
    public static double? ExperiencePercent(int level, long experience)
    {
        if (level < 1 || level >= MaxLevel)
        {
            return null;
        }

        var start = LevelExperience[level];
        var next = LevelExperience[level + 1];
        if (next <= start)
        {
            return null;
        }

        var pct = (double)(experience - start) / (next - start) * 100;
        return Math.Round(Math.Clamp(pct, 0, 100), 2);
    }

    public static string? MapName(int? id) => id is >= 0 && id < MapNames.Count ? MapNames[id.Value] : null;

    /// <summary>Nome do lugar do equipamento; null para os lugares que não entram na ficha (poções da barra).</summary>
    public static string? SlotName(int slot) => slot switch
    {
        1 => "Mão direita",
        2 => "Mão esquerda",
        3 => "Armadura",
        4 => "Amuleto",
        5 => "Anel",
        6 => "Anel",
        7 => "Sheltom",
        8 => "Bracelete",
        9 => "Luvas",
        10 => "Botas",
        14 => "Traje",
        15 => "Brinco",
        16 => "Brinco",
        17 => "Cinto",
        18 or 19 or 20 or 21 => "Relíquia",
        _ => null,
    };
}
