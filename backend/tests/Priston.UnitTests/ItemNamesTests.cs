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

        var item = Assert.Single(showcase);
        Assert.Equal(new ItemShowcase("CA182", "Traje Caos F", "Valento"), item);
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
