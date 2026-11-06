namespace WC2;

/// <summary>Resolved unit image, so the render frame does not query ArmyDef repeatedly.</summary>
public sealed class CGameResArmyRenderCacheEntry
{
    public ecImage image;
    public bool resolved;
}

/// <summary>In-game shared images: units, bases, flags, buildings, cards (CGameRes.cpp).</summary>
public sealed class CGameRes
{
    public ecTextureRes textureResArmy = new();
    public ecImage imageCarrier;
    public Dictionary<string, ecImage> imageSoldier = new();
    public Dictionary<string, ecImage> imagePanzer = new();
    public Dictionary<string, ecImage> imageCannon = new();
    public Dictionary<string, ecImage> imageRocketlauncher = new();
    public Dictionary<string, ecImage> imageTank = new();
    public Dictionary<string, ecImage> imageHeavytank = new();
    public ecImage[] imageArmyNe = new ecImage[10];
    public ecImage[] imageUnitbaseBlue = new ecImage[4];
    public ecImage[] imageUnitbaseGreen = new ecImage[4];
    public ecImage[] imageUnitbaseRed = new ecImage[4];
    public ecImage[] imageUnitbaseGrey = new ecImage[4];
    public ecImage[] imageUnitlevelmark = new ecImage[4];
    public ecImage[] imageCommanderLevel = new ecImage[5];
    public ecImage imageMedal;
    public ecImage imageMedalLight;
    public Dictionary<string, ecImage> imageCountryMedal = new();
    public ecImage[] imageMarkCarriers = new ecImage[6];
    public ecImage[] imageMarkCarriersColor = new ecImage[4];
    public ecImage imageHpbar;
    public ecImage imageHpbarFill;
    public ecImage[] imageMovementmark = new ecImage[4];
    public ecImage[] imageCardmark = new ecImage[3];
    public ecImage imageArrowGreen;
    public ecImage imageArrowYellow;
    public ecImage imageArrowRed;
    public ecImage imageArrowBlue;
    public ecImage imageArrowshadow;
    public Dictionary<string, ecImage> imageFlag = new();
    public ecImage[] imageBuildmarkInstalltion = new ecImage[4];
    public ecImage[] imageBuildmarkCity = new ecImage[4];
    public ecImage[] imageBuildmarkFactory = new ecImage[3];
    public ecImage imageBuildmarkAirport;
    public ecImage imageBuildmarkPort;
    public ecTextureRes textureResCardtex = new();
    public ecImage imageCardShadow;
    public ecImage imageCardCommon;
    public ecImage[] imageCardResearch = new ecImage[5];
    public ecTextureRes textureResBattlebg = new();
    public ecTextureRes textureResFlag = new();
    public ecText imageMovementText;
    public Dictionary<int, Dictionary<string, ecImage>> imageArmies = new();
    public Dictionary<int, ecImage> imageMarkCarriersNew = new();
    /// <summary>Runtime cache: army type + country to unit image and navy flag.</summary>
    public Dictionary<int, Dictionary<string, CGameResArmyRenderCacheEntry>> armyRenderCache = new();

    // Country ids that may own dedicated unit art / flags in the resource pack; "others" is the fallback key.
    public static readonly List<string> country_list = new()
    {
        "gb", "ru", "am", "fr", "ja", "it", "de", "ca", "fl", "ro",
        "bg", "be", "gr", "cn", "tw", "in", "au", "pl", "hu", "yu",
        "ch", "se", "pt", "dk", "no", "nl", "es", "mx", "nk", "rk",
        "tr", "ne", "others", "tes",
    };

    // [0] land, [1] navy: converts the ArmyDef id to the resource name prefix used in army.xml
    // (listed in key order, like the std::map of the original).
    private static readonly KeyValuePair<int, string>[][] ne_troop_map =
    {
        new[]
        {
            new KeyValuePair<int, string>((int)INFANTRY, "soldier"),
            new KeyValuePair<int, string>((int)PANZER, "panzer"),
            new KeyValuePair<int, string>((int)ARTILLERY, "cannon"),
            new KeyValuePair<int, string>((int)ROCKET, "rocketlauncher"),
            new KeyValuePair<int, string>((int)TANK, "tank"),
            new KeyValuePair<int, string>((int)HEAVY_TANK, "heavytank"),
            new KeyValuePair<int, string>((int)ELITE_INFANTRY, "eliteinfantry"),
        },
        new[]
        {
            new KeyValuePair<int, string>((int)DESTROYER, "destroyer"),
            new KeyValuePair<int, string>((int)CRUISER, "cruiser"),
            new KeyValuePair<int, string>((int)BATTLESHIP, "battleship"),
            new KeyValuePair<int, string>((int)AIRCRAFT_CARRIER, "aircraftcarrier"),
        },
    };
    private static readonly string[] medal_list = { "gb", "de", "am", "fr", "it", "ru", "ja", "cn", "tw", "common_1", "common_2", "common_3" };
    private static readonly string[] alliance_list = { "blue", "green", "red", "gray" };
    private static readonly string[] installtion_list = { "fortress", "wire", "aagun", "radar" };

    // Effect texture tables loaded on behalf of the (missing) ecEffectResManager.

