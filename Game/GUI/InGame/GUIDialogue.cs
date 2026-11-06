namespace WC2;

/// <summary>Story dialogue overlay with the general photo (GUIDialogue.cpp).</summary>
public sealed class GUIDialogue : GUIElement
{
    // Size of the dialogue text; 1.0f is the original size.
    private const float kDialogueTextScale = 0.67f;

    public ecTexture textureGeneral;
    public ecImage imageGeneral;
    public ecTexture textureDlgBoard;
    public ecImage imageDlgBoard;
    public readonly ecUniFont font = new();
    public readonly ecText text = new();
    public int x88;

    public GUIDialogue() { }

    /// <summary>Releases the font cache, then the images before their textures like the original.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        if (font.IsLoaded) font.Release();

        imageGeneral = null;
        if (textureGeneral != null)
        {
            ecGraphics.Instance().FreeTexture(textureGeneral);
            textureGeneral = null;
        }
        imageDlgBoard = null;
        if (textureDlgBoard != null)
        {
            ecGraphics.Instance().FreeTexture(textureDlgBoard);
            textureDlgBoard = null;
        }
        base.Dispose();
    }

    /// <summary>
    /// Initializes the fixed resources of the in-game dialogue: the board, the font and the
    /// initial state. The general photo is loaded by ShowDlg per general.
    /// </summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        // the board is a standalone texture; the original uses the 402 x 87 region
        textureDlgBoard = ecGraphics.Instance().LoadTexture("dlgboard.png");
        imageDlgBoard = new ecImage(textureDlgBoard, 0.0f, 0.0f, 402.0f, 87.0f);

        text.Init(g_FontKt);
        text.SetColor(0xFF00FCFFu);

        // display state counter maintained by the original Update/Render logic
        x88 = 0;
    }

    /// <summary>The original has no per frame logic for the dialogue.</summary>
    public override void OnUpdate(float deltaTimeS) { }

    /// <summary>
    /// Draws the general photo, the board and the text. The wide layout uses fixed coordinates,
    /// the others center a 480 wide canvas horizontally.
    /// </summary>
    public override void OnRender()
    {
        GetAbsRect(out var absRect);

        if (Layout.Tablet)
        {
            // wide layout: photo near the bottom right, board and text on its left
            if (imageGeneral != null)
                imageGeneral.Render(absRect.x + 742.0f, absRect.y + absRect.h);
            imageDlgBoard.Render(absRect.x + 311.0f, absRect.y + 682.0f);
            text.SetColor(0xFFFFFFFFu);
            ecTextDrawScaled(text, absRect.x + 325.0f, absRect.y + 695.0f, 0, kDialogueTextScale);
            return;
        }

        // the other layouts use a 480 wide reference; half of the extra width is the horizontal offset
        float centerOffset = (ecGraphics.Instance().screenW - 480.0f) * 0.5f;
        if (imageGeneral != null)
            imageGeneral.Render(absRect.x + centerOffset + 370.0f, absRect.y + absRect.h);
        imageDlgBoard.Render(absRect.x + centerOffset + 39.0f, absRect.y + 234.0f);
        text.SetColor(0xFFFFFFFFu);
        ecTextDrawScaled(text, absRect.x + centerOffset + 54.0f, absRect.y + 248.0f, 0, kDialogueTextScale);
    }

    /// <summary>
    /// Switches the general photo and the localized text and shows the dialogue. Only generals
    /// registered in the GeneralPhoto table are accepted; otherwise the current content stays.
    /// left is a kept interface parameter the original never used.
    /// </summary>
    public void ShowDlg(string textName, string generalName, bool left)
    {
        if (textName == null || string.IsNullOrEmpty(generalName)) return;
        GeneralPhoto generalPhoto = CObjectDef.Instance().GetGeneralPhoto(generalName);
        if (generalPhoto == null) return;

        // release the old image, then its texture, then build the photo of the new general
        imageGeneral = null;
        if (textureGeneral != null)
        {
            ecGraphics.Instance().FreeTexture(textureGeneral);
            textureGeneral = null;
        }

        textureGeneral = ecGraphics.Instance().LoadWEBPTexture(generalPhoto.fileName);
        imageGeneral = new ecImage(textureGeneral, 0.0f, 0.0f, generalPhoto.w, generalPhoto.h);

        // GeneralPhoto only stores the horizontal hot spot; the vertical one is the photo height
        imageGeneral.textureRect.refx = generalPhoto.refx;
        imageGeneral.textureRect.refy = generalPhoto.h;

        text.SetText(g_StringTable.GetString(textName));
        x88 = 0;
        Show();
    }

    /// <summary>Hides the dialogue but keeps the photo and the text for the next ShowDlg.</summary>
    public void HideDlg()
    {
        Hide();
    }

    /// <summary>
    /// Touches are always consumed; the release closes the dialogue and sends OTHER(16) up the
    /// GUI chain. Other events keep the default handling.
    /// </summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH)
            return base.OnEvent(evt);

        if (evt.info.touch.type == Event.Info.TouchInfo.END)
        {
            // BEGIN/MOVE only block the map; the release really closes the dialogue
            HideDlg();

            // unlike a plain Hide this lets the game state resume the flow it had before the dialogue
            base.OnEvent(Event.Gui(Event.Info.GUIInfo.OTHER, this, 16));
        }

        // every touch during the dialogue is consumed and never reaches the map
        return true;
    }
}
