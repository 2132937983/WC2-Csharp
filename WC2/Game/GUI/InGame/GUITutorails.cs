using System.Globalization;

namespace WC2;

/// <summary>
/// Scripted tutorial overlay, ported from the original binary (`GUITutorails` in
/// Reference/decompiled/x86_64/GUITutorails.c). The C++ remake has no source for it and runs
/// the original code through the binder. A script XML (`tutorails_script*.xml`, chosen by the
/// screen width) drives a finger pointer, a dark mask with a highlighted rectangle, a guide
/// character with a dialog board, optional pictures and the in-game selection/camera. The
/// overlay covers the whole screen and swallows touches; a WAIT command ends with a tap.
/// </summary>
public sealed class GUITutorails : GUIElement
{
    public const int SEL_AREA = 0, UNSEL_AREA = 1, MOVE_TO_AREA = 2, FINGER_AT = 3, FINGER_TO = 4, HIDE_FINGER = 5,
        SHOW_TEXT = 6, DRAW_RECT = 7, CLEAR_RECT = 8, WAIT = 9, SHOW_IMAGE = 10, HIDE_IMAGE = 11, EXIT = 12;

    public sealed class TutorialCmd
    {
        public int type;
        public int id;
        public float x, y, w, h;
        public string str = "";
    }

    public readonly List<TutorialCmd> cmd = new();
    public readonly ecUniFont font8 = new();
    public readonly ecText textDlg = new();
    public ecTexture textureGuider, textureHand, textureDlgBoard, textureTutorials;
    public ecImage imageGuider, imageHand, imageDlgBoard, imageTutorials;
    public float xTutorials, yTutorials;
    public float xHand, yHand, xHandTarget, yHandTarget, xHandSpeed, yHandSpeed;
    public float xRect, yRect, wRect, hRect;
    public bool showHand, drawRect, executingCmd;
    public int cmdIndex;

    public GUITutorails() { }

    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;
        var graphics = ecGraphics.Instance();
        textureGuider = graphics.LoadWEBPTexture("guider.webp");
        if (textureGuider != null) imageGuider = new ecImage(textureGuider, 0f, 0f, 210f, 246f);
        textureHand = graphics.LoadTexture("hand.png");
        if (textureHand != null) imageHand = new ecImage(textureHand, new ecTextureRect(1f, 1f, 44f, 80f, 27f, 6f));
        textureDlgBoard = graphics.LoadTexture("dlgboard.png");
        if (textureDlgBoard != null) imageDlgBoard = new ecImage(textureDlgBoard, 0f, 0f, 402f, 87f);

