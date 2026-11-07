namespace WC2;

// this file holds the conquest selection logic

/// <summary>Conquest country selection screen (gui/menus/conquests/GUISelCountry.cpp).</summary>
public sealed class GUISelCountry : GUIElement
{
    /// <summary>Private preview map (the anonymous namespace ActualConquestMap of the original).</summary>
    public sealed class ActualConquestMap
    {
        public sealed class CountryComponent
        {
            /// <summary>Area ids of this enclave.</summary>
            public List<int> areaIds = new();
            /// <summary>Map area of the enclave, used for flag size and sorting.</summary>
            public long mapArea;
            /// <summary>Representative area of the enclave for the flag and the camera.</summary>
            public int anchorArea = -1;
            /// <summary>Number of units inside the enclave.</summary>
            public int unitCount;
        }

        public sealed class CountryComponents
        {
            /// <summary>Owning country.</summary>
            public CCountry owner;
            /// <summary>Enclaves of the country split by connectivity.</summary>
            public List<CountryComponent> components = new();
        }

        /// <summary>Country flag resource ids created in the current scene.</summary>
        public List<string> flagIds = new();
        /// <summary>Representative area of the largest enclave of every country.</summary>
        public Dictionary<string, int> largestComponentAnchor = new();
        /// <summary>Representative area of the enclave of every area, for clicks and camera focus.</summary>
        public Dictionary<int, int> componentAnchorByArea = new();
        /// <summary>Last selected country id.</summary>
        public string lastSelectedCountryId = "";
        /// <summary>Enclave anchor waiting for the camera focus.</summary>
        public int pendingComponentAnchor = -1;
        /// <summary>Pre-parsed data of the conquest scenario.</summary>
        public BattleScenarioData scenarioData = new();
        /// <summary>Scene used to show the conquest map and compute the area relations.</summary>
        public CScene scene = new();
        /// <summary>Countries created from the scenario.</summary>
        public List<CCountry> countries = new();
        /// <summary>Current positions of the two touches.</summary>
        public float[] lastTouch = new float[4];
        /// <summary>Start positions of the two touches.</summary>
        public float[] touchStart = new float[4];
        /// <summary>Initial distance of the two fingers, for the zoom.</summary>
        public float pinchDistance;
        /// <summary>Whether each of the two touches is active.</summary>
        public bool[] activeTouch = new bool[2];
        /// <summary>Whether the current touch became a drag.</summary>
        public bool moved;
        /// <summary>Map display width.</summary>
        public float mapWidth = 8000f;
        /// <summary>Map display height.</summary>
        public float mapHeight = 3500f;
        // matches the 8000x3500 map CBackground computes from areamark1.raw: 16x7 tiles

        /// <summary>Copy of the cached scenario data (the original copies the struct by value).</summary>
        private static BattleScenarioData CopyScenarioData(BattleScenarioData source)
        {
            var copy = new BattleScenarioData
            {
                fileName = source.fileName,
                mapId = source.mapId,
                areasEnable = source.areasEnable,
            };
            copy.countries.AddRange(source.countries);
            copy.areas.AddRange(source.areas);
            copy.dialogues.AddRange(source.dialogues);
            return copy;
        }

        /// <summary>
        /// Preloads the conquest scenario and reads its map metadata (for the preview).
        /// </summary>
        /// <param name="fileName">scenario XML file name</param>
        /// <param name="mapId">output map id</param>
        /// <param name="areasEnable">output enabled area mask (at most 63 characters like the original buffer)</param>
        /// <returns>whether valid scenario metadata was obtained</returns>
        public bool LoadScenarioMetadata(string fileName, ref int mapId, out string areasEnable)
        {
            areasEnable = "";
            if (!PreloadBattleScenario(fileName)) return false;
            BattleScenarioData cached = GetPreloadedBattleScenario(fileName);
            if (cached == null) return false;
            scenarioData = CopyScenarioData(cached);
            mapId = scenarioData.mapId;
            if (string.IsNullOrEmpty(scenarioData.areasEnable))
                return false;
            areasEnable = scenarioData.areasEnable.Length > 63
                        ? scenarioData.areasEnable.Substring(0, 63)
                        : scenarioData.areasEnable;
            return true;
        }

        /// <summary>Creates the countries from the preloaded data and sets the area ownership.</summary>
        public void LoadOwnershipFromScenarioData()
        {
            foreach (BattleScenarioCountryData data in scenarioData.countries)
            {
                var country = new CCountry();
                country.Init(data.id, string.IsNullOrEmpty(data.name) ? data.id : data.name);
                country.ai = data.ai != 0;
                country.alliance = data.alliance;
                country.flag = data.flag;
                country.r = (byte)data.r;
                country.g = (byte)data.g;
                country.b = (byte)data.b;
                country.a = (byte)data.a;
                countries.Add(country);
            }

            foreach (BattleScenarioAreaData data in scenarioData.areas)
            {
                if (data.id < 0 || data.id >= scene.areaCount || string.IsNullOrEmpty(data.country))
                    continue;
                foreach (CCountry country in countries)
                {
                    if (country.id == data.country)
                    {
                        scene.SetAreaCountry(data.id, country);
                        country.areaIdList.Add(data.id);
                        break;
                    }
                }
            }
        }

        /// <summary>Releases the scene, the countries, the scenario copy and the flag placement data.</summary>
        public void Release()
        {
            scene.Release();
            countries.Clear();
            largestComponentAnchor.Clear();
            componentAnchorByArea.Clear();
            lastSelectedCountryId = "";
            pendingComponentAnchor = -1;
            scenarioData.Clear();
            flagIds.Clear();
        }

        /// <summary>Centers the camera on the given area keeping the current scale.</summary>
        public void FocusArea(int areaId)
        {
            if (areaId < 0 || areaId >= scene.areaCount || scene.areaData == null)
            {
                return;
            }
            AreaData areaData = scene.areaData[areaId];
            float targetX = areaData.AreaPos[0] + areaData.AreaSize[0] * 0.5f;
            float targetY = areaData.AreaPos[1] + areaData.AreaSize[1] * 0.5f;
            scene.camera.SetPosAndScale(targetX, targetY, scene.camera.scale);
        }

