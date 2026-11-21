using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Priston.Application.Home;
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

    private sealed class StaticTypes : IItemNameReader
    {
        public Task<IReadOnlyList<ItemTypeInfo>> ReadTypesAsync(CancellationToken ct) => Task.FromResult(Types);
    }
}
