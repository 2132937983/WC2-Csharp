namespace WC2.Tests;

/// <summary>
/// Mods built into the game package (assets/build.json builtInMods, packaged under mods/&lt;id&gt;/): the
/// world map ships this way, also inside the APK, where they are read through the APK's asset source.
/// </summary>
[Collection("assets")]
public class BuiltInModTests
{
    private static string Package() => Support.TempTree(
        ("data/json/rules/rules.json", """{ "format": 1, "economy": { "moneyCap": 99999, "industryCap": 99999 } }"""),
        ("mods/extra/mod.json", """{ "id": "extra", "name": "Built-in extra" }"""),
        ("mods/extra/data/json/rules/rules.json", """{ "economy": { "moneyCap": 7000 } }"""),
        ("mods/extra/newfile.txt", "from the built-in mod"),
        ("mods/off/mod.json", """{ "id": "off", "enabled": false }"""),
        ("mods/off/data/json/rules/rules.json", """{ "economy": { "industryCap": 1 } }"""));

    [Fact]
    public void BuiltInModsMountFromThePackage()
    {
        var base_ = new DirectoryAssetSource(Package());
        AssetSystem.Reset();
        AssetSystem.Mount(new AssetLayer { Id = "base", Source = base_ });
        JsonData.SchemaResolver = null;
        ModManager.MountMods(Array.Empty<string>(), base_);

        Assert.Contains(ModManager.Loaded, m => m.id == "extra" && m.builtIn);
        Assert.DoesNotContain(ModManager.Loaded, m => m.id == "off");       // disabled in its mod.json
        var rules = JsonData.LoadMerged("data/json/rules/rules.json");
        Assert.Equal(7000, rules.Obj("economy").Int("moneyCap", 0));          // merged like a folder mod
        Assert.Equal(99999, rules.Obj("economy").Int("industryCap", 0));
        Assert.Equal("mod:extra", AssetSystem.GetLayer("newfile.txt").Id);    // its root files by name
        Assert.Equal("from the built-in mod", AssetSystem.ReadAllText("newfile.txt"));
    }

    [Fact]
    public void AFolderModReplacesTheBuiltInOneWithTheSameId()
    {
        var base_ = new DirectoryAssetSource(Package());
        string folder = Support.TempTree(
            ("extra/mod.json", """{ "id": "extra", "name": "Folder copy" }"""),
            ("extra/data/json/rules/rules.json", """{ "economy": { "moneyCap": 1234 } }"""));
        AssetSystem.Reset();
        AssetSystem.Mount(new AssetLayer { Id = "base", Source = base_ });
        JsonData.SchemaResolver = null;
        ModManager.MountMods(new[] { folder }, base_);

        var extra = Assert.Single(ModManager.Loaded, m => m.id == "extra");
        Assert.False(extra.builtIn);
        Assert.Equal(1234, JsonData.LoadMerged("data/json/rules/rules.json").Obj("economy").Int("moneyCap", 0));
    }

    [Fact]
    public void TheGamePackageBuildsTheWorldMapIn()
    {
        var settings = BuildSettings.Read(Support.Assets);
        Assert.Contains(settings.BuiltInMods, m => m.EndsWith("map-natural-earth"));
        Assert.True(File.Exists(Path.Combine(Support.Repo, "mods", "map-natural-earth", "mod.json")));
        Assert.True(File.Exists(Path.Combine(Support.Repo, "mods", "map-natural-earth", "conquest_10.xml")));
    }
}
