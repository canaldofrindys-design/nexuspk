using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Home;
using Priston.Application.Site;
using Priston.Infrastructure.GameServer;

namespace Priston.UnitTests;

public class ItemNamesTests
{
    // Parte de ItemDB.dbo.ItemType do servidor (nome, hex, sigla).
    private static readonly IReadOnlyList<ItemTypeInfo> Types =
    [
        new(0x0101, "Axe", "WA"),
        new(0x0106, "Bow", "WS1"),
        new(0x0107, "Sword", "WS2"),
        new(0x0201, "Armor", "DA1"),
        new(0x0205, "Robe", "DA2"),
        new(0x0206, "Costume3", "CA"),
        new(0x0207, "Costume4", "CA"),
        new(0x0210, "Costume", "CA"),
        new(0x0211, "Costume2", "CA"),
        new(0x0235, "Sheltom", "OS"),
        new(0x0304, "Ring", "OR1"),
        new(0x0305, "Ring2", "OR1"),
        new(0x0803, "Wing", "QW"),
        new(0x0801, "Cookie", null),
    ];

    [Fact]
    public void File_prefixes_come_from_the_type_abbreviation()
    {
        var p = ItemNames.Prefixes(Types);

        Assert.Equal(0x0101, p["WA1"]);
        Assert.Equal(0x0106, p["WS1"]);
        Assert.Equal(0x0107, p["WS2"]);
        Assert.Equal(0x0205, p["DA2"]);
        Assert.Equal(0x0210, p["CA1"]);
        Assert.Equal(0x0211, p["CA2"]);
        Assert.Equal(0x0206, p["CA3"]);
        Assert.Equal(0x0207, p["CA4"]);
        Assert.Equal(0x0304, p["OR1"]);
        Assert.Equal(0x0305, p["OR2"]);
        Assert.Equal(0x0803, p["QW1"]);
    }

    [Fact]
    public void Ambiguous_prefixes_get_no_exact_name()
    {
        var p = ItemNames.Prefixes([new(0x0304, "Ring", "OR1"), new(0x0305, "RingB", "OR1")]);
        Assert.False(p.ContainsKey("OR1"));
    }

    [Theory]
    [InlineData("CA182", "CA1", 82)]
    [InlineData("wa161", "WA1", 61)]
    [InlineData("OS110", "OS1", 10)]
    [InlineData("WA1100", "WA1", 100)]
    public void File_ids_split_into_prefix_and_number(string id, string prefix, int number) =>
        Assert.Equal((prefix, number), ItemNames.ParseFileId(id));

    [Theory]
    [InlineData("CA1")]
    [InlineData("CA100")]
    [InlineData("CA1999")]
    [InlineData("Traje")]
    [InlineData("")]
    public void Other_texts_are_not_file_ids(string id) => Assert.Null(ItemNames.ParseFileId(id));

    [Fact]
    public void Exact_names_win_and_the_type_name_is_the_fallback()
    {
        var names = ItemNames.Build(Types, new Dictionary<string, string> { ["CA182"] = "Traje Caos F", ["WA161"] = "Machado Dourado", ["ZZ101"] = "Nada" });

        Assert.Equal(2, names.ExactCount);
        Assert.Equal("Traje Caos F", names.Name(0x02105200));
        Assert.Equal("Machado Dourado", names.Name(0x01013D00));
        Assert.Equal("Axe nº 62", names.Name(0x01013E00));
        Assert.Equal("Item especial #0C-4B", names.Name(0x080C4B00));
    }

    [Fact]
    public void Item_file_gives_code_and_name()
    {
        var text = "*ÀÌ¸§ \"Traje Caos F\"\r\n*Name \"Traje Caos F\"\r\n*ÄÚµå \"CA182\"\r\n*Level 80\r\n";
        Assert.Equal(("CA182", "Traje Caos F"), ItemFileCatalog.Parse("CA182", text));
        Assert.Equal(("CA182", "Traje Caos F"), ItemFileCatalog.Parse("copia", text));
        Assert.Null(ItemFileCatalog.Parse("CA182", "*Level 80"));
    }