        /// <summary>
        /// Splits the territories into enclaves by adjacency and computes the flag positions.
        /// Sea areas take part in the connectivity, but flags are only shown on enclaves with units.
        /// </summary>
        /// <param name="flags">output flag picture list</param>
        public void PlaceFlagsWithComponents(List<GUISelBattle.ImageInfo> flags)
        {
            // clear the enclaves, anchors and pending focus of the previous computation
            largestComponentAnchor.Clear();
            componentAnchorByArea.Clear();
            pendingComponentAnchor = -1;
            // unit count per area, accumulated by the union-find merges below
            var allComponents = new List<CountryComponents>(countries.Count);
            var unitCountByArea = new Dictionary<int, int>();
            foreach (BattleScenarioAreaData data in scenarioData.areas)
            {
                unitCountByArea[data.id] = data.armyTypes.Count;
            }

            // search the enclaves: collect the enabled areas per country and compute its connected components
            foreach (CCountry owner in countries)
            {
                // neutral countries own land too, but the selection map shows no flag for them
                if (owner == null || owner.alliance == (int)NEUTRAL) continue;
                var countryComponents = new CountryComponents();
                countryComponents.owner = owner;
                var areaIds = new List<int>();
                foreach (int areaId in owner.areaIdList)
                {
                    if (areaId < 0 || areaId >= scene.areaCount) continue;
                    CArea area = scene.GetArea(areaId);
                    if (area != null && area.info.enable)
                    {
                        areaIds.Add(areaId);
                    }
                }

                // parent = union-find parents; rootUnitCount = unit total below every root
                int[] parent = new int[areaIds.Count];
                int[] rootUnitCount = new int[areaIds.Count];
                var areaIndex = new Dictionary<int, int>(areaIds.Count);
                for (int i = 0; i < areaIds.Count; ++i)
                    areaIndex[areaIds[i]] = i;
                for (int i = 0; i < parent.Length; ++i)
                {
                    parent[i] = i;
                    if (unitCountByArea.TryGetValue(areaIds[i], out int units))
                        rootUnitCount[i] = units;
                }
                // union-find with path compression
                int findRoot(int value)
                {
                    int root = value;
                    while (parent[root] != root) root = parent[root];
                    while (parent[value] != value)
                    {
                        int next = parent[value];
                        parent[value] = root;
                        value = next;
                    }
                    return root;
                }

                // merge the connected areas of the same country using the CScene adjacency
                for (int i = 0; i < areaIds.Count; ++i)
                {
                    int adjacentCount = scene.GetNumAdjacentAreas(areaIds[i]);
                    for (int adjacentIndex = 0; adjacentIndex < adjacentCount; ++adjacentIndex)
                    {
                        int adjacentId = scene.GetAdjacentAreaID(areaIds[i], adjacentIndex);
                        if (!areaIndex.TryGetValue(adjacentId, out int adjacentListIndex)) continue;
                        int leftRoot = findRoot(i);
                        int rightRoot = findRoot(adjacentListIndex);
                        if (leftRoot != rightRoot)
                        {
                            parent[rightRoot] = leftRoot;
                            // merge the unit counts so the root holds the total of the component
                            rootUnitCount[leftRoot] += rootUnitCount[rightRoot];
                        }
                    }
                }

                // turn the union-find roots into CountryComponent objects
                var roots = new List<int>();
                for (int i = 0; i < areaIds.Count; ++i)
                {
                    int root = findRoot(i);
                    int componentIndex = roots.Count;
                    for (int j = 0; j < roots.Count; ++j)
                    {
                        if (roots[j] == root)
                        {
                            componentIndex = j;
                            break;
                        }
                    }
                    if (componentIndex == roots.Count)
                    {
                        roots.Add(root);
                        countryComponents.components.Add(new CountryComponent());
                    }
                    CountryComponent component = countryComponents.components[componentIndex];
                    component.areaIds.Add(areaIds[i]);
                    AreaData data = scene.areaData[areaIds[i]];
                    component.mapArea += Math.Max(1, data.AreaSize[0]) * (long)Math.Max(1, data.AreaSize[1]);
                    component.unitCount = rootUnitCount[root];
                }

                // pick the most valuable area of every enclave as flag and camera anchor
                foreach (CountryComponent component in countryComponents.components)
                {
                    int bestValue = -1;
                    foreach (int areaId in component.areaIds)
                    {
                        CArea area = scene.GetArea(areaId);
                        int value = CActionAssist.Instance().calcAreaValue(area);
                        if (component.anchorArea < 0 || value > bestValue)
                        {
                            component.anchorArea = areaId;
                            bestValue = value;
                        }
                    }
                    if (component.anchorArea >= 0)
                    {
                        foreach (int areaId in component.areaIds)
                        {
                            componentAnchorByArea[areaId] = component.anchorArea;
                        }
                    }
                }
                // the largest enclave is the default camera target after selecting the country in the side bar
                countryComponents.components.Sort((left, right) => right.mapArea.CompareTo(left.mapArea));
                if (countryComponents.components.Count > 0 &&
                    countryComponents.components[0].anchorArea >= 0)
                {
                    largestComponentAnchor[owner.id] = countryComponents.components[0].anchorArea;
                }
                allComponents.Add(countryComponents);
            }

            // every belligerent gets at least one flag; countries with several components get
            // one extra flag instance per enclave that must be shown
            foreach (CountryComponents countryComponents in allComponents)
            {
                int displayComponentCount = 0;
                for (int componentIndex = 0; componentIndex < countryComponents.components.Count; ++componentIndex)
                {
                    // the homeland (largest component) always keeps its flag; enclaves without units show none
                    if (componentIndex == 0 ||
                        countryComponents.components[componentIndex].unitCount > 0)
                    {
                        ++displayComponentCount;
                    }
                }
                int flagCount = 0;
                foreach (string flagId in flagIds)
                {
                    if (flagId == countryComponents.owner.id)
                    {
                        ++flagCount;
                    }
                }
                while (flagCount < displayComponentCount)
                {
                    var imageInfo = new GUISelBattle.ImageInfo();
                    imageInfo.image = new ecImage(CountryArt.SmallFlag(GUIElement.s_TextureRes,
                        string.IsNullOrEmpty(countryComponents.owner.flag) ? countryComponents.owner.id : countryComponents.owner.flag));
                    imageInfo.scale = Layout.Tablet ? 2f : 4f;
                    flags.Add(imageInfo);
                    flagIds.Add(countryComponents.owner.id);
                    ++flagCount;
                }
            }

            // the globally largest enclave is the common scale reference of every flag
            int largestComponentArea = 0;
            foreach (CountryComponents countryComponents in allComponents)
            {
                foreach (CountryComponent component in countryComponents.components)
                {
                    largestComponentArea = Math.Max(largestComponentArea, component.areaIds.Count);
                }
            }
            // the reference counts at most 20 areas so big countries do not keep growing their flags
            float standardAreaCount = Math.Min(largestComponentArea, 20);

            // match the flags to the enclaves in order and compute scale and position of each flag
            int[] nextComponent = new int[allComponents.Count];
            int flagIndex = 0;
            foreach (GUISelBattle.ImageInfo flag in flags)
            {
                if (flagIndex >= flagIds.Count) break;
                int countryIndex = allComponents.Count;
                for (int i = 0; i < allComponents.Count; ++i)
                {
                    if (allComponents[i].owner.id == flagIds[flagIndex])
                    {
                        countryIndex = i;
                        break;
                    }
                }
                if (countryIndex == allComponents.Count)
                {
                    flag.scale = 0f;
                    ++flagIndex;
                    continue;
                }

                List<CountryComponent> components = allComponents[countryIndex].components;
                while (nextComponent[countryIndex] < components.Count &&
                       nextComponent[countryIndex] > 0 &&
                       components[nextComponent[countryIndex]].unitCount == 0)
                {
                    ++nextComponent[countryIndex];
                }
                if (nextComponent[countryIndex] >= components.Count)
                {
                    flag.scale = 0f;
                    ++flagIndex;
                    continue;
                }

                CountryComponent chosen = components[nextComponent[countryIndex]++];
                // 20 areas are the standard size; bigger enclaves do not grow further,
                // small countries or enclaves keep at least 0.6 of the flag size
                float areaRatio = standardAreaCount > 0f
                                ? Math.Min(chosen.areaIds.Count / standardAreaCount, 1f)
                                : 1f;
                flag.scale *= Math.Max(0.6f, MathF.Sqrt(areaRatio));
                CArea anchor = scene.GetArea(chosen.anchorArea);
                if (anchor != null)
                {
                    flag.x = scene.areaData[anchor.id].AreaPos[0] + scene.areaData[anchor.id].AreaSize[0] * 0.2f;
                    flag.y = scene.areaData[anchor.id].AreaPos[1] + scene.areaData[anchor.id].AreaSize[1] * 0.2f;
                }
                ++flagIndex;
            }
        }
    }

