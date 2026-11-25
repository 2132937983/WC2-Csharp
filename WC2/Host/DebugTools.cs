namespace WC2;

/// <summary>Command line helpers for validating data loaders without starting the game window.</summary>
public static class DebugTools
{
    /// <summary>Returns true when a debug command handled the arguments (the game should not start).</summary>
    public static bool TryRun(string[] args)
    {
        if (args.Length == 0) return false;
        switch (args[0])
        {
            case "--decode-pkm" when args.Length >= 3:
                DecodePkm(args[1], args[2]);
                return true;
            case "--test-xml":
                TestXml(args.Skip(1).ToArray());
                return true;
            case "--test-fonts":
                TestFonts();
                return true;
            case "--test-audio":
                TestAudio(args.Length > 1 ? args[1] : null);
                return true;
            case "--test-input":
                InputSelfTest.Run();
                return true;
            default:
                return false;
        }
    }

    private static void DecodePkm(string input, string output)
    {
        var data = File.ReadAllBytes(input);
        var rgba = Etc1Decoder.DecodePkm(data, out int w, out int h);
        if (rgba == null) { Console.WriteLine("not a PKM file"); return; }
        using var bmp = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, bmp.GetPixels(), rgba.Length);
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var png = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        using var fs = File.Create(output);
        png.SaveTo(fs);
        Console.WriteLine($"decoded {w}x{h} -> {output}");
    }

    private static void TestXml(string[] files)
    {
        ecGame.ConfigurePaths(null, null);
        if (files.Length == 0)
            files = new[] { "carddef.xml", "mui_hd.xml", "stringtable_cn.xml", "conquest_1.xml", "armydef.xml", "battlelist.xml", "commanderdef.xml", "areatax1.xml", "Localizable.strings", "effect_exp.xml", "resolutionColumns.xml", "unitposdef.xml" };
        foreach (var f in files)
        {
            var doc = new TiXmlDocument(GetPath(f));
            bool ok = doc.LoadFile();
            var root = doc.RootElement();
            int count = 0;
            if (root != null) foreach (var _ in root.ChildElements()) count++;
            Console.WriteLine($"{f,-28} load={ok} root=<{root?.Value()}> children={count} {(ok ? "" : doc.ErrorDesc())}");
        }
        // targeted checks
        var cards = new TiXmlDocument(GetPath("carddef.xml"));
        if (cards.LoadFile())
        {
            int n = 0;
            var definitions = cards.FirstChildElement("cards")?.FirstChildElement("definitions");
            for (var card = definitions?.FirstChildElement("card");
                 card != null; card = card.NextSiblingElement("card")) n++;
            Console.WriteLine("cards parsed: " + n);
        }
        TestCardEffects();
        var table = new ecStringTable();
        table.Load("stringtable_cn.xml");
        Console.WriteLine("stringtable_cn entries: " + table.table.Count + "  Infantry=" + table.GetString("Infantry") + "  conquest name 1=" + table.GetString("conquest name 1"));
        var loc = new ecStringTable();
        loc.Load("Localizable.strings");
        Console.WriteLine("Localizable language=" + loc.GetString("language") + " stringtable=" + loc.GetString("stringtable"));
    }

    private static void TestCardEffects()
    {
        CObjectDef definitions = CObjectDef.Instance();
        definitions.LoadArmyDef();
        definitions.LoadCardDef();

        if (definitions.armyDefsById.Count != 57)
            throw new InvalidOperationException(
                $"flat ArmyDef count mismatch: {definitions.armyDefsById.Count}");
        ArmyDef panzerDef = definitions.GetArmyDef((int)PANZER);
        ArmyDef infantryDef = definitions.GetArmyDef((int)INFANTRY);
        if (panzerDef?.unitType != "armor" || panzerDef.armyType != "panzer" ||
            infantryDef?.unitType != "infantry" || infantryDef.armyType != "infantry")
            throw new InvalidOperationException("ArmyDef three-level classification was not loaded");
        if (Math.Abs(GetCombatDamageRate(panzerDef.combat, infantryDef,
                COMBAT_DAMAGE_ATTACK) - 1.3f) > 0.001f)
            throw new InvalidOperationException("unit-type combat damage rule was not loaded");
        SetCombatDamageRate(panzerDef.combat, COMBAT_DAMAGE_ATTACK,
            ArmyDamageTargetKind.ARMY_TYPE, "infantry", 1.4);
        SetCombatDamageRate(panzerDef.combat, COMBAT_DAMAGE_ATTACK,
            ArmyDamageTargetKind.ARMY_ID, "infantry", 1.5);
        if (Math.Abs(GetCombatDamageRate(panzerDef.combat, infantryDef,
                COMBAT_DAMAGE_ATTACK) - 1.5f) > 0.001f)
            throw new InvalidOperationException("army-id combat damage rule did not take precedence");
        ClearCombatDamageRate(panzerDef.combat, COMBAT_DAMAGE_ATTACK,
            ArmyDamageTargetKind.ARMY_ID, "infantry");
        if (Math.Abs(GetCombatDamageRate(panzerDef.combat, infantryDef,
                COMBAT_DAMAGE_ATTACK) - 1.4f) > 0.001f)
            throw new InvalidOperationException("army-type combat damage rule did not take precedence");
        ClearCombatDamageRate(panzerDef.combat, COMBAT_DAMAGE_ATTACK,
            ArmyDamageTargetKind.ARMY_TYPE, "infantry");
        if (!armydef_internal.TryResolveArmyTypeId("am-airstrike", out int amAirstrikeId) ||
            !armydef_internal.TryResolveArmyTypeId("de-bomber", out int deBomberId))
            throw new InvalidOperationException("flat air mission ArmyDefs were not registered");

        CardDef infantry = definitions.GetCardDef(INFANTRY_CARD);
        CardDef airstrike = definitions.GetCardDef(AIR_STRIKE_CARD);
        CardDef bomber = definitions.GetCardDef(BOMBER_CARD);
        CardDef airborne = definitions.GetCardDef(AIRBORNE_FORCE_CARD);
        CardDef nuclear = definitions.GetCardDef(NUCLEAR_BOMB_CARD);
        CardDef city = definitions.GetCardDef(CITY_CARD);
        CardDef industry = definitions.GetCardDef(INDUSTRY_CARD);
        CardDef airport = definitions.GetCardDef(AIRPORT_CARD);
        CardDef fort = definitions.GetCardDef(LAND_FORT_CARD);
        CardDef entrenchment = definitions.GetCardDef(ENTRENCHMENT_CARD);
        CardDef antiaircraft = definitions.GetCardDef(ANTIAIRCRAFT_CARD);
        CardDef radar = definitions.GetCardDef(RADAR_CARD);
        CardDef carrier = definitions.GetCardDef(CARRIER_CARD);
        CardDef assault = definitions.GetCardDef(ASSAULT_ART_CARD);
        CardDef defence = definitions.GetCardDef(DEFEND_ART_CARD);
        CardDef commander = definitions.GetCardDef(COMMANDER_CARD);
        CardDef research = definitions.GetCardDef(RESEARCH_CARD);
        CardDef supplyLine = definitions.GetCardDef(SUPPLY_LINE_CARD);
        CardDef aceForces = definitions.GetCardDef(ACE_FORCES_CARD);
        RequireEffect<DraftArmyEffectDef>(infantry, "infantry", effect =>
            effect.armyTypeId == (int)INFANTRY);
        RequireEffect<StrategicStrikeEffectDef>(airstrike, "airstrike", effect =>
            effect.payload == COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE);
        RequireEffect<StrategicStrikeEffectDef>(bomber, "bomber", effect =>
            effect.payload == COMBAT_AIRSTRIKE_PAYLOAD_BOMBER);
        RequireEffect<AirborneDeployEffectDef>(airborne, "airborne-force", effect =>
            effect.armyTypeId == (int)INFANTRY && effect.capture);
        RequireEffect<StrategicStrikeEffectDef>(nuclear, "nuclear-bomb", effect =>
            effect.payload == COMBAT_AIRSTRIKE_PAYLOAD_NUCLEAR);
        RequireEffect<StrategicStrikeEffectDef>(definitions.GetCardDef("am-airstrike"),
            "am-airstrike", effect => effect.armyTypeId == amAirstrikeId);
        RequireEffect<StrategicStrikeEffectDef>(definitions.GetCardDef("de-bomber"),
            "de-bomber", effect => effect.armyTypeId == deBomberId);

        var amCountry = new CCountry { name = "am" };
        List<CARD_ID>[] amTabs = definitions.GetCardTab(amCountry);
        if (!amTabs[0].Contains(definitions.GetCardDef("am-infantry").id) ||
            !amTabs[2].Contains(definitions.GetCardDef("am-airstrike").id))
            throw new InvalidOperationException("AM card tabs do not use flat card variants");
        RequireConstructionEffect(city, "city", CITY);
        RequireConstructionEffect(industry, "industry", INDUSTRY);
        RequireConstructionEffect(airport, "airport", AIRPORT);
        RequireInstallationEffect(fort, "land-fort", FORT);
        RequireInstallationEffect(entrenchment, "entrenchment", ENTRENCHMENT);
        RequireInstallationEffect(antiaircraft, "antiaircraft", ANTIAIRCRAFT);
        RequireInstallationEffect(radar, "radar", RADAR);
        RequireUnitBuffEffect(carrier, "carrier", UnitBuffKind.CARRIER, BuffArmyTarget.SELECTED);
        RequireUnitBuffEffect(assault, "assault-art", UnitBuffKind.ASSAULT, BuffArmyTarget.SELECTED);
        RequireUnitBuffEffect(defence, "defend-art", UnitBuffKind.DEFENCE, BuffArmyTarget.SELECTED);
        if (commander == null || !commander.commanderPriceScaling || !commander.commanderCooldown)
            throw new InvalidOperationException("commander pricing/cooldown modes were not loaded");
        RequireEffect<ResearchEffectDef>(research, "research", effect =>
            effect.turns == 3 && effect.maxLevel == 5);
        RequireEffect<ArmyHealthEffectDef>(supplyLine, "supply-line", effect =>
            effect.type == "buff-army" && effect.target == BuffArmyTarget.ALL &&
            effect.IsPercent && effect.Amount == 100);
        RequireEffect<ArmyLevelEffectDef>(aceForces, "ace-forces", effect =>
            effect.type == "buff-army" && effect.target == BuffArmyTarget.FRONT &&
            effect.levels == 1);

        var country = new CCountry();
        var capital = NewTestArea(country, CAPITAL);
        CardEffectResult result = CardEffectExecutor.Apply(NewTestContext(country, city, capital));
        if (!result.Success || capital.info.constructionType != CITY ||
            capital.info.constructionLevel != MAX_CITY_LEVEL)
            throw new InvalidOperationException("city effect did not preserve the capital default level");

        if (CardEffectExecutor.CanApply(NewTestContext(country, airport, capital), out _))
            throw new InvalidOperationException("airport effect accepted an area with a different construction");

        var plain = NewTestArea(country, NORMAL);
        result = CardEffectExecutor.Apply(NewTestContext(country, industry, plain));
        if (!result.Success || plain.info.constructionType != INDUSTRY || plain.info.constructionLevel != 1)
            throw new InvalidOperationException("industry effect did not construct level one on plain land");

        var maximize = new CardDef { key = "test-maximize" };
        maximize.effects.Add(new AreaStructureEffectDef
        {
            type = "area-structure",
            structure = INDUSTRY,
            operation = CardEffectOperation.ADJUST,
            toMaximum = true,
        });
        result = CardEffectExecutor.Apply(NewTestContext(country, maximize, plain));
        if (!result.Success || plain.info.constructionLevel != MAX_INDUSTRY_LEVEL)
            throw new InvalidOperationException("+inf did not maximize the construction level");

        var clear = new CardDef { key = "test-clear" };
        clear.effects.Add(new AreaStructureEffectDef
        {
            type = "area-structure",
            operation = CardEffectOperation.ADJUST,
            structure = INDUSTRY,
            levelDelta = -MAX_INDUSTRY_LEVEL,
        });
        result = CardEffectExecutor.Apply(NewTestContext(country, clear, plain));
        if (!result.Success || plain.info.constructionType != NO_CONSTRUCTION || plain.info.constructionLevel != 0)
            throw new InvalidOperationException("clear did not remove the construction");

        result = CardEffectExecutor.Apply(NewTestContext(country, fort, plain));
        if (!result.Success || plain.installationType != FORT)
            throw new InvalidOperationException("fort effect did not set the installation");
        if (CardEffectExecutor.CanApply(NewTestContext(country, radar, plain), out _))
            throw new InvalidOperationException("installation effect accepted an occupied installation slot");

        var clearInstallation = new CardDef { key = "test-clear-installation" };
        clearInstallation.effects.Add(new AreaStructureEffectDef
        {
            type = "area-structure",
            slot = AreaStructureSlot.INSTALLATION,
            operation = CardEffectOperation.CLEAR,
        });
        result = CardEffectExecutor.Apply(NewTestContext(country, clearInstallation, plain));
        if (!result.Success || plain.installationType != NO_INSTALLATION)
            throw new InvalidOperationException("installation clear did not remove the installation");

        plain.armyCount = 1;
        plain.army[0] = new CArmy
        {
            basicAbilities = new ArmyDef { hp = 100 },
            country = country,
            hp = 100,
            maxHp = 100,
        };
        result = CardEffectExecutor.Apply(NewTestContext(country, assault, plain, 0));
        if (!result.Success || !plain.army[0].HasCard(ASSAULT))
            throw new InvalidOperationException("assault effect did not add the unit buff");
        if (CardEffectExecutor.CanApply(NewTestContext(country, assault, plain, 0), out _))
            throw new InvalidOperationException("buff-army accepted a duplicate buff");

        Console.WriteLine("card effects: XML parser and basic handlers ok");
    }

    private static void RequireEffect<TEffect>(
        CardDef card, string expectedKey, Func<TEffect, bool> validate)
        where TEffect : CardEffectDef
    {
        if (card == null || card.key != expectedKey || card.effects.Count != 1 ||
            card.effects[0] is not TEffect effect || !validate(effect))
            throw new InvalidOperationException($"card '{expectedKey}' did not load its expected effect");
    }

    private static void RequireConstructionEffect(
        CardDef card, string expectedKey, CONSTRUCTION_TYPE expectedStructure)
    {
        if (card == null || card.key != expectedKey || card.effects.Count != 1 ||
            card.effects[0] is not AreaStructureEffectDef effect ||
            effect.structure != expectedStructure || effect.operation != CardEffectOperation.ADJUST ||
            effect.levelDelta != 1 || card.presentation?.sound != "build")
            throw new InvalidOperationException($"card '{expectedKey}' did not load its construction effect");
    }

    private static void RequireInstallationEffect(
        CardDef card, string expectedKey, INSTALLATION_TYPE expectedInstallation)
    {
        if (card == null || card.key != expectedKey || card.effects.Count != 1 ||
            card.effects[0] is not AreaStructureEffectDef effect ||
            effect.slot != AreaStructureSlot.INSTALLATION || effect.installation != expectedInstallation ||
            effect.operation != CardEffectOperation.SET || card.presentation?.sound != "build")
            throw new InvalidOperationException($"card '{expectedKey}' did not load its installation effect");
    }

    private static void RequireUnitBuffEffect(
        CardDef card, string expectedKey, UnitBuffKind expectedBuff, BuffArmyTarget expectedTarget)
    {
        if (card == null || card.key != expectedKey || card.effects.Count != 1 ||
            card.effects[0] is not UnitBuffEffectDef effect || effect.buff != expectedBuff ||
            effect.type != "buff-army" || effect.target != expectedTarget ||
            card.presentation?.sound != "buff")
            throw new InvalidOperationException($"card '{expectedKey}' did not load its unit buff effect");
    }

    private static CArea NewTestArea(CCountry country, AREA_TYPE areaType)
    {
        var area = new CArea();
        area.Init(1, new AreaInfo(areaType, 0));
        area.country = country;
        return area;
    }

    private static CardEffectContext NewTestContext(
        CCountry country, CardDef card, CArea area, int armyIndex = -1) => new()
    {
        sourceCountry = country,
        card = card,
        targetArea = area,
        targetAreaId = area.id,
        targetArmyIndex = armyIndex,
    };

    /// <summary>
    /// Decodes every MP3 of the assets with the music decoder used by the game, prints format and
    /// level statistics and (optionally) writes the PCM as WAV files into outDir for inspection.
    /// </summary>
    private static void TestAudio(string outDir)
    {
        ecGame.ConfigurePaths(null, null);
        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
        foreach (var path in Directory.GetFiles(AssetsRoot, "*.mp3").OrderBy(p => p))
        {
            try
            {
                var pcm = CCSoundBox.DecodeMp3ToPcm(path, out int sampleRate, out int channels, out string info);
                int samples = pcm.Length / 2;
                double sum = 0; short peak = 0; int clipped = 0; double sameAdjacent = 0;
                short prev = 0;
                for (int i = 0; i < samples; i++)
                {
                    short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                    int a = Math.Abs((int)s);
                    if (a > peak) peak = (short)Math.Min(a, short.MaxValue);
                    if (a >= 32700) clipped++;
                    sum += (double)s * s;
                    if (i > 0 && s == prev) sameAdjacent++;
                    prev = s;
                }
                double rms = samples > 0 ? Math.Sqrt(sum / samples) : 0;
                double seconds = sampleRate > 0 && channels > 0 ? samples / (double)(sampleRate * channels) : 0;
                Console.WriteLine($"{Path.GetFileName(path),-18} {info}  decoded: rate={sampleRate} ch={channels} samples={samples} ({seconds:F1}s) peak={peak} rms={rms:F0} clipped={clipped} sameAdjacent={sameAdjacent / Math.Max(1, samples):P1}");
                if (!string.IsNullOrEmpty(outDir))
                {
                    string outPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + ".nlayer.wav");
                    WriteWav(outPath, pcm, sampleRate, channels);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{Path.GetFileName(path),-18} FAILED: {ex.Message}");
            }
        }

        // sound effects: load every WAV through MonoGame and compare the duration with the header
        int wavOk = 0, wavBad = 0;
        foreach (var path in Directory.GetFiles(AssetsRoot, "*.wav").OrderBy(p => p))
        {
            try
            {
                var header = ReadWavHeader(path);
                using var fs = File.OpenRead(path);
                using var effect = Microsoft.Xna.Framework.Audio.SoundEffect.FromStream(fs);
                double loaded = effect.Duration.TotalSeconds;
                bool ok = Math.Abs(loaded - header.seconds) < 0.05;
                if (ok) wavOk++; else wavBad++;
                Console.WriteLine($"{Path.GetFileName(path),-22} {header.rate} Hz {header.bits}-bit ch={header.channels} header={header.seconds:F2}s loaded={loaded:F2}s {(ok ? "ok" : "MISMATCH")}");
            }
            catch (Exception ex)
            {
                wavBad++;
                Console.WriteLine($"{Path.GetFileName(path),-22} FAILED: {ex.Message}");
            }
        }
        Console.WriteLine($"wav effects: {wavOk} ok, {wavBad} bad");
    }

    private static (int rate, int bits, int channels, double seconds) ReadWavHeader(string path)
    {
        var d = File.ReadAllBytes(path);
        int i = 12, rate = 0, bits = 0, channels = 0, dataSize = 0;
        while (i + 8 <= d.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(d, i, 4);
            int size = BitConverter.ToInt32(d, i + 4);
            if (id == "fmt ") { channels = BitConverter.ToInt16(d, i + 10); rate = BitConverter.ToInt32(d, i + 12); bits = BitConverter.ToInt16(d, i + 22); }
            else if (id == "data") { dataSize = Math.Min(size, d.Length - i - 8); }
            i += 8 + size + (size & 1);
        }
        double seconds = rate > 0 && bits > 0 && channels > 0 ? dataSize / (double)(rate * channels * bits / 8) : 0;
        return (rate, bits, channels, seconds);
    }

    private static void WriteWav(string path, byte[] pcm, int sampleRate, int channels)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        int byteRate = sampleRate * channels * 2;
        w.Write("RIFF"u8); w.Write(36 + pcm.Length); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(sampleRate); w.Write(byteRate); w.Write((short)(channels * 2)); w.Write((short)16);
        w.Write("data"u8); w.Write(pcm.Length); w.Write(pcm);
    }

    private static void TestFonts()
    {
        ecGame.ConfigurePaths(null, null);
        foreach (var f in new[] { "font1.fnt", "font2_cn_hd.fnt", "fontht_cn_hd.fnt", "num3_hd.fnt" })
        {
            var data = File.ReadAllBytes(GetPath(f));
            uint glyphs = BitConverter.ToUInt32(data, 0), height = BitConverter.ToUInt32(data, 4);
            Console.WriteLine($"{f}: glyphs={glyphs} height={height} size={data.Length} expected={8 + glyphs * 12}");
        }
    }
}