    /// <summary>Initialises the resource index containers; the images come from the texture tables in Load.</summary>
    public CGameRes()
    {
        imageSoldier.Clear();
        imagePanzer.Clear();
        imageCannon.Clear();
        imageRocketlauncher.Clear();
        imageTank.Clear();
        imageHeavytank.Clear();
        imageCountryMedal.Clear();
        imageFlag.Clear();
        armyRenderCache.Clear();
    }

    // ------------------------------------------------------------------------------------
    // ecEffectResManager wrappers (the effect atlas and definitions live in the effect manager).
    // ------------------------------------------------------------------------------------

    private static void LoadEffectTextureRes(string name) => ecEffectResManager.Instance().LoadTextureRes(name);

    private static void LoadEffectRes(string name) => ecEffectResManager.Instance().LoadEffectRes(name);

    private static void ReleaseAllEffectRes() => ecEffectResManager.Instance().ReleaseAllEffectRes();

    private static void ReleaseEffectTextureRes() => ecEffectResManager.Instance().ReleaseTextureRes();

    /// <summary>
    /// Renders the country badge of an AI commander. armyCount selects the stack base height;
    /// without a country badge the common badge of commonType is used.
    /// </summary>
    public void RenderAICommanderMedal(int armyCount, float x, float y, string countryName, int commonType)
    {
        float[] tmp = { 10.0f, 13.0f, 15.0f, 17.0f };
        // Every stack layer uses a different base height; the badge follows it upwards.
        float suited_y = y - tmp[armyCount - 1];
        string true_name = countryName ?? "";
        if (!imageCountryMedal.ContainsKey(true_name))
        {
            if (commonType == 1)
                true_name = "common_1";
            else if (commonType == 2)
                true_name = "common_2";
            else
                true_name = "common_3";
        }
        if (imageCountryMedal.TryGetValue(true_name, out ecImage medal) && medal != null)
            medal.Render(x + 8.0f, suited_y - 8.0f);
    }

    /// <summary>
    /// Overlays the health bar, movement, level and tactic card marks next to a map unit.
    /// y moves up with the stack height so the top unit info aligns with its base.
    /// </summary>
    public void RenderArmyInfo(int armyCount, float x, float y, int hp, int maxHp, int movement, int cards, int level)
    {
        float[] tmp = { 10.0f, 13.0f, 15.0f, 17.0f };
        y -= tmp[armyCount - 1];
        imageHpbar.Render(x - 30.0f, y - 12.0f);
        imageHpbarFill.SetColor(HpColor(hp, maxHp), -1);
        // 33.0f is the design width of the fill; the X scale of RenderEx is the current hp ratio.
        imageHpbarFill.RenderEx(x - 11.0f, y - 4.0f, 0.0f, (float)hp * 33.0f / (float)maxHp, 1.0f);
        RenderArmyMovementNum(x - 23.0f, y - 7.0f, movement);
        if (level > 0)
            imageUnitlevelmark[level - 1].Render(x + 8.0f, y - 8.0f);
        // Only the first three unit cards are rendered; COMMANDER uses its own level/badge icon.
        if (cards == 0)
            return;
        for (int i = 0; i < 3; i++)
        {
            if (((cards >> i) & 1) != 0)
            {
                imageCardmark[i].Render(x + 26.0f, y);
                y -= 15.0f;
            }
        }
    }

    /// <summary>Renders the fill of a unit health bar.</summary>
    public void RenderArmyHP(float x, float y, int hp, int maxHp)
    {
        imageHpbarFill.SetColor(HpColor(hp, maxHp), -1);
        imageHpbarFill.RenderEx(x, y, 0.0f, (hp * 33.0f) / maxHp, 1.0f);
    }

    /// <summary>
    /// Renders a unit level mark. The library read imageUnitbaseGrey[level + 3], which through the
    /// contiguous layout is imageUnitlevelmark[level - 1]; the source keeps the intended access.
    /// </summary>
    public void RenderArmyLevel(float x, float y, int level)
    {
        if (level > 0)
            imageUnitlevelmark[level - 1].Render(x, y);
    }

    /// <summary>
    /// Draws the remaining movement state: the exact number when movement is left, the no-move icon otherwise.
    /// </summary>
    public void RenderArmyMovementNum(float x, float y, int movement)
    {
        if (movement > 0)
        {
            string movementText = Sprintf("%d", movement);
            imageMovementText.SetText(movementText);
            imageMovementText.DrawText(x + 7.0f, y - 2.0f, 1);
        }
        else
        {
            imageMovementmark[0].Render(x, y);
        }
    }

