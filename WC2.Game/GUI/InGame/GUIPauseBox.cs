namespace WC2;

/// <summary>Pause menu (GUIPauseBox.cpp).</summary>
public sealed class GUIPauseBox : GUIElement
{
    public GUIButton buttonResume;
    public GUIButton buttonSave;
    public GUIButton buttonOption;
    public GUIButton buttonRestart;
    public GUIButton buttonQuit;
    public ecTexture x48;
    public ecImage imageMenuBox;

    public GUIPauseBox() { }

    /// <summary>Releases the image and texture held directly.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageMenuBox = null;
        if (x48 != null)
        {
            ecGraphics.Instance().FreeTexture(x48);
            x48 = null;
        }
        base.Dispose();
    }

    /// <summary>Initializes the pause menu and its buttons.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;
        imageMenuBox = new ecImage(s_TextureRes.GetImage("menubox.png"));

        GUIManager guiManager = GUIManager.Instance();
        var buttonRect = new GUIRect(32.0f, 52.0f, 98.0f, 34.0f);
        buttonResume = guiManager.AddButton("buttontext_resume.png", "buttontext_resume_press.png", buttonRect, this, null);
        buttonResume.SetBackground("longgreen_normal.png");

        buttonRect = new GUIRect(32.0f, 90.0f, 102.0f, 34.0f);
        buttonSave = guiManager.AddButton("buttontext_save.png", "buttontext_save_press.png", buttonRect, this, null);
        buttonSave.SetBackground("longgreen_normal.png");

        buttonRect = new GUIRect(32.0f, 128.0f, 98.0f, 34.0f);
        buttonOption = guiManager.AddButton("buttontext_options.png", "buttontext_options_press.png", buttonRect, this, null);
        buttonOption.SetBackground("longgreen_normal.png");

        buttonRect = new GUIRect(32.0f, 166.0f, 98.0f, 34.0f);
        buttonRestart = guiManager.AddButton("buttontext_restart.png", "buttontext_restart_press.png", buttonRect, this, null);
        buttonRestart.SetBackground("longred_normal.png");

        buttonRect = new GUIRect(32.0f, 204.0f, 98.0f, 34.0f);
        buttonQuit = guiManager.AddButton("buttontext_quit.png", "buttontext_quit_press.png", buttonRect, this, null);
        buttonQuit.SetBackground("longred_normal.png");

        if (g_GameManager.gameMode == CGameManager.MULTIPLAYER)
        {
            buttonSave.SetEnable(false);
            buttonRestart.SetEnable(false);
        }
    }

    /// <summary>The resume button closes the pause menu.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.GUI && evt.info.gui.type == Event.Info.GUIInfo.BUTTON)
        {
            GUIElement source = evt.info.gui.ptr;
            if (!ReferenceEquals(source, buttonSave) &&
                !ReferenceEquals(source, buttonOption) &&
                !ReferenceEquals(source, buttonQuit) &&
                ReferenceEquals(source, buttonResume))
            {
                Hide();
            }
        }

        return base.OnEvent(evt);
    }

    /// <summary>Draws the half transparent dim and the menu panel.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absRect);
        ecGraphics.Instance().Fade(0.5f);
        imageMenuBox.Render(absRect.x, absRect.y);
    }
}