        string language = g_LocalizableStrings.GetString("language");
        if (g_contenscalefactor == 2.0f) font8.Init(Sprintf("font8_%s_hd.fnt", language), true);
        else font8.Init(Sprintf("font8_%s.fnt", language), false);
        textDlg.Init(font8.IsLoaded ? font8 : FontGlobals.DefaultTextFont());
        textDlg.SetColor(0xFF00FCFFu);
        showHand = false;
        drawRect = false;
        LoadScript();
        cmdIndex = 0;
        executingCmd = false;
    }

    private static float Attr(TiXmlElement e, string name, float fallback)
    {
        string s = e?.Attribute(name);
        return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }

    /// <summary>Loads the script that matches the layout width (original file selection).</summary>
    public void LoadScript()
    {
        ReleaseScript();
        var graphics = ecGraphics.Instance();
        string file;
        if (graphics.widthMode == 3) file = "tutorails_script_iPad.xml";
        else if (graphics.screenW <= 568)
        {
            if (graphics.screenW > 534) file = "tutorails_script-568h.xml";
            else if (graphics.screenW == 534) file = "tutorails_script-534h.xml";
            else if (graphics.screenW == 512) file = "tutorails_script-512h.xml";
            else file = "tutorails_script.xml";
        }
        else file = "tutorails_script-640h.xml";

        var doc = new TiXmlDocument(GetPath(file));
        if (!doc.LoadFile()) { Log("Tutorial script missing: " + file); return; }
        var script = doc.FirstChildElement("script");
        if (script == null) return;
        for (var e = script.FirstChildElement(); e != null; e = e.NextSiblingElement())
        {
            var c = new TutorialCmd();
            switch (e.Attribute("name") ?? "")
            {
                case "sel area": c.type = SEL_AREA; break;
                case "unsel area": c.type = UNSEL_AREA; break;
                case "moveto area": c.type = MOVE_TO_AREA; break;
                case "finger at": c.type = FINGER_AT; break;
                case "finger to": c.type = FINGER_TO; break;
                case "hide finger": c.type = HIDE_FINGER; break;
                case "show text": c.type = SHOW_TEXT; break;
                case "draw rect": c.type = DRAW_RECT; break;
                case "clear rect": c.type = CLEAR_RECT; break;
                case "wait": c.type = WAIT; break;
                case "show image": c.type = SHOW_IMAGE; break;
                case "hide image": c.type = HIDE_IMAGE; break;
                case "exit": c.type = EXIT; break;
                default: c.type = SEL_AREA; break; // the original leaves the zero-initialised type
            }
            c.id = e.QueryIntAttribute("id", out int id) == 0 ? id : 0;
            c.x = Attr(e, "x", 0f);
            c.y = Attr(e, "y", 0f);
            c.w = Attr(e, "w", 0f);
            c.h = Attr(e, "h", 0f);
            c.str = e.Attribute("string") ?? "";
            cmd.Add(c);
        }
    }

    public void ReleaseScript() => cmd.Clear();

    public void ShowDlg(int dlgTutorailsIndex) => ShowDlgText(g_StringTable.GetString(Sprintf("tutorails %d", dlgTutorailsIndex)));

    public void ShowDlg(string dlgKey) => ShowDlgText(g_StringTable.GetString(dlgKey));

    private void ShowDlgText(string text)
    {
        textDlg.SetText(text);
        Show();
        CCSoundBox.GetInstance().PlaySE("btn.wav");
    }

    public void HideDlg() => Hide();

    private void ReleaseTutorialImage()
    {
        imageTutorials = null;
        if (textureTutorials != null)
        {
            ecGraphics.Instance().FreeTexture(textureTutorials);
            textureTutorials = null;
        }
    }

    public void ExeCmd(int index)
    {
        var c = cmd[index];
        switch (c.type)
        {
            case SEL_AREA:
                (CStateManager.Instance().GetStatePtr(GAME) as CGameState)?.SelectArea(c.id);
                executingCmd = false;
                return;
            case UNSEL_AREA:
                (CStateManager.Instance().GetStatePtr(GAME) as CGameState)?.UnselectArea();
                executingCmd = false;
                return;
            case MOVE_TO_AREA:
                if (ecGraphics.Instance().widthMode == 3) g_Scene.MoveCameraToArea(c.id);
                else g_Scene.MoveCameraCenterToArea(c.id);
                executingCmd = true;
                return;
            case FINGER_AT:
                xHand = c.x; yHand = c.y;
                showHand = true;
                executingCmd = false;
                return;
            case FINGER_TO:
                xHandTarget = c.x; yHandTarget = c.y;
                if (MathF.Abs(xHand - c.x) <= 1f) { xHand = c.x; xHandSpeed = 0f; }
                else xHandSpeed = c.x - xHand;
                if (MathF.Abs(yHand - c.y) <= 1f) { yHand = c.y; yHandSpeed = 0f; }
                else yHandSpeed = c.y - yHand;
                executingCmd = true;
                return;
            case HIDE_FINGER:
                showHand = false;
                executingCmd = false;
                return;
            case SHOW_TEXT:
                if (c.id <= 0) ShowDlg(c.str); else ShowDlg(c.id);
                executingCmd = false;
                return;
            case DRAW_RECT:
                // the original stores the script's x/y/w/h swapped (y <-> x, h <-> w) and renders
                // the mask with the same swap, so the highlighted rectangle is the scripted one
                yRect = c.x; xRect = c.y; hRect = c.w; wRect = c.h;
                drawRect = true;
                executingCmd = false;
                return;
            case CLEAR_RECT:
                drawRect = false;
                executingCmd = false;
                return;
            case WAIT:
                executingCmd = true;
                return;
            case SHOW_IMAGE:
                ReleaseTutorialImage();
                textureTutorials = ecGraphics.Instance().LoadWEBPTexture(Sprintf("tutorails%d.webp", c.id));
                if (textureTutorials != null) imageTutorials = new ecImage(textureTutorials, 0f, 0f, c.w, c.h);
                xTutorials = c.x; yTutorials = c.y;
                executingCmd = false;
                return;
            case HIDE_IMAGE:
                ReleaseTutorialImage();
                executingCmd = false;
                return;
            case EXIT:
                GUIManager.Instance().FadeOut(9, null);
                executingCmd = true;
                return;
        }
    }

    public override void OnUpdate(float deltaTimeS)
    {
        if (cmdIndex >= cmd.Count) return;
        if (!executingCmd)
        {
            // run commands until one needs time (camera, finger, wait, exit)
            while (cmdIndex < cmd.Count)
            {
                ExeCmd(cmdIndex);
                if (executingCmd) break;
                cmdIndex++;
            }
            return;
        }
        int type = cmd[cmdIndex].type;
        if (type == MOVE_TO_AREA)
        {
            if (!g_Scene.IsMoving()) { executingCmd = false; cmdIndex++; }
            return;
        }
        if (type != FINGER_TO) return;
        if (xHandSpeed == 0f && yHandSpeed == 0f) { executingCmd = false; cmdIndex++; return; }
        float dx = xHandSpeed * deltaTimeS, dy = yHandSpeed * deltaTimeS;
        xHand += dx; yHand += dy;
        if (MathF.Abs(dx) > MathF.Abs(xHand - xHandTarget)) { xHand = xHandTarget; xHandSpeed = 0f; }
        if (MathF.Abs(dy) > MathF.Abs(yHand - yHandTarget)) { yHand = yHandTarget; yHandSpeed = 0f; }
        if (xHandSpeed == 0f && yHandSpeed == 0f) { executingCmd = false; cmdIndex++; }
    }

    public override void OnRender()
    {
        GetAbsRect(out var abs);
        var graphics = ecGraphics.Instance();
        imageTutorials?.Render(xTutorials, yTutorials);
        if (drawRect)
        {
            // four dark bands around the highlighted rectangle (same swapped fields as the original)
            float screenW = graphics.screenW, screenH = graphics.screenH;
            graphics.RenderRect(0f, 0f, screenW, xRect, 0x80000000u);
            graphics.RenderRect(0f, xRect + wRect, screenW, screenH - xRect - wRect, 0x80000000u);
            graphics.RenderRect(0f, xRect, yRect, wRect, 0x80000000u);
            graphics.RenderRect(yRect + hRect, xRect, screenW - yRect - hRect, wRect, 0x80000000u);
        }
        if (graphics.widthMode == 3)
        {
            imageGuider?.Render(abs.x + 542f, abs.y + 522f);
            imageDlgBoard?.Render(abs.x + 311f, abs.y + 682f);
            textDlg.SetColor(0xFFFFFFFFu);
            textDlg.DrawText(abs.x + 325f, abs.y + 695f, TextAlign.LEFT_ALIGNED);
        }
        else
        {
            float offset = (graphics.screenW - 480f) * 0.5f;
            imageGuider?.Render(abs.x + 270f + offset, abs.y + 74f);
            imageDlgBoard?.Render(abs.x + 39f + offset, abs.y + 234f);
            textDlg.SetColor(0xFFFFFFFFu);
            textDlg.DrawText(abs.x + 54f + offset, abs.y + 248f, TextAlign.LEFT_ALIGNED);
        }
        if (showHand && imageHand != null) imageHand.Render(xHand, yHand);
    }

    /// <summary>Swallows every touch; a touch end finishes a pending WAIT command.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH) return base.OnEvent(evt);
        if (evt.info.touch.type == Event.Info.TouchInfo.END && executingCmd)
        {
            if (cmdIndex < cmd.Count && cmd[cmdIndex].type == WAIT)
            {
                executingCmd = false;
                cmdIndex++;
            }
        }
        return true;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        base.Dispose();
        ReleaseScript();
        ReleaseTutorialImage();
        var graphics = ecGraphics.Instance();
        if (textureGuider != null) { graphics.FreeTexture(textureGuider); textureGuider = null; }
        if (textureHand != null) { graphics.FreeTexture(textureHand); textureHand = null; }
        if (textureDlgBoard != null) { graphics.FreeTexture(textureDlgBoard); textureDlgBoard = null; }
        imageGuider = imageHand = imageDlgBoard = null;
        font8.Release();
    }
}