    /// <summary>Renders a construction (city, industry or airport) of the given level.</summary>
    public void RenderConstruction(int Type, int Level, float x, float y)
    {
        switch ((CONSTRUCTION_TYPE)Type)
        {
            case CITY:
                if (Level > 0)
                    imageBuildmarkCity[Level - 1].Render(x, y);
                break;
            case INDUSTRY:
                if (Level > 0)
                    imageBuildmarkFactory[Level - 1].Render(x, y);
                break;
            case AIRPORT:
                imageBuildmarkAirport.Render(x, y);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Renders the unit preview of the generic UI. Unlike map units there is no alliance base; the
    /// image is always mirrored, then the status bars and cards are overlaid.
    /// </summary>
    public void RenderUIArmy(string CountryName, float x, float y, int ArmyType, bool sea, int HP, int MaxHP,
                             int Movement, int Cards, int Level)
    {
        ecImage ArmyImage = GetArmyImage(CountryName, ArmyType, false);
        if (ArmyImage != null)
        {
            ArmyImage.SetColor(0xFFFFFFFF, -1);
            ArmyImage.RenderEx(x, y, 0.0f, -1.0f, 1.0f);
        }
        imageHpbar.Render(x - 30.0f, y - 12.0f);
        imageHpbarFill.SetColor(HpColor(HP, MaxHP), -1);
        imageHpbarFill.RenderEx(x - 11.0f, y - 4.0f, 0.0f, (float)HP * 33.0f / (float)MaxHP, 1.0f);
        RenderArmyMovementNum(x - 23.0f, y - 7.0f, Movement);
        if ((Cards & (1 << (int)COMMANDER)) != 0)
        {
            // The commander level icon takes precedence over the normal unit level icon.
            imageCommanderLevel[Level / 3].Render(x + 8.0f, y - 8.0f);
        }
        else if (Level > 0)
        {
            imageUnitlevelmark[Level - 1].Render(x + 8.0f, y - 8.0f);
        }
        if (Cards == 0)
            return;
        // UI cards stack downwards right of the unit so they do not cover the health bar and level icon.
        y -= 20.0f;
        int i;
        for (i = 0; i < 3; i++)
        {
            if (((Cards >> i) & 1) != 0)
            {
                imageCardmark[i].Render(x + 16.0f, y);
                y -= 15.0f;
            }
        }
    }

    private static Dictionary<string, ecImage> ArmyImagesOf(Dictionary<int, Dictionary<string, ecImage>> map, int armyId)
    {
        if (!map.TryGetValue(armyId, out var byCountry))
        {
            byCountry = new Dictionary<string, ecImage>();
            map[armyId] = byCountry;
        }
        return byCountry;
    }

    /// <summary>
    /// Loads the textures, icons and effects shared by the battle map and the GUI and builds the
    /// army id to country image index. Resolution and wide mode select the xml packs; callers
    /// must Release before loading again.
    /// </summary>
    public void Load()
    {
        // Drop the old unit image cache on reload so it cannot reference old definitions or textures.
        armyRenderCache.Clear();
        // army and cardtex supply most in-level HUD images; the second argument is the hd handling.
        if (g_contenscalefactor == 2.0f)
        {
            textureResArmy.LoadRes("army_hd.xml", true);
            textureResCardtex.LoadRes("cardtex_hd.xml", true);
        }
        else
        {
            textureResArmy.LoadRes("army.xml", false);
            textureResCardtex.LoadRes("cardtex.xml", false);
        }

        // Walk land/navy, then the countries; missing images are not stored and fall back to others on lookup.
        for (int i = 0; i < 2; i++)
        {
            KeyValuePair<int, string>[] ne_sp_troop_map = ne_troop_map[i];
            foreach (KeyValuePair<int, string> armytype_pic_pair in ne_sp_troop_map)
            {
                string army_pic_name = armytype_pic_pair.Value;
                int armyId = armytype_pic_pair.Key;
                foreach (string country_name in country_list)
                {
                    ecImage imageSrc = textureResArmy.GetImageByFormat("%s_%s.png", army_pic_name, country_name);
                    if (imageSrc != null)
                        ArmyImagesOf(imageArmies, armyId)[country_name] = imageSrc;
                }
                // Land units only: the carrier mark applies to land units, navy needs no overlay.
                if (i == 0)
                {
                    ecImage carrierImage = textureResArmy.GetImageByFormat("mark_carriers_%s.png", army_pic_name);
                    if (carrierImage != null)
                        imageMarkCarriersNew[armyId] = carrierImage;
                }
            }
        }

        // Custom army types added in armydef.xml look for their art by type name by default:
        // <army type="super_tank"> maps to super_tank_<country>.png. Built-in types are already
        // covered by the legacy mapping above; this only adds ids missing from imageArmies.
        CObjectDef objectDef = CObjectDef.Instance();
        // Two passes so the others defaults are written first and the country overrides last,
        // independent of the country name ordering of the map.
        for (int pass = 0; pass < 2; ++pass)
        {
            foreach (KeyValuePair<string, ArmyDefList> countryEntry in objectDef.mapArmyDefList)
            {
                string countryName = countryEntry.Key;
                bool isDefaultCountry = countryName == "others";
                if ((pass == 0) != isDefaultCountry)
                    continue;
                ArmyDefList armyList = countryEntry.Value;
                if (armyList == null)
                    continue;
                foreach (KeyValuePair<int, ArmyDef> armyEntry in armyList.def)
                {
                    int armyId = armyEntry.Key;
                    ArmyDef armyDef = armyEntry.Value;
                    if (armyDef == null)
                        continue;
                    // armydef.xml only stores the base name of the unit image; the atlas
                    // coordinates still come from the <Image> entries of army.xml / army_hd.xml.
                    string armyName = armydef_internal.GetArmyTypeImageName(armyId) ?? "";
                    string overrideName = armyDef.imageName;
                    if (!string.IsNullOrEmpty(overrideName))
                        armyName = overrideName;
                    if (string.IsNullOrEmpty(armyName))
                        armyName = armyDef.name ?? "";
                    if (string.IsNullOrEmpty(armyName))
                        continue;
                    bool imageCountryIndependent = armydef_internal.IsArmyTypeImageCountryIndependent(armyId);
                    if (armyDef.hasImageCountryIndependent)
                        imageCountryIndependent = armyDef.imageCountryIndependent;
                    // others is the default definition: its image fills the fallback slot of every country.
                    // A country definition only writes its own slot so it cannot pollute other countries.
                    if (imageCountryIndependent)
                    {
                        string imageKey = countryName == "others" ? "others" : countryName;
                        ecImage image = textureResArmy.GetImageByFormat("%s.png", armyName);
                        if (image != null)
                            ArmyImagesOf(imageArmies, armyId)[imageKey] = image;
                    }
                    else if (countryName == "others")
                    {
                        foreach (string targetCountry in country_list)
                        {
                            ecImage image = textureResArmy.GetImageByFormat("%s_%s.png", armyName, targetCountry);
                            // Without an others specific image, the plain image is the shared fallback of every country.
                            if (image == null && targetCountry == "others")
                                image = textureResArmy.GetImageByFormat("%s.png", armyName);
                            if (image != null)
                                ArmyImagesOf(imageArmies, armyId)[targetCountry] = image;
                        }
                    }
                    else
                    {
                        ecImage image = textureResArmy.GetImageByFormat("%s_%s.png", armyName, countryName);
                        // Missing country image: fall back to the country independent image.
                        if (image == null)
                            image = textureResArmy.GetImageByFormat("%s.png", armyName);
                        if (image != null)
                            ArmyImagesOf(imageArmies, armyId)[countryName] = image;
                    }
                }
            }
        }

        foreach (string country_name in country_list)
        {
            // Flags load independently of the unit art; a country without unit art may still own a flag.
            imageFlag[country_name] = textureResArmy.GetImageByFormat("flag_%s.png", country_name);
        }

        imageCarrier = textureResArmy.GetImageByFormat("transportship.png");
        // AI commander badges are indexed by country id or common_*, see RenderAICommanderMedal.
        foreach (string medal_name in medal_list)
            imageCountryMedal[medal_name] = textureResArmy.GetImageByFormat("medal_%s.png", medal_name);

        for (int i = 0; i < 3; i++)
        {
            imageCardmark[i] = textureResArmy.GetImageByFormat("cardmark_%d.png", i + 1);
            imageBuildmarkFactory[i] = textureResArmy.GetImageByFormat("buildmark_factory_%d.png", i + 1);
        }

        for (int i = 0; i < 4; i++)
        {
            // Index i is stack layer i + 1, also reused for unit level, city level and installation icons.
            imageUnitbaseBlue[i] = textureResArmy.GetImageByFormat("unitbase_blue_%d.png", i + 1);
            imageUnitbaseGreen[i] = textureResArmy.GetImageByFormat("unitbase_green_%d.png", i + 1);
            imageUnitbaseRed[i] = textureResArmy.GetImageByFormat("unitbase_red_%d.png", i + 1);
            imageUnitbaseGrey[i] = textureResArmy.GetImageByFormat("unitbase_gray_%d.png", i + 1);
            imageUnitlevelmark[i] = textureResArmy.GetImageByFormat("unitlevelmark_%d.png", i + 1);
            imageMovementmark[i] = textureResArmy.GetImageByFormat(i > 0 ? "hpbar_movementmark_%d.png" : "hpbar_movementmark_e.png", i);
            imageBuildmarkCity[i] = textureResArmy.GetImageByFormat("buildmark_city_%d.png", i + 1);
            imageMarkCarriersColor[i] = textureResArmy.GetImageByFormat("mark_carriers_%s.png", alliance_list[i]);
            imageBuildmarkInstalltion[i] = textureResArmy.GetImageByFormat("buildmark_%s.png", installtion_list[i]);
        }

        for (int i = 0; i < 5; i++)
        {
            imageCommanderLevel[i] = textureResArmy.GetImageByFormat("commander_level_%d_s.png", i + 1);
            imageCardResearch[i] = textureResCardtex.GetImageByFormat(i < 4 ? "card_d_research_%d.png" : "card_d_research_max.png", i + 2);
        }

        for (int i = 0; i < 6; i++)
            imageMarkCarriers[i] = textureResArmy.GetImageByFormat("mark_carriers_%d.png", i + 1);

        imageMedal = textureResArmy.GetImageByFormat("medal.png");
        imageMedalLight = textureResArmy.GetImageByFormat("medal_light.png");
        imageHpbar = textureResArmy.GetImageByFormat("hpbar.png");
        imageHpbarFill = textureResArmy.GetImageByFormat("hpbar_fill.png");
        // Cut one pixel off the left of the fill so the scaled fill never covers the frame.
        if (imageHpbarFill != null)
        {
            imageHpbarFill.SetTextureRect(imageHpbarFill.textureRect.x + 1.0f,
                                          imageHpbarFill.textureRect.y,
                                          1.0f, imageHpbarFill.textureRect.h);
        }
        imageArrowGreen = textureResArmy.GetImageByFormat("arrow_green.png");
        imageArrowYellow = textureResArmy.GetImageByFormat("arrow_yellow.png");
        imageArrowshadow = textureResArmy.GetImageByFormat("arrowshadow.png");
        imageArrowRed = textureResArmy.GetImageByFormat("arrow_red.png");
        imageArrowBlue = textureResArmy.GetImageByFormat("arrow_blue.png");
        imageBuildmarkAirport = textureResArmy.GetImageByFormat("buildmark_airport.png");
        imageBuildmarkPort = textureResArmy.GetImageByFormat("buildmark_port.png");
        imageCardShadow = textureResCardtex.GetImageByFormat("card_shadow.png");
        imageCardCommon = textureResCardtex.GetImageByFormat("card_common.png");

        // The battle background layout depends on the device width and the content scale.
        ecGraphics graphics = ecGraphics.Instance();
        if (graphics.widthMode == 3)
        {
            textureResBattlebg.LoadRes("battlebg1_iPad.xml", false);
            textureResBattlebg.LoadRes("battlebg2_iPad.xml", false);
            textureResBattlebg.LoadRes("battlebg3_iPad.xml", false);
        }
        else
        {
            if (g_contenscalefactor == 2.0f)
            {
                if (graphics.screenW <= 480.0)
                {
                    textureResBattlebg.LoadRes("battlebg1_hd.xml", false);
                    textureResBattlebg.LoadRes("battlebg2_hd.xml", false);
                }
                else if (graphics.screenW <= 568.0)
                {
                    textureResBattlebg.LoadRes("battlebg1_568h.xml", false);
                    textureResBattlebg.LoadRes("battlebg2_568h.xml", false);
                }
                else
                {
                    textureResBattlebg.LoadRes("battlebg1_640hd.xml", true);
                    textureResBattlebg.LoadRes("battlebg2_640hd.xml", true);
                }
            }
            else
            {
                if (graphics.screenW <= 480.0)
                {
                    textureResBattlebg.LoadRes("battlebg1.xml", false);
                    textureResBattlebg.LoadRes("battlebg2.xml", false);
                }
                else
                {
                    textureResBattlebg.LoadRes("battlebg1_568h.xml", false);
                    textureResBattlebg.LoadRes("battlebg2_568h.xml", false);
                }
            }
        }

        if (graphics.widthMode == 3 || g_contenscalefactor != 2.0f)
        {
            // Wide and non-hd modes share the standard flag table; only hd non-wide uses flag_hd.
            textureResFlag.LoadRes("flag.xml", false);
        }
        else
        {
            textureResFlag.LoadRes("flag_hd.xml", true);
        }

        // The effect texture table must be loaded before the effect xmls, which only reference textures and emitters.
        LoadEffectTextureRes("eff.xml");
        LoadEffectRes("effect_gunfire.xml");
        LoadEffectRes("effect_mgunfire.xml");
        LoadEffectRes("effect_tankfire.xml");
        LoadEffectRes("effect_fortfire.xml");
        LoadEffectRes("effect_shipfire.xml");
        LoadEffectRes("effect_exp.xml");
        LoadEffectRes("effect_airstrike.xml");
        LoadEffectRes("effect_parachute.xml");
        LoadEffectRes("effect_nuclearbomb.xml");
        LoadEffectRes("effect_strike1.xml");
        LoadEffectRes("effect_strike2.xml");
        LoadEffectRes("effect_strike3.xml");
        LoadEffectRes("effect_strike4.xml");
        LoadEffectRes("effect_strike5.xml");

        imageMovementText = new ecText();
        // All units share the movement text object, so SetText runs before every draw.
        imageMovementText.Init(g_Num3);
        imageMovementText.SetColor(0xFFFFDF5F);
    }

    /// <summary>Clamps the HSV saturation / value to [0, 1].</summary>
    private static float clamp01(float x)
    {
        if (x < 0.0f) return 0.0f;
        if (x > 1.0f) return 1.0f;
        return x;
    }

    /// <summary>Rounds a normalised colour component and clamps it to an 8 bit value.</summary>
    private static uint to_u8(float x)
    {
        int v = (int)Math.Round(x * 255.0f + 0.5f, MidpointRounding.AwayFromZero);
        if (v < 0) v = 0;
        if (v > 255) v = 255;
        return (uint)v;
    }

    /// <summary>
    /// Converts an HSV colour to the low 24 bits used by the engine (0x00BBGGRR);
    /// callers usually write the alpha into the top 8 bits afterwards.
    /// </summary>
    public static uint HSV2BGR888(float h_deg, float s, float v)
    {
        // Any number of turns is accepted; normalise to the six 60 degree segments first.
        while (h_deg < 0.0f) h_deg += 360.0f;
        while (h_deg >= 360.0f) h_deg -= 360.0f;

        s = clamp01(s);
        v = clamp01(v);

        float c = v * s;
        float h = h_deg / 60.0f;
        float x = c * (1.0f - MathF.Abs((h % 2.0f) - 1.0f));
        float m = v - c;

        float rp = 0, gp = 0, bp = 0;

        // Pick the two interpolated RGB components from the hue segment.
        if (h < 1) { rp = c; gp = x; bp = 0; }
        else if (h < 2) { rp = x; gp = c; bp = 0; }
        else if (h < 3) { rp = 0; gp = c; bp = x; }
        else if (h < 4) { rp = 0; gp = x; bp = c; }
        else if (h < 5) { rp = x; gp = 0; bp = c; }
        else { rp = c; gp = 0; bp = x; }

        uint R = to_u8(rp + m);
        uint G = to_u8(gp + m);
        uint B = to_u8(bp + m);

        return (B << 16) | (G << 8) | R;
    }

    // Hue table of the alliance numbers; the second set lowers the value for the alternate state
    // of the same hue. The fourth entry does not matter because neutral is replaced by grey/white.
    private static readonly float[] colors_h_deg =
        { 195.0f, 112.0f, 359.0f, 332.0f, 305.0f, 288.0f, 262.0f, 230.0f, 168.0f, 140.0f, 82.0f, 54.0f, 26.0f };
    private static readonly float[] colors_s = { 0.55f, 0.34f };
    private static readonly float[] colors_v = { 1.0f, 0.74f };
    private static readonly List<uint> colors_argb = new();

    /// <summary>
    /// Lazily builds the alliance colour cache and returns the colour of an alliance (0xAABBGGRR).
    /// Invalid alliance numbers fall back to grey; NEUTRAL is explicitly white when the cache is built.
    /// </summary>
    public static uint GetAllianceColor(int alliance)
    {
        if (colors_argb.Count == 0)
        {
            // The cache is flattened as 13 bright + 13 dark colours; alliance numbers start at 1.
            for (int i = 0; i < 2; i++)
            {
                foreach (float color_h_deg in colors_h_deg)
                    colors_argb.Add((0xe2u << 24) | HSV2BGR888(color_h_deg * 1.0f, colors_s[i], colors_v[i]));
            }
            colors_argb[(int)NEUTRAL - 1] = 0xe2ffffff;
        }
        if (alliance > 0 && alliance <= colors_argb.Count)
            return colors_argb[alliance - 1];
        return 0xe2cccccc;
    }

    /// <summary>
    /// Renders a map unit: alliance base, country/type art and the transport ship marks of land
    /// units at sea. direction is the +-1 horizontal mirror factor.
    /// </summary>
    public void RenderArmy(string countryName, int alliance, int armyCount, float x, float y,
                           int armyType, uint color, bool sea, float direction)
    {
        ArmyDef army = CObjectDef.Instance().GetArmyDef(armyType, countryName);
        bool isNavy = army != null && army.IsNavy();
        RenderArmyWithNavy(countryName, alliance, armyCount, x, y, armyType, color, sea, isNavy, direction);
    }

    /// <summary>Renders a map unit with the navy flag already resolved by the caller.</summary>
    public void RenderArmyWithNavy(string countryName, int alliance, int armyCount,
                                   float x, float y, int armyType, uint color,
                                   bool sea, bool isNavy, float direction)
    {
        float[] tmp = { 10.0f, 13.0f, 15.0f, 17.0f };
        // The transport ship replacement of land units at sea is decided by the isNavy of the caller.
        ecImage army_image = sea && !isNavy
            ? imageCarrier
            : GetArmyImage(countryName, armyType, false);
        if (army_image != null)
        {
            uint allianceColor = GetAllianceColor(alliance);
            // The grey base is tinted with SetColor to the alliance colour; no per-alliance base sets needed.
            imageUnitbaseGrey[armyCount - 1].SetColor(allianceColor, -1);
            imageUnitbaseGrey[armyCount - 1].Render(x, y);
            army_image.SetColor(color, -1);
            army_image.RenderEx(x, y - tmp[armyCount - 1], 0.0f, direction, 1.0f);
            if (sea && !isNavy)
            {
                // Land units in sea areas show the transport ship and the per-type transport mark.
                if (!imageMarkCarriersNew.ContainsKey(armyType))
                {
                    // Land units without a dedicated transport mark fall back to the infantry mark.
                    armyType = (int)INFANTRY;
                }
                imageMarkCarriersColor[3].SetColor(allianceColor, -1);
                imageMarkCarriersColor[3].Render(x, y - 20.0f);
                if (imageMarkCarriersNew.TryGetValue(armyType, out ecImage carrierMark) && carrierMark != null)
                    carrierMark.Render(x, y - 20.0f);
            }
        }
    }

    /// <summary>
    /// Renders the attacker unit info on the left of the battle box. The attacker faces forward;
    /// AI commanders show the country badge, player commanders the level badge.
    /// </summary>
    public void RenderUIAttackArmy(string CountryName, float x, float y, int ArmyType, int HP, int MaxHP, int Movement, int Cards, int Level, int CommonType, bool AI)
    {
        ecImage ArmyImage = GetArmyImage(CountryName, ArmyType, false);
        if (ArmyImage != null)
        {
            ArmyImage.SetColor(0xFFFFFFFF, -1);
            ArmyImage.RenderEx(x, y, 0.0f, 1.0f, 1.0f);
        }
        imageHpbar.Render(x - 30.0f, y - 12.0f);
        imageHpbarFill.SetColor(HpColor(HP, MaxHP), -1);
        imageHpbarFill.RenderEx(x - 11.0f, y - 4.0f, 0.0f, (float)HP * 33.0f / (float)MaxHP, 1.0f);
        RenderArmyMovementNum(x - 23.0f, y - 7.0f, Movement);
        if ((Cards & (1 << (int)COMMANDER)) != 0)
        {
            // The AI has no local level badge in the battle box; its country/common badge marks the commander.
            if (AI)
                RenderAICommanderMedal(1, x, y + 10.0f, CountryName, CommonType);
            else
                imageCommanderLevel[Level / 3].Render(x + 8.0f, y - 8.0f);
        }
        else if (Level > 0 && Level <= 4)
        {
            imageUnitlevelmark[Level - 1].Render(x + 8.0f, y - 8.0f);
        }
        if (Cards != 0)
        {
            // The attacker cards on the left run to the right.
            for (int i = 0; i < 3; i++)
            {
                if (((Cards >> i) & 1) != 0)
                {
                    imageCardmark[i].Render(x + 36.0f, y - 20.0f);
                    y -= 15.0f;
                }
            }
        }
    }

    /// <summary>
    /// Renders the defender unit info on the right of the battle box. The image is mirrored and
    /// the card marks run to the left, symmetric to the attacker layout.
    /// </summary>
    public void RenderUIDefendArmy(string CountryName, float x, float y, int ArmyType, int HP, int MaxHP, int Movement, int Cards, int Level, int CommonType, bool AI)
    {
        ecImage ArmyImage = GetArmyImage(CountryName, ArmyType, false);
        if (ArmyImage != null)
        {
            ArmyImage.SetColor(0xFFFFFFFF, -1);
            ArmyImage.RenderEx(x, y, 0.0f, -1.0f, 1.0f);
        }
        imageHpbar.Render(x - 30.0f, y - 12.0f);
        imageHpbarFill.SetColor(HpColor(HP, MaxHP), -1);
        imageHpbarFill.RenderEx(x - 11.0f, y - 4.0f, 0.0f, (float)HP * 33.0f / (float)MaxHP, 1.0f);
        RenderArmyMovementNum(x - 23.0f, y - 7.0f, Movement);
        if ((Cards & (1 << (int)COMMANDER)) != 0)
        {
            // Same commander rule as the attacker; only the image and card mirroring differ.
            if (AI)
                RenderAICommanderMedal(1, x, y + 10.0f, CountryName, CommonType);
            else
                imageCommanderLevel[Level / 3].Render(x + 8.0f, y - 8.0f);
        }
        else if (Level > 0 && Level <= 4)
        {
            imageUnitlevelmark[Level - 1].Render(x + 8.0f, y - 8.0f);
        }
        if (Cards != 0)
        {
            // The defender cards on the right run to the left so both sides do not collide in the middle.
            for (int i = 0; i < 3; i++)
            {
                if (((Cards >> i) & 1) != 0)
                {
                    imageCardmark[i].Render(x - 50.0f, y - 20.0f);
                    y -= 15.0f;
                }
            }
        }
    }

    /// <summary>Drops the ecImage references of a map (killAllEcImages); the caller clears the map.</summary>
    private static void killAllEcImages<T>(Dictionary<T, ecImage> map_in)
    {
        foreach (T key in new List<T>(map_in.Keys))
            map_in[key] = null;
    }

    /// <summary>
    /// Releases the images, texture tables and effect resources owned by CGameRes. Every array /
    /// map member is cleared so the instance never keeps dangling resource references.
    /// </summary>
    public void Release()
    {
        armyRenderCache.Clear();
        // Stop every effect still referencing the resources, then release the definitions and textures.
        ecEffectManager.Instance().RemoveAll();
        ReleaseAllEffectRes();
        ReleaseEffectTextureRes();

        // The old per-type maps may still be referenced elsewhere, so clear them in the original order.
        killAllEcImages(imageFlag);
        imageFlag.Clear();

        killAllEcImages(imageSoldier);
        imageSoldier.Clear();

        killAllEcImages(imagePanzer);
        imagePanzer.Clear();

        killAllEcImages(imageCannon);
        imageCannon.Clear();

        killAllEcImages(imageRocketlauncher);
        imageRocketlauncher.Clear();

        killAllEcImages(imageTank);
        imageTank.Clear();

        killAllEcImages(imageHeavytank);
        imageHeavytank.Clear();

        for (int i = 0; i < imageArmyNe.Length; ++i)
            imageArmyNe[i] = null;

        foreach (KeyValuePair<int, Dictionary<string, ecImage>> ecImageListPair in imageArmies)
        {
            // imageArmies is the main two level map: army id to country id to image.
            killAllEcImages(ecImageListPair.Value);
            ecImageListPair.Value.Clear();
        }
        imageArmies.Clear();

        killAllEcImages(imageMarkCarriersNew);
        imageMarkCarriersNew.Clear();

        // The four alliance base arrays are owned by Load; release every entry, not just the arrays.
        for (int i = 0; i < imageUnitbaseGreen.Length; ++i) imageUnitbaseGreen[i] = null;
        for (int i = 0; i < imageUnitbaseBlue.Length; ++i) imageUnitbaseBlue[i] = null;
        for (int i = 0; i < imageUnitbaseGrey.Length; ++i) imageUnitbaseGrey[i] = null;
        for (int i = 0; i < imageUnitbaseRed.Length; ++i) imageUnitbaseRed[i] = null;

        imageCarrier = null;

        for (int i = 0; i < imageCommanderLevel.Length; ++i) imageCommanderLevel[i] = null;
        for (int i = 0; i < imageUnitlevelmark.Length; ++i) imageUnitlevelmark[i] = null;
        imageMedal = null;
        imageMedalLight = null;

        killAllEcImages(imageCountryMedal);
        imageCountryMedal.Clear();
        for (int i = 0; i < imageMarkCarriersColor.Length; ++i) imageMarkCarriersColor[i] = null;

        // HUD resources: health bar, movement marks, card marks and the map target arrows.
        imageHpbar = null;
        imageHpbarFill = null;
        for (int i = 0; i < imageMovementmark.Length; ++i) imageMovementmark[i] = null;
        for (int i = 0; i < imageCardmark.Length; ++i) imageCardmark[i] = null;
        imageArrowGreen = null;
        imageArrowYellow = null;
        imageArrowRed = null;
        imageArrowBlue = null;
        imageArrowshadow = null;

        // Area building and installation icons.
        for (int i = 0; i < imageBuildmarkInstalltion.Length; ++i) imageBuildmarkInstalltion[i] = null;
        for (int i = 0; i < imageBuildmarkCity.Length; ++i) imageBuildmarkCity[i] = null;
        for (int i = 0; i < imageBuildmarkFactory.Length; ++i) imageBuildmarkFactory[i] = null;
        imageBuildmarkPort = null;
        imageBuildmarkAirport = null;

        imageCardShadow = null;
        imageCardCommon = null;
        for (int i = 0; i < imageCardResearch.Length; ++i) imageCardResearch[i] = null;

        // After the directly owned images, release the xml tables and the shared text object.
        textureResCardtex.Release();
        textureResArmy.Release();
        textureResFlag.Release();
        textureResBattlebg.Release();
        imageMovementText = null;
    }

    /// <summary>
    /// Finds the unit art. Land units at sea always return the transport ship; otherwise the
    /// country image wins and others is the fallback. Returns null when the type has no resources.
    /// </summary>
    public ecImage GetArmyImage(string countryName, int armyType, bool sea)
    {
        string countryKey = countryName ?? "";
        if (!armyRenderCache.TryGetValue(armyType, out Dictionary<string, CGameResArmyRenderCacheEntry> cacheByArmy))
        {
            cacheByArmy = new Dictionary<string, CGameResArmyRenderCacheEntry>();
            armyRenderCache[armyType] = cacheByArmy;
        }
        if (cacheByArmy.TryGetValue(countryKey, out CGameResArmyRenderCacheEntry cached) && cached.resolved)
        {
            if (!sea)
                return cached.image;
            ArmyDef cachedArmy = CObjectDef.Instance().GetArmyDef(armyType, countryName);
            return cachedArmy != null && !cachedArmy.IsNavy() ? imageCarrier : cached.image;
        }

        // The definition is queried only the first time a type + country pair shows up; later calls reuse the cache.
        ArmyDef army = CObjectDef.Instance().GetArmyDef(armyType, countryName);
        var entry = new CGameResArmyRenderCacheEntry { image = null, resolved = false };
        if (army != null)
        {
            entry.resolved = true;
            // The definition decides whether the unit is navy; enum ranges alone would break custom types.
            if (imageArmies.TryGetValue(armyType, out Dictionary<string, ecImage> armyCountryRscs))
            {
                if (armyCountryRscs.TryGetValue(countryKey, out ecImage countryImage))
                {
                    entry.image = countryImage;
                }
                else if (armyCountryRscs.TryGetValue("others", out ecImage fallbackImage))
                {
                    // Load guarantees country_list contains others, the shared fallback of missing country art.
                    entry.image = fallbackImage;
                }
            }
        }
        cacheByArmy[countryKey] = entry;
        if (!entry.resolved)
            return null;
        if (!sea)
            return entry.image;
        return army != null && !army.IsNavy() ? imageCarrier : entry.image;
    }

    private static readonly float[] COMMANDER_OFFSET = { 10.0f, 13.0f, 15.0f, 17.0f };

    /// <summary>Draws the level badge of a player commander at the stack base height.</summary>
    public void RenderCommanderMedal(int armyCount, float x, float y, int commanderLevel)
    {
        imageCommanderLevel[commanderLevel / 3].Render(x + 8.0f, y - COMMANDER_OFFSET[armyCount - 1] - 8.0f);
    }

    /// <summary>Draws the port mark at the construction position of an area.</summary>
    public void RenderPort(float x, float y)
    {
        imageBuildmarkPort.Render(x, y);
    }

    /// <summary>Draws the fort, trench, anti-air or radar mark for a non-zero INSTALLATION_TYPE.</summary>
    public void RenderInstallation(int type, float x, float y)
    {
        if (type > 0)
            imageBuildmarkInstalltion[type - 1].Render(x, y);
    }

    /// <summary>Image attribute from the battle background table, or null.</summary>
    public ecImageAttr GetBattleBG(string name)
    {
        return textureResBattlebg.GetImage(name);
    }

    /// <summary>Image attribute from the flag table, for callers needing the raw texture attribute.</summary>
    public ecImageAttr GetFlagImage(string name)
    {
        return textureResFlag.GetImage(name);
    }

    /// <summary>Renders a country flag; silently skips countries without a loaded flag.</summary>
    public void RenderFlag(string countryName, float x, float y)
    {
        string key = countryName ?? "";
        if (imageFlag.TryGetValue(key, out ecImage flag) && flag != null)
            flag.Render(x, y);
    }
}