    public GUIButton buttonBack;
    public GUIButton buttonOk;
    public GUICountryList guiCountryList;
    public GUIMedal guiMedal;
    public GUIImage imageBoard;
    public ecTexture[] textureMinimap = new ecTexture[8];
    public ecImage[] imageMinimap = new ecImage[8];
    public ActualConquestMap pActualConquestMap;
    public ecImage imageFlagShadow;
    public List<GUISelBattle.ImageInfo> imageFlag = new();
    public float centerX;
    public float centerY;
    public float w;
    public float h;
    public int conquestId;
    public int xAC;
    public int xB0;
    public int xB4;
    public int xB8;
    public int xBC;
    public int xC0;
    public int xC4;
    public int xC8;
    public int xCC;
    public float flagFadingAlpha;
    public float flagAlpha;
    public float xMinimap;
    public float yMinimap;
    public float xSpeed;
    public float ySpeed;
    public int status;
    public float xBase;
    public GUIButton buttonConquestIntro;
    public GUIButton buttonCountryIntro;
    public GUIConquestIntro guiConquestIntro;
    public GUIConquestCountryIntro guiCountryIntro;
    public bool ui2Loaded;

    /// <summary>
    /// The original only clears the buttons, the minimap arrays, the flag list and the flag
    /// shadow; the children, the board and the state fields are set by Init.
    /// </summary>
    public GUISelCountry()
    {
        ui2Loaded = false;
    }

    /// <summary>Releases the minimap, the flag resources and the atlas reference of the screen.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;

        ReleaseImageList();

        ActualConquestMap actualMap = pActualConquestMap;
        if (actualMap != null)
        {
            actualMap.Release();
            pActualConquestMap = null;
        }

        for (int index = 0; index < 8; ++index)
        {
            imageMinimap[index] = null;
            if (textureMinimap[index] != null)
            {
                ecGraphics.Instance().FreeTexture(textureMinimap[index]);
                textureMinimap[index] = null;
            }
        }

        imageFlagShadow = null;

        bool useHdResources = Layout.Tablet || g_contenscalefactor == 2.0f;
        s_TextureRes.UnloadRes(useHdResources ? "selcountry_hd.xml" : "selcountry.xml");
        // ui2_hd.xml is a shared atlas (slider.png, buttons, check marks): it must stay loaded,
        // otherwise GUIOptions opened afterwards gets empty widget pictures

