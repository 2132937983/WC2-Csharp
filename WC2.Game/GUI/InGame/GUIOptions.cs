namespace WC2;

/// <summary>
/// Options page (GUIOptions.cpp). The layout is read from guioptions.xml: the XML binds the
/// widget attributes to the setting fields, the OK / back behaviour stays in OnEvent.
/// </summary>
public sealed class GUIOptions : GUIElement
{
    /// <summary>Text created from the XML layout.</summary>
    private sealed class OptionsText : GUIElement
    {
        public ecUniFont font;
        public readonly ecText text = new();
        public int align = MIDDLE_ALIGNED;

        public override void OnRender()
        {
            GetAbsRect(out var absoluteRect);
            text.DrawText(absoluteRect.x, absoluteRect.y, align);
        }
    }

    /// <summary>A standalone option container; the caption, decoration and widgets of one option hang here.</summary>
    private sealed class OptionItem : GUIElement
    {
        public string id;
        public int row;
        public int column;
        public float sourceY;
        public float columnWidth;

        public OptionItem(string itemId) { id = itemId; }
    }

    private sealed class OptionItemSpec
    {
        public string id;
        public int row = -1;
        public int column;
        public float sourceY;

        public OptionItemSpec(string itemId) { id = itemId; }
    }

    /// <summary>The XML created texts are owned by this state object; the other widgets by the GUIElement tree.</summary>
    private sealed class GUIOptionsXmlState
    {
        public readonly List<OptionsText> texts = new();
        public readonly List<OptionItem> items = new();
        public readonly List<OptionItemSpec> itemSpecs = new();

        public OptionItemSpec FindSpec(string id)
        {
            foreach (var spec in itemSpecs)
                if (spec.id == id) return spec;
            return null;
        }

        public OptionItem FindItem(string id)
        {
            foreach (var item in items)
                if (item != null && item.id == id) return item;
            return null;
        }
    }

    public GUIButton buttonBack;
    public GUIButton buttonOk;
    public GUIScrollBar scrollMusicVolume;
    public GUIScrollBar scrollSEVolume;
    public GUIButton battleAnimationUncheck;
    public GUIButtonEx battleAnimationCheck;
    public GUILevelSel selSpeed;
    public ecTexture textureCommonbg;
    public ecImage imageCommonbg;
    public ecTexture textureBoardOptions;
    public ecImage imageBoardOptions;
    public float width;
    private GUIOptionsXmlState xmlState;
    public GUIButton newConquestLoadingUncheck;
    public GUIButtonEx newConquestLoadingCheck;
    public bool optionsResLoaded;
    public GUIPagedList pagedList;
    public GUIButton aiHostingUncheck;
    public GUIButtonEx aiHostingCheck;

    public GUIOptions() { }

    // ---- XML helpers (would move to a dedicated class if the whole GUI became XML driven) ----

    private static int ClampOptionLevel(GUILevelSel selector, int level)
    {
        if (selector == null || selector.totalLevel <= 0) return 0;
        return Math.Max(0, Math.Min(level, selector.totalLevel - 1));
    }

    private static float ReadFloat(TiXmlElement element, string name, float fallback)
    {
        if (element != null && element.QueryDoubleAttribute(name, out double value) == TIXML_SUCCESS)
            return (float)value;
        return fallback;
    }

    private static int ReadInt(TiXmlElement element, string name, int fallback)
    {
        if (element != null && element.QueryIntAttribute(name, out int value) == TIXML_SUCCESS)
            return value;
        return fallback;
    }

    private static string ReadString(TiXmlElement element, string name, string fallback)
    {
        string value = element?.Attribute(name);
        return value ?? fallback;
    }

    private static bool IsId(TiXmlElement element, string expected)
    {
        string id = element?.Attribute("id");
        return id != null && id == expected;
    }

