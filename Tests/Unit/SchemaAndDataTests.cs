using System.Text.Json.Nodes;

namespace WC2.Tests;

public class JsonSchemaTests
{
    private static List<string> Check(string schema, string value) =>
        new JsonSchema((JsonObject)Support.Json(schema)).Validate(Support.Json(value));

    [Fact]
    public void TypesRequiredAndUnknownFields()
    {
        const string schema = """
        { "type": "object", "required": ["id"], "additionalProperties": false,
          "properties": { "id": { "type": "integer", "minimum": 0 }, "name": { "type": "string" } } }
        """;
        Assert.Empty(Check(schema, """{ "id": 3, "name": "tank" }"""));
        Assert.Contains(Check(schema, """{ "name": "tank" }"""), e => e.Contains("missing \"id\""));
        Assert.Contains(Check(schema, """{ "id": -1 }"""), e => e.Contains("below the minimum"));
        Assert.Contains(Check(schema, """{ "id": 1.5 }"""), e => e.Contains("expected integer"));
        Assert.Contains(Check(schema, """{ "id": 1, "nmae": "x" }"""), e => e.Contains("$.nmae: unknown field"));
    }

    [Fact]
    public void RegistriesRefsEnumsAndArrays()
    {
        const string schema = """
        { "$defs": { "unit": { "type": "object", "properties": { "kind": { "enum": ["land", "navy"] } } } },
          "type": "object",
          "properties": { "units": { "type": "object", "additionalProperties": { "$ref": "#/$defs/unit" } },
                          "list": { "type": "array", "items": { "type": "number" }, "minItems": 1 } } }
        """;
        Assert.Empty(Check(schema, """{ "units": { "a": { "kind": "land" } }, "list": [1, 2.5] }"""));
        Assert.Contains(Check(schema, """{ "units": { "a": { "kind": "air" } } }"""), e => e.Contains("$.units.a.kind"));
        Assert.Contains(Check(schema, """{ "list": [] }"""), e => e.Contains("at least 1"));
        Assert.Contains(Check(schema, """{ "list": ["x"] }"""), e => e.Contains("$.list[0]"));
    }
}

[Collection("assets")]
public class DataFileTests
{
    /// <summary>Every JSON data file of the repository passes the schema DataPaths assigns to it.</summary>
    [Fact]
    public void EveryDataFileMatchesItsSchema()
    {
        Support.MountRepositoryAssets();
        int checkedFiles = 0;
        var problems = new List<string>();
        foreach (var path in AssetSystem.List("data/").Where(p => p.EndsWith(".json")))
        {
            string schemaPath = DataPaths.SchemaFor(path);
            if (schemaPath == null) continue;
            var schema = JsonSchema.Load(schemaPath);
            Assert.True(schema != null, "schema missing: " + schemaPath);
            problems.AddRange(schema.Validate(JsonData.Parse(AssetSystem.ReadAllText(path))).Select(e => path + ": " + e));
            checkedFiles++;
        }
        Assert.True(checkedFiles >= 30, $"only {checkedFiles} data files have a schema");
        Assert.Empty(problems);
    }

    /// <summary>Every data file under data/json has a schema (except the schemas themselves).</summary>
    [Fact]
    public void EveryDataFileHasASchema()
    {
        Support.MountRepositoryAssets();
        var without = AssetSystem.List("data/")
            .Where(p => p.EndsWith(".json") && !p.StartsWith("data/json/schemas/") && DataPaths.SchemaFor(p) == null).ToList();
        Assert.Empty(without);
    }

    /// <summary>A mod that breaks a data file loses its changes to that file; the game data stays.</summary>
    [Fact]
    public void InvalidModDataFallsBackToTheGameData()
    {
        Support.MountRepositoryAssets();
        string mod = Support.TempTree(("data/json/rules/rules.json", """{ "economy": { "moneyCap": -1 } }"""));
        AssetSystem.Mount(new AssetLayer { Id = "mod:bad", IsMod = true, Source = new DirectoryAssetSource(mod) });
        JsonData.ValidationProblems.Clear();
        var rules = JsonData.LoadMerged(DataPaths.Rules);
        Assert.Equal(99999, rules.Obj("economy").Int("moneyCap", 0));
        Assert.Contains(JsonData.ValidationProblems, p => p.Contains("moneyCap") && p.Contains("ignored"));
    }