    [Fact]
    public async Task Item_folder_is_read_with_portuguese_accents()
    {
        var dir = Directory.CreateTempSubdirectory("openitem");
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "WA161.txt"), Encoding.GetEncoding(1252).GetBytes("*Name \"Machado de Fênix\"\r\n*Code \"WA161\"\r\n"));
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "lixo.txt"), "sem nome");
            var catalog = new ItemFileCatalog(Options.Create(new GameServerOptions { ItemsFolder = dir.FullName }));

            var names = await catalog.ReadNamesAsync(CancellationToken.None);

            Assert.Equal("Machado de Fênix", Assert.Single(names).Value);
            Assert.Equal("WA161", names.Keys.Single());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Missing_item_folder_falls_back_to_type_names()
    {
        var catalog = new ItemFileCatalog(Options.Create(new GameServerOptions { ItemsFolder = Path.Combine(Path.GetTempPath(), "nao-existe-" + Guid.NewGuid()) }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var source = new ItemNameSource(cache, new StaticTypes(), catalog);

        var names = await source.GetAsync(CancellationToken.None);

        Assert.Equal(0, names.ExactCount);
        Assert.Equal("Axe nº 61", names.Name(0x01013D00));
    }

    // Trecho real de um .inf (cabeçalhos coreanos lidos como Windows-1252), com uma linha de itens acrescentada.
    private const string MonsterFile =
        "*Name \"Valento\"\r\n" +
        "//\u00B8\u00F3\u00BD\u00BA\u00C5\u00CD\u00C0\u00C7 HP\r\n" +
        "*\u00BB\u00FD\u00B8\u00ED\u00B7\u00C2\t\t\t10000\r\n" +
        "*\u00BE\u00C6\u00C0\u00CC\u00C5\u00DB\u00C4\u00AB\u00BF\u00EE\u00C5\u00CD\t\t1\r\n" +
        "*\u00BE\u00C6\u00C0\u00CC\u00C5\u00DB\t\t50\t\u00B5\u00B7\t2000\t5000\r\n" +
        "*\u00BE\u00C6\u00C0\u00CC\u00C5\u00DB\t\t2\twa161\tCA182 // raros\r\n" +
        "//*\u00BE\u00C6\u00C0\u00CC\u00C5\u00DB\t\t2\tOS110\r\n";

    [Fact]
    public void Monster_file_gives_name_and_item_drops_but_not_gold()
    {
        var m = MonsterFileCatalog.Parse("Boss_100_Valento", MonsterFile);
        Assert.NotNull(m);
        Assert.Equal("Valento", m.Value.Name);
        Assert.Equal(["WA161", "CA182"], m.Value.Drops);

        var noName = MonsterFileCatalog.Parse("Slab_119_Elite Zumbi", MonsterFile.Replace("*Name \"Valento\"", string.Empty));
        Assert.Equal("Elite Zumbi", noName!.Value.Name);

        var onlyGold = "*Name \"Lobo\"\r\n*\u00BE\u00C6\u00C0\u00CC\u00C5\u00DB\t\t50\t\u00B5\u00B7\t2000\t5000\r\n";
        Assert.Null(MonsterFileCatalog.Parse("Lobo", onlyGold));
    }

    [Fact]
    public void Monster_details_keep_chance_items_gold_and_hp()
    {
        var m = MonsterFileCatalog.ParseDetails("Boss_100_Valento.inf", MonsterFile);

        Assert.NotNull(m);
        Assert.Equal("Valento", m.Name);
        Assert.Equal(10000, m.Hp);
        Assert.Equal(2, m.Drops.Count);
        Assert.Equal("50", m.Drops[0].Chance);
        Assert.Equal("2000–5000", m.Drops[0].Gold);
        Assert.Empty(m.Drops[0].Items);
        Assert.Equal("2", m.Drops[1].Chance);
        Assert.Equal(["WA161", "CA182"], m.Drops[1].Items);
        Assert.Null(m.Drops[1].Gold);
    }

    [Fact]
    public void Boss_is_shown_only_when_few_monsters_drop_the_item()
    {
        var names = ItemNames.Build(Types, new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<string>>
        {
            ["WA161"] = ["Valento"],
            ["CA182"] = ["Fúria", "Babel"],
            ["OS110"] = ["Lobo", "Urso", "Zumbi"],
        });

        Assert.Equal("Valento", names.Boss(0x01013D00));
        Assert.Equal("Babel ou Fúria", names.Boss(0x02105200));
        Assert.Null(names.Boss(0x02350A00));
        Assert.Null(names.Boss(0x01013E00));
    }

    [Fact]
    public async Task Monster_folder_maps_items_to_monsters()
    {
        var dir = Directory.CreateTempSubdirectory("monster");
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var latin = Encoding.GetEncoding(1252);
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "Boss_100_Valento.inf"), latin.GetBytes(MonsterFile));
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "Boss_101_Fenix.inf"), latin.GetBytes(MonsterFile.Replace("Valento", "Fênix")));
            var catalog = new MonsterFileCatalog(Options.Create(new GameServerOptions { MonstersFolder = dir.FullName }));

            var sources = await catalog.ReadDropSourcesAsync(CancellationToken.None);

            Assert.Equal(["Fênix", "Valento"], sources["WA161"].Order());
            Assert.False(sources.ContainsKey("OS110"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    // Trechos reais de arquivos .npc (CP949, cabeçalhos coreanos e nomes em português).
    private const string ShopNpc =
        "\r\n*속성\t\tNPC\r\n*모양파일\t\"char\\npc\\TN-005\\TN-005.ini\"\r\n" +
        "*이름\t\t\"Loja da Sabrine\"\r\n*Name\t\t\"Loja da Sabrine\"\r\n" +
        "//*대화\t\t\"빨간약은 생명력을\"\r\n" +
        "*무기판매\t없음\r\n\r\n*방어구판매\t없음\r\n\r\n" +
        "*잡화판매\tEC101 EC102 ec104 BI108 PL104\t\t\r\n" +
        "*잡화판매\tOS103\r\n" +
        "//*잡화판매\tOS110\r\n";

    private const string KoreanNameNpc =
        "*이름\t\t\t\"대장장이 엄프\"\r\n*무기판매\tWA161\r\n*방어구판매\tDA110 da110\r\n*잡화판매\t없음\r\n";

    private static string AsGameFile(string text)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252).GetString(Encoding.GetEncoding(949).GetBytes(text));
    }

    // Trecho real de OpenItem\CA182.txt (cabeçalhos coreanos), com requisito e bônus de classe acrescentados.
    private const string ItemFileText =
        "//\t\t아이템 정보\t\t//\r\n*이름\t\"Traje Caos F\"\r\n*Name\t\"Traje Caos F\"\r\n*코드\t\"CA182\"\r\n" +
        "*내구력\t\t\r\n*무게\t1\r\n*가격\t\r\n*생체\t10 10\r\n*불\t10 10\r\n*공격력\t\t\r\n*명중력\t\t100\r\n" +
        "*크리티컬\t1\r\n*흡수력\t\t2\r\n*방어력\t\t200\r\n*블럭율\t\t0\r\n*레벨\t150\r\n**흡수력\t1.5 // classe\r\n";

    [Fact]
    public void Item_file_gives_stats_requirements_and_class_bonus()
    {
        var d = ItemFileCatalog.ParseDetail("CA182", AsGameFile(ItemFileText));

        Assert.NotNull(d);
        Assert.Equal("CA182", d.Id);
        Assert.Equal("Traje Caos F", d.Name);
        Assert.Equal(
            ["Peso: 1", "Orgânico: 10", "Fogo: 10", "Taxa de ataque: 100", "Crítico: 1", "Absorção: 2", "Defesa: 200"],
            d.Stats.Select(s => $"{s.Label}: {s.Value}"));
        Assert.Equal(["Nível: 150"], d.Requirements.Select(s => $"{s.Label}: {s.Value}"));
        Assert.Equal(["Absorção: 1.5"], d.Spec.Select(s => $"{s.Label}: {s.Value}"));
    }

    [Fact]
    public void Item_guide_lists_monsters_and_shops_of_the_item()
    {
        var monsters = new List<MonsterInfo>
        {
            new("Valento", "Boss_100_Valento.inf", null, [new("2", ["WA161", "CA182"], null), new("50", [], "2000–5000")]),
            new("Babel", "Babel.inf", null, [new("5", ["CA182"], null)]),
            new("Lobo", "Lobo.inf", null, [new("10", ["OS110"], null)]),
        };
        var shops = new List<NpcShop>
        {
            new("Loja da Sabrine", "LojadePotes.NPC", [], [], ["EC101"]),
            new("Ferreiro", "Ferreiro.npc", ["WA161"], ["CA182"], []),
            new("Ferreiro", "Ferreiro.npc.npc", ["WA161"], ["CA182"], []),
        };

        Assert.Equal([new ItemDropSource("Babel", "5", 1), new ItemDropSource("Valento", "2", 2)], ItemGuide.DropsOf("CA182", monsters));
        Assert.Equal([new ItemShopSource("Ferreiro", "Armaduras")], ItemGuide.ShopsOf("ca182", shops));
        Assert.Empty(ItemGuide.DropsOf("BI108", monsters));
    }

    [Fact]
    public void Npc_file_gives_the_shop_items_by_kind()
    {
        var shop = NpcFileCatalog.Parse("LojadePotes.NPC", AsGameFile(ShopNpc));

        Assert.NotNull(shop);
        Assert.Equal("Loja da Sabrine", shop.Name);
        Assert.Equal("LojadePotes.NPC", shop.File);
        Assert.Empty(shop.Weapons);
        Assert.Empty(shop.Armors);
        Assert.Equal(["EC101", "EC102", "EC104", "BI108", "PL104", "OS103"], shop.Misc);
    }

    [Fact]
    public void Npc_with_a_korean_name_uses_the_file_name()
    {
        var shop = NpcFileCatalog.Parse("navisko-equip.npc.npc", AsGameFile(KoreanNameNpc));

        Assert.NotNull(shop);
        Assert.Equal("navisko-equip", shop.Name);
        Assert.Equal(["WA161"], shop.Weapons);
        Assert.Equal(["DA110"], shop.Armors);
        Assert.Empty(shop.Misc);

        var portuguese = NpcFileCatalog.Parse("Migracao.NPC", AsGameFile(KoreanNameNpc).Replace("\"" + AsGameFile("대장장이 엄프") + "\"", "\"Ação\""));
        Assert.Equal("Ação", portuguese!.Name);
    }

    [Fact]
    public void Npc_without_a_shop_is_left_out()
    {
        Assert.Null(NpcFileCatalog.Parse("Bancaria.NPC", AsGameFile("*Name\t\"Banco\"\r\n*무기판매\t\r\n*잡화판매\t없음\r\n")));
    }

    [Fact]
    public async Task Npc_folder_lists_only_npcs_that_sell()
    {
        var dir = Directory.CreateTempSubdirectory("npc");
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var korean = Encoding.GetEncoding(949);
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "LojadePotes.NPC"), korean.GetBytes(ShopNpc));
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "navisko-equip.npc"), korean.GetBytes(KoreanNameNpc));
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "Bancaria.NPC"), korean.GetBytes("*Name \"Banco\"\r\n"));
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "Agiota.zhoon"), korean.GetBytes("*잡화판매\tEC101\r\n"));
            var catalog = new NpcFileCatalog(Options.Create(new GameServerOptions { NpcFolder = dir.FullName }));

            var shops = await catalog.ReadShopsAsync(CancellationToken.None);

            Assert.Equal(["Loja da Sabrine", "navisko-equip"], shops.Select(s => s.Name));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("itWA161.bmp", "WA161")]
    [InlineData("ITca182.BMP", "CA182")]
    [InlineData("itBI354.png", "BI354")]
    [InlineData("WA161.bmp", null)]
    [InlineData("itWA161.txt", null)]
    [InlineData("item.bmp", null)]
    public void Item_image_names_give_the_item_code(string file, string? id) =>
        Assert.Equal(id, ItemImageFolder.IdFromFileName(file));

    [Fact]
    public async Task Item_images_are_found_in_any_subfolder_and_checked_by_content()
    {
        var dir = Directory.CreateTempSubdirectory("itemimages");
        try
        {
            var weapons = Directory.CreateDirectory(Path.Combine(dir.FullName, "Items", "Weapon"));
            await File.WriteAllBytesAsync(Path.Combine(weapons.FullName, "itWA161.bmp"), [(byte)'B', (byte)'M', 0, 0, 0, 0]);
            await File.WriteAllBytesAsync(Path.Combine(weapons.FullName, "itWA162.bmp"), "MZ não é imagem"u8.ToArray());
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var store = new ItemImageFolder(Options.Create(new GameServerOptions { ItemImagesFolder = dir.FullName }), cache);

            Assert.Equal(["WA161", "WA162"], (await store.ListIdsAsync(CancellationToken.None)).Order());
            var image = await store.GetAsync("wa161", CancellationToken.None);
            Assert.Equal("image/bmp", image!.ContentType);
            Assert.Null(await store.GetAsync("WA162", CancellationToken.None));
            Assert.Null(await store.GetAsync("../WA161", CancellationToken.None));
            Assert.Null(await store.GetAsync("CA182", CancellationToken.None));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void File_names_are_kept_by_code_for_the_picker()
    {
        var names = ItemNames.Build(Types, new Dictionary<string, string> { ["ca182"] = "Traje Caos F", ["lixo"] = "x" });
        Assert.Equal("Traje Caos F", names.FileNames["CA182"]);
        Assert.Single(names.FileNames);
    }

    [Fact]
    public void Item_code_gives_back_the_file_code()
    {
        var names = ItemNames.Build(Types, new Dictionary<string, string>());
        Assert.Equal("CA182", names.FileId(0x02105200));
        Assert.Equal("WA161", names.FileId(0x01013D00));
        Assert.Equal("OR205", names.FileId(0x03050500));
        Assert.Equal("WA1100", names.FileId(0x01016400));
        Assert.Null(names.FileId(0x080C4B00));
    }

    [Fact]
    public async Task Showcase_shows_real_boss_drops_with_image()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var source = new ItemNameSource(cache, new StaticTypes(), new Files(), new Drops());
        var feed = new RareDropFeed(cache, TimeProvider.System, null, source, new Images());

        var showcase = await feed.GetShowcaseAsync(11, CancellationToken.None);

        // Boss primeiro; depois o item que cai de qualquer monstro. Sem imagem (WA161) fica fora.
        Assert.Equal([new ItemShowcase("CA182", "Traje Caos F", "Valento"), new ItemShowcase("OS110", "Sheltom", null)], showcase);
    }

    [Fact]
    public async Task Showcase_works_without_the_game_database()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var source = new ItemNameSource(cache, null, new Files(), new Drops());
        var feed = new RareDropFeed(cache, TimeProvider.System, null, source, new Images());

        var showcase = await feed.GetShowcaseAsync(1, CancellationToken.None);

        Assert.Equal([new ItemShowcase("CA182", "Traje Caos F", "Valento")], showcase);
    }

    [Fact]
    public void Showcase_fills_with_other_server_items_but_never_potions()
    {
        var names = ItemNames.Build([], new Dictionary<string, string> { ["PL104"] = "Poção", ["DA110"] = "Armadura", ["CA182"] = "Traje" },
            new Dictionary<string, IReadOnlyList<string>> { ["CA182"] = ["Valento", "Babel"] });

        var picked = RareDropFeed.PickShowcase(names, new HashSet<string>(["PL104", "DA110", "CA182"]), 202647);

        Assert.Equal([new ItemShowcase("CA182", "Traje", "Babel ou Valento"), new ItemShowcase("DA110", "Armadura", null)], picked);
    }

    private sealed class Files : IItemFileCatalog
    {
        public Task<IReadOnlyDictionary<string, string>> ReadNamesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string> { ["CA182"] = "Traje Caos F", ["WA161"] = "Machado", ["OS110"] = "Sheltom" });
    }

    private sealed class Drops : IMonsterDropCatalog
    {
        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadDropSourcesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(new Dictionary<string, IReadOnlyList<string>>
            {
                ["CA182"] = ["Valento"],                  // boss, com imagem: entra
                ["WA161"] = ["Valento"],                  // boss, sem imagem: fica fora
                ["OS110"] = ["Lobo", "Urso", "Zumbi"],    // cai de qualquer lugar: fica fora
            });

        public Task<IReadOnlyList<MonsterInfo>> ReadMonstersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<MonsterInfo>>([]);
    }

    private sealed class Images : IItemImageStore
    {
        public Task<IReadOnlySet<string>> ListIdsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(["CA182", "OS110"], StringComparer.OrdinalIgnoreCase));

        public Task<ItemImage?> GetAsync(string fileId, CancellationToken ct) => Task.FromResult<ItemImage?>(null);
    }

    private sealed class StaticTypes : IItemNameReader
    {
        public Task<IReadOnlyList<ItemTypeInfo>> ReadTypesAsync(CancellationToken ct) => Task.FromResult(Types);
    }
}

public sealed class ItemTypeKeyTests
{
    [Theory]
    [InlineData(new byte[] { 0x01, 0x01, 0x00, 0x00 }, 0x0101)]
    [InlineData(new byte[] { 0x02, 0x01, 0x00, 0x00 }, 0x0201)]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 }, null)]
    public void Reads_the_type_from_varbinary(byte[] hex, int? expected) =>
        Assert.Equal(expected, Priston.GameDatabaseIntegration.Characters.ItemTypeNameReader.Key(hex));

    [Theory]
    [InlineData(16842752, 0x0101)]
    [InlineData(33619968, 0x0201)]
    [InlineData(-1, null)]
    [InlineData(0, null)]
    public void Reads_the_type_from_the_id(int id, int? expected) =>
        Assert.Equal(expected, Priston.GameDatabaseIntegration.Characters.ItemTypeNameReader.Key(id));
}