    private static string ReadOptionItemId(TiXmlElement element)
    {
        string item = element?.Attribute("item");
        if (!string.IsNullOrEmpty(item)) return item;

        // Compatibility with old layouts without the item attribute: only text widgets with the
        // Text suffix join the option of the same name. Widgets without item (back/ok above all)
        // must stay on the fixed layer and never be taken over by a scrolling item.
        string id = element?.Attribute("id");
        if (string.IsNullOrEmpty(id)) return "";
        const string suffix = "Text";
        if (id.Length > suffix.Length && id.EndsWith(suffix, StringComparison.Ordinal))
            return id.Substring(0, id.Length - suffix.Length);
        return "";
    }

    private static void DefaultOptionPlacement(string id, ref int row, ref int column)
    {
        if (id == "musicVolume") { row = 0; column = 0; }
        else if (id == "soundVolume") { row = 1; column = 0; }
        else if (id == "battleAnimation") { row = 2; column = 0; }
        else if (id == "gameSpeed") { row = 3; column = 0; }
        else if (id == "newConquestLoading") { row = 4; column = 0; }
    }

    /// <summary>strtoul(text, 16) semantics: leading hex digits, optional '#'.</summary>
    private static uint ReadColor(TiXmlElement element, uint fallback)
    {
        string text = element?.Attribute("color");
        if (text == null) return fallback;
        int start = 0;
        if (start < text.Length && text[start] == '#') ++start;
        ulong value = 0;
        int i = start;
        while (i < text.Length && Uri.IsHexDigit(text[i]))
        {
            value = unchecked(value * 16 + (ulong)Convert.ToInt32(text[i].ToString(), 16));
            ++i;
        }
        return i == start ? fallback : unchecked((uint)value);
    }

    private static ecUniFont ReadFont(TiXmlElement element)
    {
        string name = ReadString(element, "font", "fontkt");
        if (name == "font1") return g_Font1;
        if (name == "font2") return g_Font2;
        if (name == "font3") return g_Font3;
        if (name == "font6") return g_Font6;
        if (name == "font7") return g_Font7;
        if (name == "fontkt" || name == "font9") return g_FontKt;
        if (name == "fontht") return g_FontHt;
        // the XML layout defaults to the full font so new Chinese widgets never miss a font
        return g_FontKt;
    }

    private static int ReadAlignment(TiXmlElement element)
    {
        string align = ReadString(element, "align", "center");
        if (align == "left") return LEFT_ALIGNED;
        if (align == "right") return RIGHT_ALIGNED;
        return MIDDLE_ALIGNED;
    }

    /// <summary>
    /// Releases the background and board. The child tree (above all the slider tracks of the
    /// options atlas) is freed first, then the XML atlas is unloaded.
    /// </summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        FreeAllChild();
        buttonBack = null;
        buttonOk = null;
        scrollMusicVolume = null;
        scrollSEVolume = null;
        battleAnimationUncheck = null;
        battleAnimationCheck = null;
        selSpeed = null;
        newConquestLoadingUncheck = null;
        newConquestLoadingCheck = null;
        pagedList = null;
        aiHostingUncheck = null;
        aiHostingCheck = null;

        xmlState = null;

        if (optionsResLoaded)
        {
            s_TextureRes.UnloadRes(ecGraphics.Instance().widthMode == 3 ? "options_ui_hd.xml" : "options_ui.xml");
            optionsResLoaded = false;
        }

        imageCommonbg = null;
        if (textureCommonbg != null)
        {
            ecGraphics.Instance().FreeTexture(textureCommonbg);
            textureCommonbg = null;
        }