    [Fact]
    public void ValidModDataMerges()
    {
        Support.MountRepositoryAssets();
        string mod = Support.TempTree(("data/json/rules/rules.json", """{ "economy": { "moneyCap": 5000 } }"""));
        AssetSystem.Mount(new AssetLayer { Id = "mod:good", IsMod = true, Source = new DirectoryAssetSource(mod) });
        var rules = JsonData.LoadMerged(DataPaths.Rules);
        Assert.Equal(5000, rules.Obj("economy").Int("moneyCap", 0));
        Assert.Equal(99999, rules.Obj("economy").Int("industryCap", 0));
    }
}

[Collection("assets")]
public class AssetLayerTests
{
    [Fact]
    public void OriginalNamesResolveThroughTheIndex()
    {
        Support.MountRepositoryAssets();
        Assert.Equal("image/ui/hd/ui1.xml", AssetSystem.Resolve("ui1_hd.xml"));
        Assert.Equal("data/xml/scenarios/battle_axis1.xml", AssetSystem.Resolve("battle_axis1.xml"));
        AssetSystem.LanguageFolder = "zh_CN.lproj";
        Assert.Equal("data/strings/cn/Localizable.strings", AssetSystem.Resolve("Localizable.strings"));
        AssetSystem.LanguageFolder = "English.lproj";
        Assert.Equal("data/strings/en/Localizable.strings", AssetSystem.Resolve("Localizable.strings"));
        AssetSystem.LanguageFolder = "";
    }

    [Fact]
    public void ModsWinByPathAndByOriginalNameAtTheirRoot()
    {
        Support.MountRepositoryAssets();
        string mod = Support.TempTree(("image/ui/hd/ui1.xml", "<mod/>"), ("btn.wav", "x"), ("image/ui/hd/blank.png", "y"));
        AssetSystem.Mount(new AssetLayer { Id = "mod:m", IsMod = true, Source = new DirectoryAssetSource(mod) });
        Assert.Equal("mod:m", AssetSystem.GetLayer("ui1_hd.xml").Id);          // original name -> path -> the mod has that path
        Assert.Equal("mod:m", AssetSystem.GetLayer("btn.wav").Id);             // original name at the mod's root
        // a file inside a mod's tree does not take over a bare original name of another file
        Assert.Equal("base", AssetSystem.GetLayer("blank.png").Id);
    }

    [Fact]
    public void EveryOriginalNameIsInTheTree()
    {
        var names = PackBuilder.ReadLegacyNames(Support.Assets);
        Assert.Equal(1111, names.Count);
        var missing = names.Where(kv => !File.Exists(Path.Combine(Support.Assets, kv.Value))).Select(kv => kv.Key).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void VariantsAreFoldersNotSuffixes()
    {
        Assert.Equal(("hd", "cn"), PackBuilder.Describe("image/fonts/hd/cn/fontkt.png"));
        Assert.Equal(("tablet-hd", null), PackBuilder.Describe("image/ui/tablet-hd/mainbg.webp"));
        Assert.Equal((null, "en"), PackBuilder.Describe("data/json/text/en/stringtable_en.json"));
        // no file in the image tree still carries a variant suffix the folders replaced
        var suffixed = Directory.EnumerateFiles(Path.Combine(Support.Assets, "image"), "*", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n.EndsWith("_hd") || n.EndsWith("@2x") || n.EndsWith("_iPad") || n.EndsWith("-568h") || n.EndsWith("-640h"))
            .ToList();
        Assert.Empty(suffixed);
    }
}

public class LayoutProfileTests
{
    [Theory]
    [InlineData(2400, 1080, 640, 320)]   // 20:9 phone
    [InlineData(1136, 640, 568, 320)]    // 16:9
    [InlineData(1600, 1000, 534, 320)]   // 16:10
    [InlineData(1024, 768, 1024, 768)]   // 4:3 tablet
    [InlineData(1500, 1000, 480, 320)]   // 3:2
    public void OriginalBuckets(int w, int h, int lw, int lh)
    {
        LayoutProfiles.LoadBuiltIn();
        var p = LayoutProfiles.Select(w, h);
        Assert.Equal((lw, lh), (p.width, p.height));
    }
}
