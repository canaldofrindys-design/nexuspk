using System.Buffers.Binary;
using System.Text;
using Priston.Application.Home;
using Priston.Application.Player;
using Priston.Infrastructure.GameServer;

namespace Priston.UnitTests;

public sealed class CharacterFileTests
{
    private static readonly DateTimeOffset Saved = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Arquivo de personagem montado no formato do servidor, com os itens comprimidos só com cópias literais.</summary>
    private static byte[] File(string name, int level, long experience, int map, params (int Slot, int Code, string Name)[] items)
    {
        var b = new byte[0x4000];
        Encoding.ASCII.GetBytes("GHRC 1.50").CopyTo(b, 6);
        Encoding.ASCII.GetBytes(name).CopyTo(b, 0x10);
        void I(int o, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(o), v);
        void S(int o, short v) => BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(o), v);
        I(0xC8, level);
        I(0xCC, 1670); I(0xD0, 64); I(0xD4, 90); I(0xD8, 60); I(0xDC, 50);
        I(0xE4, 3772); I(0xE8, 3105); I(0xEC, 3380); I(0xF0, 11); I(0xF8, 39); I(0xFC, 7405); I(0x100, 28); I(0x104, 403); I(0x108, 37);
        S(0x110, 999); S(0x112, 4574); S(0x114, 40); S(0x118, 56); S(0x11A, 75); S(0x11C, 40); S(0x11E, 15);
        S(0x138, 1250); S(0x13C, 2266);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0x14C), (uint)(experience & 0xFFFFFFFF));
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0x194), (uint)(experience >> 32));
        I(0x154, 405615226);
        I(0x160, 20);
        I(0x1E4, map);

        I(0x6E4, items.Length);
        I(0x6E8, items.Length);
        var o = 0x6F0;
        foreach (var (slot, code, itemName) in items)
        {
            var raw = new byte[16 + 0x60];
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(12), slot);
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(16 + 0x18), code);
            Encoding.Latin1.GetBytes(itemName).CopyTo(raw, 16 + 0x1C);
            var packed = new List<byte>();
            for (var i = 0; i < raw.Length; i += 100)
            {
                var n = Math.Min(100, raw.Length - i);
                packed.Add((byte)n);
                packed.AddRange(raw.AsSpan(i, n).ToArray());
            }

            I(o, packed.Count + 4);
            packed.CopyTo(b, o + 4);
            o += packed.Count + 4;
        }

        I(0x6EC, o - 0x6F0);
        return b;
    }

    [Fact]
    public void Reads_level_experience_stats_map_and_equipment()
    {
        var start = CharacterGameTables.LevelExperience[200];
        var next = CharacterGameTables.LevelExperience[201];
        var data = File("Quaglia", 200, start + ((next - start) / 4), 21,
            (1, 0x01071400, "Espada Carmesin"),
            (2, 0x02044600, "Escudo Carmesin"),
            (11, 0x04020400, "Poção de HP"),
            (0, 0x08010200, "Na mochila"),
            (21, 0x03170500, "Relics Noctar"));

        var sheet = CharacterFile.Parse(data, "quaglia", Saved)!;

        Assert.Equal("Quaglia", sheet.Name);
        Assert.Equal(200, sheet.Level);
        Assert.Equal(25.0, sheet.ExperiencePercent!.Value, 1);
        Assert.Equal(1670, sheet.Strength);
        Assert.Equal(50, sheet.Health);
        Assert.Equal(20, sheet.StatPoints);
        Assert.Equal(3105, sheet.DamageMin);
        Assert.Equal(3380, sheet.DamageMax);
        Assert.Equal(7405, sheet.Defense);
        Assert.Equal(1250, sheet.Mana);
        Assert.Equal(4574, sheet.MaxWeight);
        Assert.Equal(405615226, sheet.Gold);
        Assert.Equal(new CharacterResistances(40, 56, 75, 40, 15), sheet.Resistances);
        Assert.Equal("Pillai", sheet.MapName);
        Assert.Equal(Saved, sheet.SavedUtc);
        Assert.Equal(["Espada Carmesin", "Escudo Carmesin", "Relics Noctar"], sheet.Equipment.Select(e => e.Name));
        Assert.Equal("Relíquia", sheet.Equipment[^1].SlotName);
    }

    [Fact]
    public void Wrong_name_or_short_file_gives_nothing()
    {
        Assert.Null(CharacterFile.Parse(File("Outro", 10, 0, 3), "Quaglia", Saved));
        Assert.Null(CharacterFile.Parse(new byte[100], "Quaglia", Saved));
    }

    [Theory]
    [InlineData(1, 0L, 0.0)]
    [InlineData(2, 1750L, 50.0)]
    public void Experience_percent_uses_the_game_table(int level, long exp, double pct) =>
        Assert.Equal(pct, CharacterGameTables.ExperiencePercent(level, exp));

    [Fact]
    public void Max_level_has_no_percent_and_maps_are_named()
    {
        Assert.Null(CharacterGameTables.ExperiencePercent(CharacterGameTables.MaxLevel, long.MaxValue));
        Assert.Equal("Ricarten", CharacterGameTables.MapName(3));
        Assert.Null(CharacterGameTables.MapName(999));
    }

    [Fact]
    public void Equipment_gets_the_item_file_code()
    {
        var names = ItemNames.Build([new ItemTypeInfo(0x0107, "Espada", "WS1")], new Dictionary<string, string>());
        var sheet = CharacterFile.Parse(File("A", 10, 0, 3, (1, 0x01071400, "Espada")), "A", Saved)!;

        var withIds = PlayerPanelService.WithFileIds(sheet, names);

        Assert.Equal("WS120", withIds.Equipment[0].FileId);
    }

    [Fact]
    public void Without_the_type_prefix_the_photo_comes_from_the_item_name()
    {
        // Sem os tipos do banco não há prefixo; o nome gravado no personagem é o do arquivo do item.
        var names = ItemNames.Build([], new Dictionary<string, string>
        {
            ["DA111"] = "Armadura Carmesin +",
            ["OR119"] = "Anel do Valento",
            ["OR219"] = "Anel do Valento",
            ["DA211"] = "Escudo Carmesin +",
            ["DS161"] = "Escudo Carmesin +",
        });
        var sheet = CharacterFile.Parse(File("A", 10, 0, 3,
            (3, 0x02010b00, "Armadura Carmesin +"), (5, 0x03041900, "Anel do Valento"), (2, 0x02043d00, "Escudo Carmesin +"), (9, 0x02031300, "Luvas")), "A", Saved)!;

        var ids = PlayerPanelService.WithFileIds(sheet, names).Equipment.ToDictionary(e => e.Slot, e => e.FileId);

        Assert.Equal("DA111", ids[3]);
        Assert.Null(ids[5]);
        Assert.Equal("DS161", ids[2]);
        Assert.Null(ids[9]);
    }
}
