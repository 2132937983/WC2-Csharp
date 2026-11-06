namespace WC2;

/// <summary>
/// Panels of the original game that depend on networking (service, multiplayer). The port keeps them
/// as empty, closable panels so the surrounding flow (menu/game state) still works. The original's
/// in-app store is removed entirely: this build sells nothing.
/// </summary>
public class GUIStubPanel : GUIElement
{
    public GUIButton buttonBack;
    public GUIButton buttonOk;
    protected ecText title;

    protected void InitStub(in GUIRect rectIn, string caption)
    {
        rect = rectIn;
        title = new ecText(FontGlobals.DefaultTextFont());
        title.SetText(caption);
        var backRect = new GUIRect(rect.w - 40f, 4f, 32f, 30f);
        buttonBack = GUIManager.Instance().AddButton("buttontext_back.png", "buttontext_back_press.png", backRect, this, null);
        if (buttonBack.imageNotPresed == null) buttonBack.SetText("<");
        buttonBack.SetBackground("buttonboard_gray.png");
    }

    public override void OnRender()
    {
        GetAbsRect(out var abs);
        ecGraphics.Instance().Fade(0.5f);
        ecGraphics.Instance().RenderRect(abs.x, abs.y, abs.w, abs.h, 0xC0202020u);
        title?.DrawText(abs.x + abs.w * 0.5f, abs.y + abs.h * 0.5f - 8f, TextAlign.MIDDLE_ALIGNED);
    }

    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH) return base.OnEvent(evt) || true;
        return base.OnEvent(evt);
    }
}

/// <summary>Customer service panel.</summary>
public sealed class GUIService : GUIStubPanel
{
    public void Init(in GUIRect rectIn) => InitStub(rectIn, "Service");

    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.GUI && evt.info.gui.type == Event.Info.GUIInfo.BUTTON && ReferenceEquals(evt.info.gui.ptr, buttonBack))
            return base.OnEvent(Event.Gui(Event.Info.GUIInfo.OTHER, this, 21));
        return base.OnEvent(evt);
    }
}

/// <summary>"More games" promotion panel.</summary>
public sealed class GUINewGame : GUIStubPanel
{
    public void Init(in GUIRect rectIn) => InitStub(rectIn, "More games");

    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.GUI && evt.info.gui.type == Event.Info.GUIInfo.BUTTON && ReferenceEquals(evt.info.gui.ptr, buttonBack))
            return base.OnEvent(Event.Gui(Event.Info.GUIInfo.OTHER, this, 28));
        return base.OnEvent(evt);
    }
}

/// <summary>Multiplayer host list.</summary>
public sealed class GUIHostList : GUIStubPanel
{
    public void Init(in GUIRect rectIn) => InitStub(rectIn, "Multiplayer is not available in this version");
    public void ResetConnectState() { }
}

/// <summary>Multiplayer corps setup.</summary>
public sealed class GUIMatchSetup : GUIStubPanel
{
    public void Init(in GUIRect rectIn)
    {
        InitStub(rectIn, "Multiplayer is not available in this version");
        var okRect = new GUIRect(rect.w - 80f, 4f, 32f, 30f);
        buttonOk = GUIManager.Instance().AddButton("buttontext_ok.png", "buttontext_ok_press.png", okRect, this, null);
        buttonOk.SetBackground("buttonboard_green.png");
        buttonOk.Hide();
    }

    public void CreatCorsButtons() { }
    public void RefreshCorsButtons() { }
}

/// <summary>"X in action" banner used by traditional multiplayer.</summary>
public sealed class GUIActionInfo : GUIElement
{
    public ecImage x34;
    public readonly ecLabelText textInfo = new();

    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;
        textInfo.Init("Arial", 14, 14, (int)rect.w, (int)rect.h);
    }

    public void SetInfo(string info) => textInfo.SetText(info);

    public override void OnRender()
    {
        GetAbsRect(out var abs);
        textInfo.DrawText(abs.x, abs.y);
    }
}

/// <summary>
/// Battle animation screen. The original animation lives only in the binary library; the
/// remake resolves fights instantly (CGameState.StartBattale), so this panel is never shown.
/// </summary>
public sealed class GUIBattle : GUIElement
{
    public int startAreaId;
    public int targetAreaId;
    public int battleState;
    public int attackState;
    public float secondAttackTime;
    public float startAttackTime;
    public readonly CFight fight = new();

    public void Init(in GUIRect rectIn) => rect = rectIn;
    public void ResetBattle() { battleState = 0; attackState = 0; }
    public void BattleStart(int x288, int startAreaIdIn, int targetAreaIdIn)
    {
        startAreaId = startAreaIdIn;
        targetAreaId = targetAreaIdIn;
        battleState = 1;
    }
    public void BattleFinish() { battleState = 0; Hide(); }
    public void UpateFighting(float deltaTimeS) { }
    public override void OnUpdate(float deltaTimeS) { }
    public override bool OnEvent(Event evt) => evt.type == Event.TOUCH && IsShown ? true : base.OnEvent(evt);
}