        base.Dispose();
    }

    public override void OnRender()
    {
        ActualConquestMap actualMap = pActualConquestMap;
        if (actualMap != null)
        {
            // apply the conquest map camera: world coordinates to the current screen viewport
            actualMap.scene.camera.Apply();
            CMapMesh mesh = actualMap.scene.mapMesh;
            if (mesh != null)
            {
                // a mesh map: the areas in their owners' colours and the borders
                var cam = actualMap.scene.camera;
                float hw = cam.screenHalfSize[0] / cam.scale, hh = cam.screenHalfSize[1] / cam.scale;
                mesh.Render(actualMap.scene, cam.centerPos[0] - hw, cam.centerPos[0] + hw, cam.centerPos[1] - hh, cam.centerPos[1] + hh,
                            actualMap.scene.topLeftPos[0] + actualMap.scene.size[0], actualMap.scene.topLeftPos[1] + actualMap.scene.size[1], cam.scale);
            }
            // draw the map background first, then the area color layer
            if (mesh == null && actualMap.scene.background != null)
                actualMap.scene.background.Render(actualMap.scene.camera);
            int areaCount = mesh != null ? 0 : Math.Min(actualMap.scene.areaCount, actualMap.scene.areaList.Count);
            // every enabled area: country color for owned areas, a common translucent color for the sea
            for (int areaId = 0; areaId < areaCount; ++areaId)
            {
                CArea area = actualMap.scene.areaList[areaId];
                ecImage zone = actualMap.scene.zoneImage[areaId];
                if (area == null || zone == null || !area.info.enable) continue;
                uint color = 0x50708080u;
                if (area.country != null)
                {
                    // like CSceneRender::RenderAreasType: r, g, b, a are consecutive bytes used as the color
                    color = area.country.ColorRGBA;
                }
                else if (area.info.sea)
                {
                    color = 0x507090A0u;
                }
                zone.SetColor(color, -1);
                zone.RenderEx(actualMap.scene.areaData[areaId].AreaPos[0],
                              actualMap.scene.areaData[areaId].AreaPos[1],
                              0f, g_contenscalefactor == 1.0f ? 2f : 1f, 0f);
            }

            // find the scene country of the entry selected in the list on the right
            string selectedCountryId = guiCountryList.GetSelCountryID();
            CCountry selectedCountry = null;
            if (selectedCountryId != null)
            {
                foreach (CCountry country in actualMap.countries)
                {
                    if (country.id == selectedCountryId)
                    {
                        selectedCountry = country;
                        break;
                    }
                }
            }
            if (selectedCountry != null && mesh != null)
            {
                // the selected country's areas flashing
                uint c = selectedCountry.ColorRGBA;
                mesh.RenderCountry(selectedCountry, (c & 0x00FFFFFFu) | ((uint)(Math.Clamp(actualMap.scene.flashTimer * 0.8f, 0f, 1f) * 255f) << 24));
            }
            else if (selectedCountry != null)
            {
                // overlay the flashing alpha on every area of the selected country as highlight
                for (int areaId = 0; areaId < areaCount; ++areaId)
                {
                    CArea area = actualMap.scene.areaList[areaId];
                    ecImage zone = actualMap.scene.zoneImage[areaId];
                    if (area == null || zone == null || !area.info.enable ||
                        area.country != selectedCountry) continue;
                    uint color = selectedCountry.ColorRGBA;
                    zone.SetColor(color, -1);
                    zone.SetAlpha(actualMap.scene.flashTimer, -1);
                    zone.RenderEx(actualMap.scene.areaData[areaId].AreaPos[0],
                                  actualMap.scene.areaData[areaId].AreaPos[1],
                                  0f, g_contenscalefactor == 1.0f ? 2f : 1f, 0f);
                }
            }
        }
        else
        {
            ecGraphics.Instance().SetViewPoint(xMinimap, yMinimap, 1f);
            bool wideLayout = Layout.Tablet;
            float minimapSize = wideLayout ? 1000f : 500f;
            float mapWidth = wideLayout ? 3430f : 1715f;
            float imageX = 0f;
            float imageY = 0f;
            foreach (ecImage minimap in imageMinimap)
            {
                if (minimap != null) minimap.Render(imageX, imageY);
                imageX += minimapSize;
                if (imageX >= mapWidth)
                {
                    imageY += minimapSize;
                    imageX = 0f;
                }
            }
        }

        foreach (GUISelBattle.ImageInfo flagInfo in imageFlag)
        {
            if (flagInfo.image == null || flagInfo.scale <= 0f) continue;
            imageFlagShadow.SetAlpha(flagAlpha, -1);
            flagInfo.image.SetAlpha(flagAlpha, -1);
            imageFlagShadow.RenderEx(flagInfo.x, flagInfo.y, flagInfo.rot, flagInfo.scale, 0f);
            flagInfo.image.RenderEx(flagInfo.x, flagInfo.y, flagInfo.rot, flagInfo.scale, 0f);
        }

        // the children keep drawing in screen coordinates; do not leak the map viewport
        ecGraphics.Instance().SetViewPoint(0f, 0f, 1f);
    }

    /// <summary>
    /// Initializes the conquest country selection: the eight minimap textures (or the real map
    /// preview), the top/bottom buttons, the medal widget and the country list. The right side
    /// board shows only a part of the full picture, so texture area and screen area differ.
    /// </summary>
    /// <param name="rectIn">area inside the parent</param>
    /// <param name="ConquestId">conquest scenario index</param>
    public void Init(in GUIRect rectIn, int ConquestId)
    {
        rect = rectIn;
        conquestId = ConquestId;
        centerX = 0f;
        centerY = 0f;
        xBase = 0f;
        status = 0;
        flagFadingAlpha = 1f;
        flagAlpha = 1f;
        xCC = -1;
        pActualConquestMap = null;
        xMinimap = centerX;
        yMinimap = centerY;

        bool wideLayout = Layout.Tablet;
        h = wideLayout ? 1500f : 750f;
        w = wideLayout ? 3430f : 1715f;

        // the wide layout always uses HD resources; the normal layout only at content scale 2
        if (wideLayout)
        {
            s_TextureRes.LoadRes("selcountry_hd.xml", false);
        }
        else if (g_contenscalefactor == 2.0f)
        {
            s_TextureRes.LoadRes("selcountry_hd.xml", true);
        }
        else
        {
            s_TextureRes.LoadRes("selcountry.xml", false);
        }
        // button_intro and the new buttontext_spy live in the ui2_hd atlas; the normal layout
        // scales the coordinates through the hd flag of LoadRes
        ui2Loaded = s_TextureRes.LoadRes("ui2_hd.xml", !wideLayout);

        // the picture is made of 8 ETC textures in 2x4; HD coordinates and margins are doubled
        if (!g_GameSettings.useNewConquestLoading)
        {
            for (int index = 0; index < 8; ++index)
            {
                string minimapName;
                if (wideLayout)
                {
                    minimapName = Sprintf("minimap%d_%d@2x.pkm", 1, index + 1);
                    textureMinimap[index] = ecGraphics.Instance().LoadETCTexture(minimapName);
                    imageMinimap[index] = new ecImage(textureMinimap[index], 12f, 12f, 1000f, 1000f);
                }
                else
                {
                    minimapName = Sprintf("minimap%d_%d.pkm", 1, index + 1);
                    textureMinimap[index] = ecGraphics.Instance().LoadETCTexture(minimapName);
                    imageMinimap[index] = new ecImage(textureMinimap[index], 6f, 6f, 500f, 500f);
                }
            }
        }
        else
        {
            // build the preview map first
            var actualMap = new ActualConquestMap();
            pActualConquestMap = actualMap;
            w = actualMap.mapWidth;
            h = actualMap.mapHeight;

            string scenarioFileName = GetBattleFileName(2, 0, 0, ConquestId);
            int mapId = 1;
            if (!actualMap.LoadScenarioMetadata(scenarioFileName, ref mapId, out string areasEnable))
            {
                // an invalid scenario id must not hand an unloaded scene to CScene; this was the
                // crash path when the 0-based ConquestId produced a missing conquest_0
                pActualConquestMap = null;
                return;
            }
            actualMap.scene.Init(areasEnable, mapId);
            actualMap.mapWidth = actualMap.scene.areaMark.size[0] * 8f;
            actualMap.mapHeight = actualMap.scene.areaMark.size[1] * 8f;
            w = actualMap.mapWidth;
            h = actualMap.mapHeight;
            actualMap.LoadOwnershipFromScenarioData();
            actualMap.scene.camera.SetPosAndScale(actualMap.mapWidth * 0.5f, actualMap.mapHeight * 0.5f, 0.25f);
        }

        imageFlagShadow = new ecImage(s_TextureRes.GetImage("sflag_shadow.png"));

        GUIRect controlRect;
        if (wideLayout)
        {
            buttonBack = new GUIButton();
            controlRect = new GUIRect(0f, 730f, 40f, 38f);
            buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", controlRect, null);
            buttonBack.SetBackground("buttonboard_gray.png");
            AddChild(buttonBack, true);

            // conquest intro and country intro entries, same style as the battle selection [i] button
            buttonConquestIntro = new GUIButton();
            controlRect = new GUIRect(10f, 0f, 62f, 52f);
            buttonConquestIntro.Init("buttontext_intro.png", "buttontext_intro.png", controlRect, null);
            buttonConquestIntro.SetBackground("button_intro.png");
            AddChild(buttonConquestIntro, true);

            buttonCountryIntro = new GUIButton();
            controlRect = new GUIRect(76f, 0f, 62f, 52f);
            buttonCountryIntro.Init("buttontext_spy.png", "buttontext_spy.png", controlRect, null);
            buttonCountryIntro.SetBackground("button_intro.png");
            AddChild(buttonCountryIntro, true);

            buttonOk = new GUIButton();
            controlRect = new GUIRect(984f, 730f, 40f, 38f);
            buttonOk.Init("buttontext_ok.png", "buttontext_ok_press.png", controlRect, null);
            buttonOk.SetBackground("buttonboard_green.png");
            AddChild(buttonOk, true);

            guiMedal = new GUIMedal();
            controlRect = new GUIRect(55f, 718f, 146f, 50f);
            guiMedal.Init(controlRect);
            AddChild(guiMedal, true);

            guiCountryList = new GUICountryList();
            controlRect = new GUIRect(892f, 20f, 142f, 728f);
            guiCountryList.Init(controlRect, conquestId);
            AddChild(guiCountryList, true);

            var boardTextureRect = new ecTextureRect(0f, 0f, 288f, 768f, 0f, 0f);
            var boardRect = new GUIRect(rect.w - 162f, 0f, 162f, 768f);
            imageBoard = GUIManager.Instance().AddImage("board_selbattle_iPad.webp", boardTextureRect, boardRect, this, false);
        }
        else
        {
            xBase = ecGraphics.Instance().screenW - 480f;

            buttonBack = new GUIButton();
            controlRect = new GUIRect(0f, 290f, 32f, 30f);
            buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", controlRect, null);
            buttonBack.SetBackground("buttonboard_gray.png");
            AddChild(buttonBack, true);

            // the normal layout uses the same button resources, the icon stays inside the button area
            buttonConquestIntro = new GUIButton();
            controlRect = new GUIRect(5f, 0f, 31f, 26f);
            buttonConquestIntro.Init("buttontext_intro.png", "buttontext_intro.png", controlRect, null);
            buttonConquestIntro.SetBackground("button_intro.png");
            AddChild(buttonConquestIntro, true);

            buttonCountryIntro = new GUIButton();
            controlRect = new GUIRect(36f, 0f, 31f, 26f);
            buttonCountryIntro.Init("buttontext_spy.png", "buttontext_spy.png", controlRect, null);
            buttonCountryIntro.SetBackground("button_intro.png");
            AddChild(buttonCountryIntro, true);

            buttonOk = new GUIButton();
            controlRect = new GUIRect(xBase + 448f, 290f, 32f, 30f);
            buttonOk.Init("buttontext_ok.png", "buttontext_ok_press.png", controlRect, null);
            buttonOk.SetBackground("buttonboard_green.png");
            AddChild(buttonOk, true);

            guiMedal = new GUIMedal();
            controlRect = new GUIRect(36f, 297f, 70f, 23f);
            guiMedal.Init(controlRect);
            AddChild(guiMedal, true);

            guiCountryList = new GUICountryList();
            controlRect = new GUIRect(xBase + 414f, 10f, 71f, 300f);
            guiCountryList.Init(controlRect, conquestId);
            AddChild(guiCountryList, true);

            var boardTextureRect = new ecTextureRect(0f, 0f, 144f, 320f, 0f, 0f);
            var boardRect = new GUIRect(rect.w - 81f, 0f, 81f, 320f);
            imageBoard = GUIManager.Instance().AddImage("board_selbattle.webp", boardTextureRect, boardRect, this, false);
        }
        // the board is appended, so it stays behind the list

        LoadImageList(conquestId);
        if (pActualConquestMap != null)
        {
            string initialCountryId = guiCountryList.GetSelCountryID();
            if (initialCountryId != null)
            {
                pActualConquestMap.lastSelectedCountryId = initialCountryId;
            }
        }
        xMinimap = centerX;
        yMinimap = centerY;
    }


    /// <summary>Reads the flag marks of the conquest definition and computes the initial minimap viewport.</summary>
    public void LoadImageList(int conquestIdIn)
    {
        string conquestKey = GetConquestKeyName(conquestIdIn);

        ConquestDef conquestDef = CObjectDef.Instance().GetConquestDef(conquestKey);
        if (conquestDef == null)
        {
            return;
        }

        ActualConquestMap actualMap = pActualConquestMap;
        if (actualMap != null)
        {
            // the real map mode uses the belligerent list directly instead of the conquestlist.xml flag coordinates
            foreach (GUICountryItem countryItem in guiCountryList.countryItem)
            {
                CCountry country = null;
                foreach (CCountry candidate in actualMap.countries)
                {
                    if (candidate.id == countryItem.id)
                    {
                        country = candidate;
                        break;
                    }
                }
                if (country == null || country.alliance == (int)NEUTRAL) continue;
                var imageInfo = new GUISelBattle.ImageInfo();
                imageInfo.image = new ecImage(CountryArt.SmallFlag(s_TextureRes, string.IsNullOrEmpty(country.flag) ? countryItem.id : country.flag));
                imageInfo.scale = Layout.Tablet ? 2f : 4f;
                imageFlag.Add(imageInfo);
                actualMap.flagIds.Add(countryItem.id);
            }
        }
        else
        {
            foreach (FlagInfo flagDef in conquestDef.flag)
            {
                // conquestlist.xml may still hold flags of neutral countries; the country list on
                // the right only holds belligerents and is used as the display white list
                bool isBelligerent = false;
                foreach (GUICountryItem countryItem in guiCountryList.countryItem)
                {
                    if (countryItem.id == flagDef.name)
                    {
                        isBelligerent = true;
                        break;
                    }
                }
                if (!isBelligerent) continue;
                ActualConquestMap actualMapInner = pActualConquestMap;
                // the real map shows one flag per country to avoid overlapping enclave flags
                // the original concatenates the string piecewise: sflag_ + definition name + .png
                var imageInfo = new GUISelBattle.ImageInfo();
                imageInfo.image = new ecImage(CountryArt.SmallFlag(s_TextureRes, flagDef.name));
                imageInfo.x = flagDef.x;
                imageInfo.y = flagDef.y;
                imageInfo.rot = flagDef.rot;
                imageInfo.scale = flagDef.scale;

                // the HD minimap uses doubled logical coordinates; rotation and scale stay
                if (Layout.Tablet)
                {
                    imageInfo.x += imageInfo.x;
                    imageInfo.y += imageInfo.y;
                }
                if (actualMapInner != null)
                {
                    imageInfo.x *= actualMapInner.mapWidth / 1715f;
                    imageInfo.y *= actualMapInner.mapHeight / 750f;
                    imageInfo.scale *= (Layout.Tablet ? 2f : 4f);
                }
                imageFlag.Add(imageInfo);
                if (actualMapInner != null)
                {
                    actualMapInner.flagIds.Add(flagDef.name);
                }
            }
        }

        bool wideLayout = Layout.Tablet;
        bool actualMapEnabled = pActualConquestMap != null;
        float mapCenterX = actualMapEnabled
                         ? conquestDef.centerX * (pActualConquestMap.mapWidth / 1715f)
                         : (wideLayout ? conquestDef.centerX * 2f : conquestDef.centerX);
        float mapCenterY = actualMapEnabled
                         ? conquestDef.centerY * (pActualConquestMap.mapHeight / 750f)
                         : (wideLayout ? conquestDef.centerY * 2f : conquestDef.centerY);

        // the definition holds the map center, the viewport coordinates are its top left corner
        if (actualMapEnabled)
        {
            ActualConquestMap map = pActualConquestMap;
            map.scene.camera.SetPosAndScale(mapCenterX, mapCenterY, 0.25f);
            centerX = map.scene.camera.centerPos[0];
            centerY = map.scene.camera.centerPos[1];
        }
        else
        {
            centerX = mapCenterX - ecGraphics.Instance().screenW * 0.5f;
            centerY = mapCenterY - ecGraphics.Instance().screenH * 0.5f;
        }

        // the original clamps both axes independently so the initial camera stays inside the 2x4 map
        if (!actualMapEnabled && centerX < 0f)
        {
            centerX = 0f;
        }
        if (!actualMapEnabled && centerX > w - ecGraphics.Instance().screenW)
        {
            centerX = w - ecGraphics.Instance().screenW;
        }
        if (!actualMapEnabled && centerY < 0f)
        {
            centerY = 0f;
        }
        if (!actualMapEnabled && centerY > h - ecGraphics.Instance().screenH)
        {
            centerY = h - ecGraphics.Instance().screenH;
        }
        if (pActualConquestMap != null)
        {
            pActualConquestMap.PlaceFlagsWithComponents(imageFlag);
        }
    }

    /// <summary>Releases the flag pictures created for the current conquest.</summary>
    public void ReleaseImageList()
    {
        foreach (GUISelBattle.ImageInfo imageInfo in imageFlag)
        {
            imageInfo.image = null;
        }
        imageFlag.Clear();
    }

    /// <summary>
    /// Advances the flag fade and minimap camera state machine of a selection switch
    /// (only triggered when entering / leaving).
    /// </summary>
    public override void OnUpdate(float deltaTimeS)
    {
        ActualConquestMap actualMap = pActualConquestMap;
        if (actualMap != null)
        {
            actualMap.scene.UpdateFlashTimer(deltaTimeS);

            // after selecting a country in the list, focus the most valuable area of its largest
            // connected component; the current scale is kept on purpose
            string selectedCountryId = guiCountryList.GetSelCountryID();
            if (selectedCountryId == null)
            {
                actualMap.lastSelectedCountryId = "";
                actualMap.pendingComponentAnchor = -1;
            }
            else
            {
                int targetArea = actualMap.pendingComponentAnchor;
                actualMap.pendingComponentAnchor = -1;
                if (targetArea < 0 && actualMap.lastSelectedCountryId != selectedCountryId)
                {
                    actualMap.lastSelectedCountryId = selectedCountryId;
                    if (actualMap.largestComponentAnchor.TryGetValue(selectedCountryId, out int anchorArea))
                    {
                        targetArea = anchorArea;
                    }
                }
                else if (targetArea >= 0)
                {
                    actualMap.lastSelectedCountryId = selectedCountryId;
                }
                actualMap.FocusArea(targetArea);
            }
        }

        // 1: fade out the flags; 2: move the camera; 3: fade in the flags; 4: restore the fade channel
        if (status == 1)
        {
            float nextFadeAlpha = flagFadingAlpha - deltaTimeS * 2.5f;
            if (nextFadeAlpha > 0f)
            {
                flagFadingAlpha = nextFadeAlpha;
            }
            else
            {
                flagFadingAlpha = 0f;

                float deltaX = centerX - xMinimap;
                float deltaY = centerY - yMinimap;
                float distanceSquared = deltaX * deltaX + deltaY * deltaY;
                if (distanceSquared <= 2f)
                {
                    // close enough: snap the viewport to the target without a speed
                    xMinimap = centerX;
                    yMinimap = centerY;
                }
                else
                {
                    float distance = MathF.Sqrt(distanceSquared);
                    float moveSpeed = Layout.Tablet ? 2000f : 1000f;
                    xSpeed = deltaX * moveSpeed / distance;
                    ySpeed = deltaY * moveSpeed / distance;
                }
                status = 2;
            }
            // the flags render with flagAlpha; both alphas are synced during the fade out
            flagAlpha = flagFadingAlpha;
            return;
        }

        if (status == 2)
        {
            float targetX = centerX;
            float targetY = centerY;
            if (xMinimap == targetX && yMinimap == targetY)
            {
                status = 3;
                return;
            }

            float nextX = xMinimap + xSpeed * deltaTimeS;
            float nextY = yMinimap + ySpeed * deltaTimeS;
            xMinimap = nextX;
            yMinimap = nextY;

            // each axis detects and clamps the crossing of the target on its own
            if ((xSpeed <= 0f && nextX <= targetX) ||
                (xSpeed > 0f && nextX >= targetX))
            {
                xMinimap = targetX;
            }
            if ((ySpeed <= 0f && nextY <= targetY) ||
                (ySpeed > 0f && nextY >= targetY))
            {
                yMinimap = targetY;
            }
            return;
        }

        if (status == 3)
        {
            float nextAlpha = flagAlpha + deltaTimeS * 2f;
            if (nextAlpha < 1f)
            {
                flagAlpha = nextAlpha;
                return;
            }
            flagAlpha = 1f;
            status = 4;
            return;
        }

        if (status == 4)
        {
            float nextFadeAlpha = flagFadingAlpha + deltaTimeS * 1.5f;
            if (nextFadeAlpha < 1f)
            {
                flagFadingAlpha = nextFadeAlpha;
                return;
            }
            flagFadingAlpha = 1f;
            status = 0;
        }
    }

    /// <summary>
    /// Handles the map touches, the area click selection, the back key and the buttons. In map
    /// mode the left map is dragged / pinch zoomed / clicked here; the country list and the other
    /// children keep the normal GUIElement dispatch.
    /// </summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            // original engine order: the children (country list, buttons) see the touch first
            if (base.OnEvent(evt))
            {
                return true;
            }

            ActualConquestMap actualMap = pActualConquestMap;
            // the real map only covers the left side; the country list on the right keeps its own touches
            if (actualMap != null && evt.info.touch.type != Event.Info.TouchInfo.END)
            {
                int index = evt.info.touch.index & 1;
                float x = evt.info.touch.pos[0];
                float y = evt.info.touch.pos[1];
                float listLeft = Layout.Tablet ? 892f : xBase + 414f;
                // only accept touches inside the map area so they do not overlap the country list
                if (x < listLeft)
                {
                    if (evt.info.touch.type == Event.Info.TouchInfo.BEGIN)
                    {
                        // remember the start and last position to tell click, drag and zoom apart
                        actualMap.activeTouch[index] = true;
                        actualMap.lastTouch[index * 2] = x;
                        actualMap.lastTouch[index * 2 + 1] = y;
                        actualMap.touchStart[index * 2] = x;
                        actualMap.touchStart[index * 2 + 1] = y;
                        actualMap.moved = false;
                        int other = index ^ 1;
                        if (actualMap.activeTouch[other])
                        {
                            // the second finger starts the zoom with the current finger distance
                            float dx = x - actualMap.lastTouch[other * 2];
                            float dy = y - actualMap.lastTouch[other * 2 + 1];
                            actualMap.pinchDistance = MathF.Sqrt(dx * dx + dy * dy);
                        }
                    }
                    else
                    {
                        bool pinching = actualMap.activeTouch[0] && actualMap.activeTouch[1];
                        if (!pinching)
                        {
                            // a single finger drags the map by its displacement
                            float dx = x - actualMap.lastTouch[index * 2];
                            float dy = y - actualMap.lastTouch[index * 2 + 1];
                            actualMap.scene.camera.Move(-dx, -dy, true);
                        }
                        actualMap.lastTouch[index * 2] = x;
                        actualMap.lastTouch[index * 2 + 1] = y;
                        if (MathF.Abs(x - actualMap.touchStart[index * 2]) > 4f ||
                            MathF.Abs(y - actualMap.touchStart[index * 2 + 1]) > 4f)
                            actualMap.moved = true;
                        if (pinching)
                        {
                            // two fingers: no drag, zoom around the middle point instead
                            int other = index ^ 1;
                            float ox = actualMap.lastTouch[other * 2];
                            float oy = actualMap.lastTouch[other * 2 + 1];
                            float distance = MathF.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy));
                            if (actualMap.pinchDistance > 1f && distance > 1f)
                            {
                                float oldScale = actualMap.scene.camera.scale;
                                float midX = (x + ox) * 0.5f;
                                float midY = (y + oy) * 0.5f;
                                // convert the middle point to world coordinates at the old scale and keep it fixed
                                float worldX = actualMap.scene.camera.centerPos[0] -
                                    (ecGraphics.Instance().screenW * 0.5f - midX) / oldScale;
                                float worldY = actualMap.scene.camera.centerPos[1] -
                                    (ecGraphics.Instance().screenH * 0.5f - midY) / oldScale;
                                PinchZoomResult zoomResult = default;
                                if (CalculatePinchZoom(
                                        actualMap.scene.camera.centerPos[0],
                                        actualMap.scene.camera.centerPos[1],
                                        oldScale, worldX, worldY,
                                        actualMap.pinchDistance, distance,
                                        0.05f, 0f, ref zoomResult))
                                {
                                    actualMap.scene.camera.SetPosAndScale(
                                            zoomResult.centerX, zoomResult.centerY, zoomResult.scale);
                                }
                            }
                            actualMap.pinchDistance = distance;
                        }
                    }
                }
            }
            else if (actualMap != null && evt.info.touch.type == Event.Info.TouchInfo.END)
            {
                // release the touch state first; only an undragged single finger counts as a click
                int index = evt.info.touch.index & 1;
                actualMap.activeTouch[index] = false;
                actualMap.pinchDistance = 0f;
                float listLeft = Layout.Tablet ? 892f : xBase + 414f;
                // the end of a drag or zoom gesture never selects a country
                if (!actualMap.moved && evt.info.touch.pos[0] < listLeft)
                {
                    // no flag hit test any more: the scene areaMark maps the screen position to
                    // the clicked area and its real owner is used
                    int areaId = actualMap.scene.ScreenToAreaID(evt.info.touch.pos[0], evt.info.touch.pos[1]);
                    CArea area = (areaId >= 0 && areaId < actualMap.scene.areaCount)
                               ? actualMap.scene.GetArea(areaId)
                               : null;
                    // only enabled areas with an owner are selectable
                    if (area != null && area.info.enable && area.country != null)
                    {
                        // remember the enclave anchor of the clicked area for the camera focus
                        string id = area.country.id;
                        if (actualMap.componentAnchorByArea.TryGetValue(areaId, out int anchor))
                        {
                            actualMap.pendingComponentAnchor = anchor;
                        }
                        for (int itemIndex = 0; itemIndex < guiCountryList.countryItem.Count; ++itemIndex)
                        {
                            if (id == guiCountryList.countryItem[itemIndex].id)
                            {
                                // select the country and scroll the list on the right to its entry
                                guiCountryList.SelectAndEnsureVisible(itemIndex);
                                break;
                            }
                        }
                    }
                }
            }
            return true;
        }

        if (evt.type == Event.BACK_PRESSED)
        {
            if (base.OnEvent(evt))
                return true;
            if ((flags & SHOWN) != 0)
            {
                // same GUI event path as a click on the back button
                OnEvent(Event.Button(buttonBack));
                return true;
            }
            return false;
        }
        else if (evt.type == Event.GUI &&
                 evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
                 evt.info.gui.ptr == buttonConquestIntro)
        {
            bool wideLayout = Layout.Tablet;
            GUIRect introRect = wideLayout
                    ? new GUIRect(32f, 32f, 960f, 640f)
                    : new GUIRect((ecGraphics.Instance().screenW - 480f) * 0.5f, 0f, 480f, 320f);
            if (guiConquestIntro == null)
            {
                guiConquestIntro = new GUIConquestIntro();
                guiConquestIntro.Init(introRect, conquestId);
                AddChild(guiConquestIntro, false);
            }
            else
            {
                guiConquestIntro.SetConquest(conquestId);
                guiConquestIntro.Show();
            }
            guiConquestIntro.Center();
        }
        else if (evt.type == Event.GUI &&
                 evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
                 evt.info.gui.ptr == buttonCountryIntro)
        {
            bool wideLayout = Layout.Tablet;
            GUIRect introRect = wideLayout
                    ? new GUIRect(32f, 32f, 960f, 640f)
                    : new GUIRect((ecGraphics.Instance().screenW - 480f) * 0.5f, 0f, 480f, 320f);
            string selectedId = guiCountryList.GetSelCountryID();
            string selectedName = selectedId;
            foreach (GUICountryItem item in guiCountryList.countryItem)
            {
                if (selectedId != null && item.id == selectedId)
                {
                    selectedName = item.name;
                    break;
                }
            }
            if (guiCountryIntro == null)
            {
                guiCountryIntro = new GUIConquestCountryIntro();
                guiCountryIntro.Init(introRect, selectedName, selectedId, conquestId);
                AddChild(guiCountryIntro, false);
            }
            else
            {
                guiCountryIntro.SetCountry(selectedName, selectedId, conquestId);
                guiCountryIntro.Show();
            }
            guiCountryIntro.Center();
        }
        else if (evt.type == Event.GUI &&
                 evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
                 evt.info.gui.ptr == buttonOk)
        {
            // the original switches to the conquest game first, then stores the player country id from the list
            g_GameManager.NewGame((int)CGameManager.CONQUEST, -1, 0, conquestId);
            g_GameManager.SetConquestPlayerCountryID(guiCountryList.GetSelCountryID());
        }
        else if (evt.type == Event.GUI &&
                 evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
                 evt.info.gui.ptr == buttonBack)
        {
            // going back never enters the battle, the preloaded XML can be released right away
            ReleasePreloadedBattleScenario();
        }

        return base.OnEvent(evt);
    }
}
