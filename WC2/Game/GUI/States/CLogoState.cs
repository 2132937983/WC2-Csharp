namespace WC2;

/// <summary>Start-up logo: shows the logo, waits, then fades to the main menu.</summary>
public sealed class CLogoState : CBaseState
{
    public float fadeInTime;
    public bool fadingIn;
    public bool fadingOut;
    public float fadeOutTime;

    private ecTexture textureLogo;
    private ecImage imageLogo;
    private float shownTime;
    private const float LogoDuration = 2.0f;

    public CLogoState()
    {
        stateId = EState.LOGO;
        exist = true;
    }

    public override void OnEnter()
    {
        GUIManager.Instance().currentState = this;
        GUIMotionManager.Instance().currentState = this;
        var graphics = ecGraphics.Instance();
        string name = graphics.widthMode == 3 ? "logo_iPad.webp" : "logo.webp";
        textureLogo = graphics.LoadTexture(name);
        if (textureLogo != null)
            imageLogo = new ecImage(textureLogo, 0f, 0f, textureLogo.w, textureLogo.h);
        fadeInTime = 0f;
        fadeOutTime = 0f;
        shownTime = 0f;
        fadingIn = true;
        fadingOut = false;
        GUIManager.Instance().FadeIn(0);
    }

    public override void OnExit()
    {
        imageLogo = null;
        if (textureLogo != null)
        {
            ecGraphics.Instance().FreeTexture(textureLogo);
            textureLogo = null;
        }
    }

    public override void Update(float deltaTimeS)
    {
        if (fadingIn || fadingOut) return;
        shownTime += deltaTimeS;
        if (shownTime >= LogoDuration) StartFadeOut();
    }

    private void StartFadeOut()
    {
        if (fadingOut) return;
        fadingOut = true;
        GUIManager.Instance().FadeOut(0, null);
    }

    public override void Render()
    {
        var graphics = ecGraphics.Instance();
        graphics.SetViewPoint(0f, 0f, 1f);
        if (imageLogo == null) return;
        float x = (graphics.screenW - imageLogo.width) * 0.5f;
        float y = (graphics.screenH - imageLogo.height) * 0.5f;
        imageLogo.Render(x, y);
    }

    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.GUI) return false;
        if (evt.info.gui.type == Event.Info.GUIInfo.FADE_IN)
        {
            fadingIn = false;
            return true;
        }
        if (evt.info.gui.type == Event.Info.GUIInfo.FADE_OUT)
        {
            CStateManager.Instance().SetCurState(EState.MENU);
            return true;
        }
        return false;
    }

    public override void TouchEnd(float x, float y, int index)
    {
        if (!fadingIn) StartFadeOut();
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
