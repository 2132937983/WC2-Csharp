namespace WC2;

/// <summary>Loading screen state (CLoadState.cpp): shows the title, a random tip and initializes the game.</summary>
public sealed class CLoadState : CBaseState
{
    private const float kLoadingTipTextScale = 0.65f;

    public int waitedAfterGameInited;
    public bool gameInited;
    public ecTexture textureLoading;
    public ecImage imageLoading;
    public ecTexture textureLoadingTitle;
    public ecImage imageLoadingTitle;
    public readonly ecText textTip = new();

    /// <summary>
    /// waitedAfterGameInited and gameInited are set when the loading flow is entered, like the
    /// original; the four resource references start empty so OnExit never frees garbage.
    /// </summary>
    public CLoadState()
    {
        stateId = LOAD;
        exist = true;
    }

    /// <summary>Releases the images and textures created when the state was entered.</summary>
    public override void OnExit()
    {
        imageLoading = null;
        if (textureLoading != null)
        {
            ecGraphics.Instance().FreeTexture(textureLoading);
            textureLoading = null;
        }

        imageLoadingTitle = null;
        if (textureLoadingTitle != null)
        {
            ecGraphics.Instance().FreeTexture(textureLoadingTitle);
            textureLoadingTitle = null;
        }
    }

    /// <summary>Registers the event receiver and creates the resources of this loading screen.</summary>
    public override void OnEnter()
    {
        IEventReceiver receiver = this;
        GUIManager.Instance().currentState = receiver;
        GUIMotionManager.Instance().currentState = receiver;

        ecGraphics graphics = ecGraphics.Instance();
        bool wideLayout = graphics.widthMode == 3;
        string language = g_LocalizableStrings.GetString("language");
        bool chinese = language != null && language == "cn";

        if (wideLayout)
        {
            textureLoading = graphics.LoadTexture(chinese ? "loading_cn@2x.png" : "loading@2x.png");
            imageLoading = new ecImage(textureLoading, 0.0f, 0.0f, 156.0f, 22.0f);

            textureLoadingTitle = graphics.LoadTexture(chinese ? "loading_title_cn@2x.png" : "loading_title@2x.png");
            imageLoadingTitle = new ecImage(textureLoadingTitle, 0.0f, 0.0f, 428.0f, 118.0f);
        }
        else
        {
            textureLoading = graphics.LoadTexture(chinese ? "loading_cn.png" : "loading.png");
            imageLoading = new ecImage(textureLoading, 0.0f, 0.0f, 78.0f, 11.0f);

            textureLoadingTitle = graphics.LoadTexture(chinese ? "loading_title_cn.png" : "loading_title.png");
            imageLoadingTitle = new ecImage(textureLoadingTitle, 0.0f, 0.0f, 214.0f, 59.0f);
        }

        textTip.Init(g_FontHt);
        int tipNumber = (int)(lrand48() % 11) + 1;
        string tipKey = Sprintf("tip %d", tipNumber);
        textTip.SetText(g_StringTable.GetString(tipKey));

        waitedAfterGameInited = 0;
        gameInited = false;
        GUIManager.Instance().FadeIn(-1);
    }

    /// <summary>Draws the loading title, the loading hint and the random tip.</summary>
    public override void Render()
    {
        ecGraphics graphics = ecGraphics.Instance();
        graphics.SetViewPoint(0.0f, 0.0f, 1.0f);

        if (graphics.widthMode == 3)
        {
            imageLoading?.Render(850.0f, 730.0f);
            imageLoadingTitle?.Render(0.0f, 0.0f);
            ecTextDrawScaled(textTip, 50.0f, 370.0f, 0, kLoadingTipTextScale);
        }
        else
        {
            float xBase = (graphics.screenW - 480.0f) * 0.5f;
            imageLoading?.Render(graphics.screenW - 86.0f, 300.0f);
            imageLoadingTitle?.Render(0.0f, 0.0f);
            ecTextDrawScaled(textTip, xBase + 30.0f, 150.0f, 0, kLoadingTipTextScale);
        }
    }

    /// <summary>After the game is initialized wait four more updates, then fade out and switch to the game state.</summary>
    public override void Update(float deltaTimeS)
    {
        if (waitedAfterGameInited < 0 || !gameInited) return;

        ++waitedAfterGameInited;
        if (waitedAfterGameInited > 3)
        {
            GUIManager.Instance().FadeOut(-1, null);
            waitedAfterGameInited = -1;
        }
    }

    /// <summary>Touches are ignored while loading.</summary>
    public override void TouchBegin(float x, float y, int index) { }

    public override void TouchMove(float x, float y, int index) { }

    public override void TouchEnd(float x, float y, int index) { }

    /// <summary>The back key is swallowed while loading so the state switch cannot be interrupted.</summary>
    public override bool BackPressed() => true;

    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.GUI) return false;
        if (evt.info.gui.type == Event.Info.GUIInfo.FADE_IN)
        {
            var pCGameState = (CGameState)CStateManager.Instance().GetStatePtr(GAME);
            pCGameState.InitGame();
            waitedAfterGameInited = 0;
            gameInited = true;
            return true;
        }
        if (evt.info.gui.type == Event.Info.GUIInfo.FADE_OUT)
        {
            CStateManager.Instance().SetCurState(GAME);
            return true;
        }
        return false;
    }
}
