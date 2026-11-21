namespace Priston.Application.Characters;

/// <summary>
/// Uma patente PvP do jogo. <paramref name="Level"/>: 1 a 7 pelos pontos PvP, 8 é o Rei PvP.
/// <paramref name="MinPoints"/>/<paramref name="MaxPoints"/>: faixa de <c>PontosPvP</c> (sem teto na última; nulos no Rei).
/// <paramref name="Bonus"/>: o buff que o jogo dá. <paramref name="Emblem"/>: nome do emblema do cliente (game\patente\emblems).
/// </summary>
public sealed record PvpPatent(int Level, string Key, string Name, int? MinPoints, int? MaxPoints, string Bonus, string Emblem);

/// <summary>
/// A regra de patente do próprio jogo, tirada do game.exe do cliente: o servidor manda os pontos PvP
/// (<c>UserDB.dbo.BattlePoints.PontosPvP</c>) e o cliente escolhe a patente assim: menos de 100 pontos, nenhuma;
/// de 100 a 699, pontos / 100 (1 a 6); 700 ou mais, 7. Quem tem <c>IsReiPvP = 1</c> é o Rei PvP (8), seja qual for o ponto.
/// Nomes e bônus são os dos buffs "Buff Patente I" a "VII"; os emblemas seguem a mesma ordem no cliente.
/// </summary>
public static class PvpPatents
{
    public const int KingLevel = 8;

    public static readonly IReadOnlyList<PvpPatent> All =
    [
        new(1, "prata-de-elite", "Prata de Elite", 100, 199, "+25 de HP", "primal"),
        new(2, "ouro-mestre", "Ouro Mestre", 200, 299, "+50 de HP", "ceifador"),
        new(3, "guardiao-de-elite", "Guardião de Elite", 300, 399, "+75 de HP", "guerreiro"),
        new(4, "mestre-guardiao", "Mestre Guardião", 400, 499, "+1% de ATQ, +100 de HP", "guardiao"),
        new(5, "guerreiro-lendario", "Guerreiro Lendário", 500, 599, "+1% de ATQ, +125 de HP", "mestre"),
        new(6, "guerreiro-de-elite", "Guerreiro de Elite", 600, 699, "+1% de ATQ, +150 de HP", "desafiante"),
        new(7, "mestre-supremo", "Mestre Supremo", 700, null, "+2% de ATQ, +200 de HP", "mvp"),
        new(KingLevel, "rei-pvp", "Rei PvP", null, null, "Título de Rei PvP", "rei-pvp"),
    ];

    /// <summary>Nível da patente (0 = sem patente), igual ao cliente.</summary>
    public static int LevelOf(int points, bool isKing)
    {
        if (isKing)
        {
            return KingLevel;
        }

        if (points < 100)
        {
            return 0;
        }

        return points >= 700 ? 7 : points / 100;
    }

    /// <summary>A patente do personagem, ou null sem patente.</summary>
    public static PvpPatent? Of(int points, bool isKing)
    {
        var level = LevelOf(points, isKing);
        return level == 0 ? null : All[level - 1];
    }
}
