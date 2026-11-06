namespace WC2;

/// <summary>
/// Start-up logo, ported from the original binary (CLogoState in Reference/decompiled): a
/// centered logo picture on a white screen, applies the saved volumes, holds 2.1 s, fades out,
/// waits one more second and switches to the main menu.
/// </summary>
public sealed class CLogoState : CBaseState
{
    public float fadeInTime;
    public bool fadingIn;
    public bool fadingOut;
    public float fadeOutTime;

    public CLogoState()
    {
        stateId = EState.LOGO;
        exist = true;
    }

    public override void OnEnter()
    {
        var guiManager = GUIManager.Instance();
        guiManager.currentState = this;
        GUIMotionManager.Instance().currentState = this;
        var graphics = ecGraphics.Instance();
        GUIImage logo;
        if (graphics.widthMode == 3)
            logo = guiManager.AddImage("logo_iPad.webp", new ecTextureRect(1f, 1f, 510f, 510f, 0f, 0f), new GUIRect(0f, 0f, 510f, 510f), guiManager, false);
        else
            logo = guiManager.AddImage("logo.webp", new ecTextureRect(1f, 1f, 254f, 254f, 0f, 0f), new GUIRect(0f, 0f, 254f, 254f), guiManager, false);
        logo?.Center();
        // the original (re)applies the saved volumes here
        CCSoundBox.GetInstance().SetMusicVolume(g_GameSettings.musicVolume);
        CCSoundBox.GetInstance().SetSEVolume(g_GameSettings.seVolume);
        fadeInTime = 0f;
        fadingIn = true;
        fadingOut = false;
        fadeOutTime = 0f;
    }

    public override void OnExit()
    {
        GUIManager.Instance().FreeAllChild();
    }

    public override void Update(float deltaTimeS)
    {
        if (fadeInTime >= 0f) fadeInTime += deltaTimeS;
        if (fadeInTime > 2.1f && fadingIn)
        {
            GUIManager.Instance().FadeOut(-1, null);
            fadeInTime = -1f;
        }
        if (fadingOut)
        {
            fadeOutTime += deltaTimeS;
            if (fadeOutTime > 1f)
            {
                g_PlayerManager.Init();
                CStateManager.Instance().SetCurState(EState.MENU);
                fadingOut = false;
            }
        }
    }

    public override void Render()
    {
        var graphics = ecGraphics.Instance();
        graphics.SetViewPoint(0f, 0f, 1f);
        graphics.RenderRect(0f, 0f, graphics.screenW, graphics.screenH, 0xFFFFFFFFu);
    }

    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.GUI) return false;
        if (evt.info.gui.type == Event.Info.GUIInfo.FADE_OUT)
        {
            fadingOut = true;
            return true;
        }
        if (evt.info.gui.type == Event.Info.GUIInfo.FADE_IN) fadingIn = true;
        return true;
    }

    public override bool BackPressed() => true;

    public override void EnterForeground() { }
}

/// <summary>Online match state; multiplayer is not part of the port, so it returns to the menu.</summary>
public sealed class CMatchState : CBaseState
{
    public CMatchState()
    {
        stateId = EState.MATCH;
        exist = true;
    }

    public override void OnEnter()
    {
        GUIManager.Instance().currentState = this;
        CStateManager.Instance().SetCurState(EState.MENU);
    }

    public override void Update(float deltaTimeS) { }
    public override void Render() { }
    public override bool OnEvent(Event evt) => false;
    public void StartMultiplayGame(int levelId) { }
    public void ShowWarning(int warning) { }
}

/// <summary>Map editor state of the original tool chain; not available in the port.</summary>
public sealed class CEditState : CBaseState
{
    public readonly CTouchInertia touchInertia = new();
    public int renderMode;
    public int mapId = 1;

    public CEditState()
    {
        stateId = EState.EDIT;
        exist = true;
    }

    public override void OnEnter()
    {
        GUIManager.Instance().currentState = this;
        CStateManager.Instance().SetCurState(EState.MENU);
    }

    public override void Update(float deltaTimeS) { }
    public override void Render() { }
    public override bool OnEvent(Event evt) => false;
    public void InitGame() { }
    public void ReleaseGame() { }
}
