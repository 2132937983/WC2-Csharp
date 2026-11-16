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
            for (var c = cards.FirstChildElement("cards")?.FirstChildElement("country"); c != null; c = c.NextSiblingElement("country"))
                for (var card = c.FirstChildElement("card"); card != null; card = card.NextSiblingElement("card"))
                {
                    n++;
                    if (card.Attribute("name") == "Rocket") Console.WriteLine("Rocket card image attr = " + card.Attribute("image") + " relatedArmyId=" + card.Attribute("relatedArmyId"));
                }
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

        CardDef infantry = definitions.GetNewCardDef(INFANTRY_CARD, "others");
        CardDef airstrike = definitions.GetNewCardDef(AIR_STRIKE_CARD, "others");
        CardDef bomber = definitions.GetNewCardDef(BOMBER_CARD, "others");
        CardDef airborne = definitions.GetNewCardDef(AIRBORNE_FORCE_CARD, "others");
        CardDef nuclear = definitions.GetNewCardDef(NUCLEAR_BOMB_CARD, "others");
        CardDef city = definitions.GetNewCardDef(CITY_CARD, "others");
        CardDef industry = definitions.GetNewCardDef(INDUSTRY_CARD, "others");
        CardDef airport = definitions.GetNewCardDef(AIRPORT_CARD, "others");
        CardDef fort = definitions.GetNewCardDef(LAND_FORT_CARD, "others");
        CardDef entrenchment = definitions.GetNewCardDef(ENTRENCHMENT_CARD, "others");
        CardDef antiaircraft = definitions.GetNewCardDef(ANTIAIRCRAFT_CARD, "others");
        CardDef radar = definitions.GetNewCardDef(RADAR_CARD, "others");
        CardDef carrier = definitions.GetNewCardDef(CARRIER_CARD, "others");
        CardDef assault = definitions.GetNewCardDef(ASSAULT_ART_CARD, "others");
        CardDef defence = definitions.GetNewCardDef(DEFEND_ART_CARD, "others");
        CardDef commander = definitions.GetNewCardDef(COMMANDER_CARD, "others");
        CardDef research = definitions.GetNewCardDef(RESEARCH_CARD, "others");
        CardDef supplyLine = definitions.GetNewCardDef(SUPPLY_LINE_CARD, "others");
        CardDef aceForces = definitions.GetNewCardDef(ACE_FORCES_CARD, "others");
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
        RequireConstructionEffect(city, "city", CITY);
        RequireConstructionEffect(industry, "industry", INDUSTRY);
        RequireConstructionEffect(airport, "airport", AIRPORT);
        RequireInstallationEffect(fort, "land-fort", FORT);
        RequireInstallationEffect(entrenchment, "entrenchment", ENTRENCHMENT);
        RequireInstallationEffect(antiaircraft, "antiaircraft", ANTIAIRCRAFT);
        RequireInstallationEffect(radar, "radar", RADAR);
        RequireUnitBuffEffect(carrier, "carrier", UnitBuffKind.Carrier, UnitBuffTarget.SelectedArmy);
        RequireUnitBuffEffect(assault, "assault-art", UnitBuffKind.Assault, UnitBuffTarget.SelectedArmy);
        RequireUnitBuffEffect(defence, "defend-art", UnitBuffKind.Defence, UnitBuffTarget.SelectedArmy);
        if (commander == null || !commander.commanderPriceScaling || !commander.commanderCooldown)
            throw new InvalidOperationException("commander pricing/cooldown modes were not loaded");
        RequireEffect<ResearchEffectDef>(research, "research", effect =>
            effect.turns == 3 && effect.maxLevel == 5);
        RequireEffect<ArmyHealthEffectDef>(supplyLine, "supply-line", effect =>
            effect.target == ArmyEffectTarget.AllArmies && effect.IsPercent && effect.Amount == 100);
        RequireEffect<ArmyLevelEffectDef>(aceForces, "ace-forces", effect =>
            effect.target == ArmyEffectTarget.FrontArmy && effect.levels == 1);

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
            operation = CardEffectOperation.Adjust,
            toMaximum = true,
        });
        result = CardEffectExecutor.Apply(NewTestContext(country, maximize, plain));
        if (!result.Success || plain.info.constructionLevel != MAX_INDUSTRY_LEVEL)
            throw new InvalidOperationException("+inf did not maximize the construction level");

        var clear = new CardDef { key = "test-clear" };
        clear.effects.Add(new AreaStructureEffectDef
        {
            type = "area-structure",
            operation = CardEffectOperation.Clear,
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
            slot = AreaStructureSlot.Installation,
            operation = CardEffectOperation.Clear,
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
            throw new InvalidOperationException("unit-buff accepted a duplicate buff");

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
            effect.structure != expectedStructure || effect.operation != CardEffectOperation.Adjust ||
            effect.levelDelta != 1 || card.presentation?.sound != "build")
            throw new InvalidOperationException($"card '{expectedKey}' did not load its construction effect");
    }

    private static void RequireInstallationEffect(
        CardDef card, string expectedKey, INSTALLATION_TYPE expectedInstallation)
    {
        if (card == null || card.key != expectedKey || card.effects.Count != 1 ||
            card.effects[0] is not AreaStructureEffectDef effect ||
            effect.slot != AreaStructureSlot.Installation || effect.installation != expectedInstallation ||
            effect.operation != CardEffectOperation.Set || card.presentation?.sound != "build")
            throw new InvalidOperationException($"card '{expectedKey}' did not load its installation effect");
    }

    private static void RequireUnitBuffEffect(
        CardDef card, string expectedKey, UnitBuffKind expectedBuff, UnitBuffTarget expectedTarget)
    {
        if (card == null || card.key != expectedKey || card.effects.Count != 1 ||
            card.effects[0] is not UnitBuffEffectDef effect || effect.buff != expectedBuff ||
            effect.target != expectedTarget || card.presentation?.sound != "buff")
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