        imageBoardOptions = null;
        if (textureBoardOptions != null)
        {
            ecGraphics.Instance().FreeTexture(textureBoardOptions);
            textureBoardOptions = null;
        }
        base.Dispose();
    }

    /// <summary>Builds the options page from guioptions.xml.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;
        width = 0.0f;
        xmlState = new GUIOptionsXmlState();

        ecGraphics graphics = ecGraphics.Instance();
        GUIManager guiManager = GUIManager.Instance();
        bool tablet = graphics.widthMode == 3;
        float originX = tablet ? 0.0f : (graphics.screenW - 480.0f) * 0.5f;
        width = originX;

        // The option items go into the paged content layer; back and OK stay directly on GUIOptions.
        float pageHeight = tablet ? 540.0f : 270.0f;
        float pageY = Math.Max(0.0f, (rect.h - pageHeight) * 0.5f);
        // The page height keeps the device ratio; the paged list shows five items per page.
        pagedList = new GUIPagedList();
        // The options XML uses the full panel width; the page buttons overlay both sides without moving the items.
        pagedList.Init(new GUIRect(0.0f, pageY, rect.w, pageHeight), 5, false);

        AddChild(pagedList, false);

        string commonBackground;
        if (tablet) commonBackground = "commonbg_iPad.png";
        else if (graphics.screenW <= 480) commonBackground = "commonbg.png";
        else if (graphics.screenW <= 568) commonBackground = "commonbg-568h.png";
        else commonBackground = "commonbg-640h.png";

        textureCommonbg = graphics.LoadTexture(commonBackground);
        imageCommonbg = new ecImage(textureCommonbg, 0.0f, 0.0f, rect.w, rect.h);
        // The board only draws the paper and the frame; tracks, check boxes and dividers are XML image widgets.
        textureBoardOptions = graphics.LoadWEBPTexture(tablet ? "options_board_sides@2x.webp" : "options_board_sides.webp");
        imageBoardOptions = new ecImage(textureBoardOptions, 0.0f, 0.0f, tablet ? 1024.0f : 512.0f, tablet ? 530.0f : 279.0f);
        if (!tablet)
        {
            // the side frame board is already phone sized, no clipping offset of the old board_options
            imageBoardOptions.textureRect.refx = 0.0f;
            imageBoardOptions.textureRect.refy = 0.0f;
        }

        // data/ui/options.json (or the original guioptions.xml) as a document of controls
        var document = OptionsLayoutJson.Load();
        TiXmlElement layout = null;
        if (document != null)
        {
            TiXmlNode root = document.FirstChild("guiOptions");
            if (root != null)
                layout = root.FirstChildElement(tablet ? "tablet" : "phone");
        }

        // Without the file no half page is built; the shipped XML is the complete default description.
        if (layout == null) return;

        string[] elementTypes = { "image", "text", "button", "slider", "checkbox", "level" };

        // First scan the XML to build the row info of every option. The options only describe
        // their content; the final vertical position is computed here, so new options never
        // need the whole page re-laid out by hand.
        for (int typeIndex = 0; typeIndex < 6; ++typeIndex)
        {
            string type = elementTypes[typeIndex];
            for (TiXmlElement element = layout.FirstChildElement(type); element != null; element = element.NextSiblingElement(type))
            {
                string itemId = ReadOptionItemId(element);
                if (string.IsNullOrEmpty(itemId)) continue;

                OptionItemSpec spec = xmlState.FindSpec(itemId);
                if (spec == null)
                {
                    spec = new OptionItemSpec(itemId);
                    xmlState.itemSpecs.Add(spec);
                    DefaultOptionPlacement(spec.id, ref spec.row, ref spec.column);
                    spec.row = ReadInt(element, "row", spec.row);
                    spec.column = ReadInt(element, "column", spec.column);
                    spec.sourceY = ReadFloat(element, "y", 0.0f);
                }
                else
                {
                    spec.sourceY = Math.Min(spec.sourceY, ReadFloat(element, "y", 0.0f));
                }
            }
        }

        int nextAutoRow = 5;
        foreach (var spec in xmlState.itemSpecs)
        {
            if (spec.row < 0) spec.row = nextAutoRow++;
        }

        float rowHeight = pageHeight / 5.0f;
        int itemIndex = 0;

        foreach (var spec in xmlState.itemSpecs)
        {
            var item = new OptionItem(spec.id);
            item.column = spec.column;
            item.sourceY = spec.sourceY;
            item.columnWidth = tablet ? 1024.0f : 480.0f;
            int pageRow = itemIndex % 5;
            item.row = pageRow;
            item.rect = new GUIRect(originX + item.column * item.columnWidth, pageRow * rowHeight, item.columnWidth, rowHeight);
            pagedList.RegisterPageItem(item);
            xmlState.items.Add(item);
            ++itemIndex;
        }

        for (int typeIndex = 0; typeIndex < 6; ++typeIndex)
        {
            string type = elementTypes[typeIndex];
            for (TiXmlElement element = layout.FirstChildElement(type); element != null; element = element.NextSiblingElement(type))
            {
                string itemId = ReadOptionItemId(element);
                OptionItem optionItem = xmlState.FindItem(itemId);
                GUIElement optionParent = optionItem != null ? optionItem : pagedList;
                GUIElement buttonParent = optionItem != null ? optionItem : this;
                float rawX = ReadFloat(element, "x", 0.0f);
                float rawY = ReadFloat(element, "y", 0.0f);
                var controlRect = new GUIRect(rawX, rawY, ReadFloat(element, "w", 1.0f), ReadFloat(element, "h", 1.0f));

                if (optionItem != null)
                {
                    controlRect.x -= optionItem.column * optionItem.columnWidth;
                    controlRect.y -= optionItem.sourceY;
                }
                else
                {
                    controlRect.x += originX;
                }

                // The bottom back / OK buttons stick to the screen edges and do not follow the phone centering.
                if (type == "button")
                {
                    if (IsId(element, "back"))
                        controlRect.x = 0.0f;
                    else if (IsId(element, "ok"))
                        controlRect.x = graphics.screenW - controlRect.w;
                }

                if (type == "image")
                {
                    string imageName = ReadString(element, "src", null);
                    if (imageName != null)
                        guiManager.AddImage(imageName, controlRect, optionParent);
                    continue;
                }

                if (type == "text")
                {
                    string imageName = element.Attribute("image");
                    if (imageName != null)
                    {
                        guiManager.AddImage(imageName, controlRect, optionParent);
                        continue;
                    }
                    var item = new OptionsText();
                    ecUniFont font = ReadFont(element);
                    item.font = font;
                    item.text.Init(font);
                    string stringKey = element.Attribute("stringKey");
                    string content = stringKey == null ? ReadString(element, "content", "") : g_StringTable.GetString(stringKey);
                    item.text.SetText(content ?? "");
                    item.text.SetColor(ReadColor(element, 0xFFFFFFFFu));
                    item.align = ReadAlignment(element);
                    item.rect = controlRect;
                    optionParent.AddChild(item, true);
                    xmlState.texts.Add(item);
                    continue;
                }

                if (type == "button")
                {
                    var button = new GUIButton();
                    button.Init(ReadString(element, "normalImage", null), ReadString(element, "pressedImage", null), controlRect, null);
                    string background = element.Attribute("backgroundImage");
                    if (background != null)
                        button.SetBackground(background);
                    buttonParent.AddChild(button, true);
                    if (IsId(element, "back"))
                        buttonBack = button;
                    else if (IsId(element, "ok"))
                        buttonOk = button;
                    continue;
                }

                if (type == "slider")
                {
                    GUIScrollBar slider = guiManager.AddScrollBar(
                        controlRect,
                        optionParent,
                        ReadString(element, "normalImage", "slider.png"),
                        ReadString(element, "pressedImage", "slider.png"),
                        ReadInt(element, "knobX", tablet ? 26 : 13),
                        ReadInt(element, "knobY", tablet ? 42 : 21),
                        ReadInt(element, "initial", 0),
                        ReadInt(element, "multiplier", 100),
                        ReadInt(element, "horizontal", 1));
                    if (IsId(element, "musicVolume"))
                        scrollMusicVolume = slider;
                    else if (IsId(element, "soundVolume"))
                        scrollSEVolume = slider;
                    continue;
                }

                if (type == "checkbox")
                {
                    GUIButton unchecked_ = guiManager.AddButton(null, null, controlRect, optionParent, null);
                    var checked_ = new GUIButtonEx();
                    checked_.Init(null, null, controlRect, null);
                    checked_.SetImageText(
                        ReadString(element, "checkedImage", "buttontext_ok.png"),
                        ReadString(element, "checkedPressedImage", ReadString(element, "checkedImage", "buttontext_ok.png")));
                    checked_.imagePos[0] = ReadFloat(element, "imageX", -6.0f);
                    checked_.imagePos[1] = ReadFloat(element, "imageY", -5.0f);
                    optionParent.AddChild(checked_, true);
                    if (IsId(element, "battleAnimation"))
                    {
                        battleAnimationUncheck = unchecked_;
                        battleAnimationCheck = checked_;
                    }
                    else if (IsId(element, "newConquestLoading"))
                    {
                        newConquestLoadingUncheck = unchecked_;
                        newConquestLoadingCheck = checked_;
                    }
                    else if (IsId(element, "aiHosting"))
                    {
                        aiHostingUncheck = unchecked_;
                        aiHostingCheck = checked_;
                    }
                    continue;
                }

                if (type == "level")
                {
                    var level = new GUILevelSel();
                    level.Init(controlRect, ReadInt(element, "levels", 5));
                    optionParent.AddChild(level, true);
                    if (IsId(element, "gameSpeed"))
                        selSpeed = level;
                }
            }
        }

        // Back / OK are not scrolling content: always pinned to the bottom corners of the full
        // GUIOptions rectangle, unaffected by the phone centering or old tablet XML coordinates.
        if (buttonBack != null)
        {
            buttonBack.rect.x = 0.0f;
            buttonBack.rect.y = rect.h - buttonBack.rect.h;
        }
        if (buttonOk != null)
        {
            buttonOk.rect.x = rect.w - buttonOk.rect.w;
            buttonOk.rect.y = rect.h - buttonOk.rect.h;
        }

        if (scrollMusicVolume != null)
            scrollMusicVolume.SetScrollPos(g_GameSettings.musicVolume);
        if (scrollSEVolume != null)
            scrollSEVolume.SetScrollPos(g_GameSettings.seVolume);
        if (battleAnimationUncheck != null && battleAnimationCheck != null)
        {
            if (g_GameSettings.battleAnimation)
            {
                battleAnimationUncheck.Hide();
                battleAnimationCheck.Show();
            }
            else
            {
                battleAnimationUncheck.Show();
                battleAnimationCheck.Hide();
            }
        }
        if (newConquestLoadingUncheck != null && newConquestLoadingCheck != null)
        {
            if (g_GameSettings.useNewConquestLoading)
            {
                newConquestLoadingUncheck.Hide();
                newConquestLoadingCheck.Show();
            }
            else
            {
                newConquestLoadingUncheck.Show();
                newConquestLoadingCheck.Hide();
            }
        }
        if (aiHostingUncheck != null && aiHostingCheck != null)
        {
            if (g_GameSettings.aiHosting)
            {
                aiHostingUncheck.Hide();
                aiHostingCheck.Show();
            }
            else
            {
                aiHostingUncheck.Show();
                aiHostingCheck.Hide();
            }
        }
        if (selSpeed != null)
            selSpeed.currentLevel = ClampOptionLevel(selSpeed, g_GameSettings.speed);

        // AddImage inserts in native order, so the interactive widgets are explicitly moved above the decoration.
        scrollMusicVolume?.parent?.MoveToFront(scrollMusicVolume);
        scrollSEVolume?.parent?.MoveToFront(scrollSEVolume);
        battleAnimationUncheck?.parent?.MoveToFront(battleAnimationUncheck);
        battleAnimationCheck?.parent?.MoveToFront(battleAnimationCheck);
        newConquestLoadingUncheck?.parent?.MoveToFront(newConquestLoadingUncheck);
        newConquestLoadingCheck?.parent?.MoveToFront(newConquestLoadingCheck);
        aiHostingUncheck?.parent?.MoveToFront(aiHostingUncheck);
        aiHostingCheck?.parent?.MoveToFront(aiHostingCheck);
        selSpeed?.parent?.MoveToFront(selSpeed);

        // Back and OK belong to the fixed layer: rendered after the scrolling layer so they never inherit its clipping.
        if (buttonBack != null) MoveToFront(buttonBack);
        if (buttonOk != null) MoveToFront(buttonOk);
    }

    /// <summary>Back, OK and the check box toggles of the options page.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            // The children (buttons, sliders, level selector inside the paged list) see the touch
            // first; the page itself is modal and swallows it.
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.BACK_PRESSED)
        {
            if ((flags & SHOWN) != 0)
            {
                if (buttonBack == null) return true;
                OnEvent(Event.Button(buttonBack));
                return true;
            }
        }
        else if (evt.type == Event.GUI && evt.info.gui.type == Event.Info.GUIInfo.BUTTON)
        {
            if (ReferenceEquals(evt.info.gui.ptr, buttonOk))
            {
                if (scrollMusicVolume == null || scrollSEVolume == null ||
                    battleAnimationUncheck == null || selSpeed == null ||
                    newConquestLoadingUncheck == null ||
                    aiHostingUncheck == null)
                    return true;
                int musicVolume = scrollMusicVolume.GetScrollPos();
                CCSoundBox.GetInstance().SetMusicVolume(musicVolume);
                g_GameSettings.musicVolume = musicVolume;

                int seVolume = scrollSEVolume.GetScrollPos();
                CCSoundBox.GetInstance().SetSEVolume(seVolume);
                g_GameSettings.seVolume = seVolume;

                g_GameSettings.battleAnimation = (battleAnimationUncheck.flags & SHOWN) == 0;
                g_GameSettings.speed = ClampOptionLevel(selSpeed, selSpeed.currentLevel);
                g_GameSettings.useNewConquestLoading = (newConquestLoadingUncheck.flags & SHOWN) == 0;
                g_GameSettings.aiHosting = (aiHostingUncheck.flags & SHOWN) == 0;
                g_GameSettings.SaveSettings();
            }
            else if (ReferenceEquals(evt.info.gui.ptr, battleAnimationUncheck))
            {
                battleAnimationUncheck.Hide();
                battleAnimationCheck.Show();
            }
            else if (ReferenceEquals(evt.info.gui.ptr, battleAnimationCheck))
            {
                battleAnimationUncheck.Show();
                battleAnimationCheck.Hide();
            }
            else if (ReferenceEquals(evt.info.gui.ptr, newConquestLoadingUncheck))
            {
                newConquestLoadingUncheck.Hide();
                newConquestLoadingCheck.Show();
            }
            else if (ReferenceEquals(evt.info.gui.ptr, newConquestLoadingCheck))
            {
                newConquestLoadingUncheck.Show();
                newConquestLoadingCheck.Hide();
            }
            else if (ReferenceEquals(evt.info.gui.ptr, aiHostingUncheck))
            {
                aiHostingUncheck.Hide();
                aiHostingCheck.Show();
            }
            else if (ReferenceEquals(evt.info.gui.ptr, aiHostingCheck))
            {
                aiHostingUncheck.Show();
                aiHostingCheck.Hide();
            }
        }

        return base.OnEvent(evt);
    }

    /// <summary>Draws the full screen background and the options board.</summary>
    public override void OnRender()
    {
        if (imageCommonbg != null)
            imageCommonbg.Render(0.0f, 0.0f);
        if (imageBoardOptions != null)
        {
            bool tablet = ecGraphics.Instance().widthMode == 3;
            float boardWidth = tablet ? 1024.0f : 480.0f;
            imageBoardOptions.Render(width, 0.0f, boardWidth, rect.h + (tablet ? 64.0f : 32.0f));
        }
    }
}
