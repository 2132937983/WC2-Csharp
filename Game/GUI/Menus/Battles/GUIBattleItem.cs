namespace WC2;

/// <summary>One level entry of the campaign battle list (gui/menus/battles/GUIBattleItem.cpp).</summary>
public sealed class GUIBattleItem : GUIElement
{
    // the multiplayer level ids and their text pictures are not a plain sequence; keep the original table order
    private static readonly string[] kMultiplayerBattleTextImages =
    {
        "text_axis_01.png",
        "text_axis_02.png",
        "text_allies_02.png",
        "text_axis_04.png",
        "text_allies_07.png",
        "text_axis_05.png",
        "text_allies_09.png",
        "text_axis_03.png",
        "text_allies_04.png",
        "text_allies_05.png",
        "text_axis_06.png",
        "text_allies_08.png",
    };

    public int series;
    public int level;
    public int rank;
    public ecImage imageButton;
    public ecImage imageText;
    public ecImage imageSmallRankStar;
    public ecImage imageMarkLocked;
    public bool touched;
    public bool selected;
    public bool locked;

    /// <summary>The original only clears the four picture pointers; level, stars and state are set by Init.</summary>
    public GUIBattleItem() { }

    /// <summary>Releases the pictures owned by the entry.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageButton = null;
        imageText = null;
        imageSmallRankStar = null;
        imageMarkLocked = null;
        base.Dispose();
    }

    // empty in the original; kept as a virtual slot
    public override void OnUpdate(float deltaTimeS) { }

    /// <summary>
    /// Creates the pictures of the given series/level.
    /// </summary>
    /// <param name="seriesIn">0 axis, 1 allies, 2 WTO, 3 NATO, 4 multiplayer</param>
    /// <param name="levelIn">zero based level inside the series</param>
    /// <param name="rectIn">initial area inside the list parent</param>
    public void Init(int seriesIn, int levelIn, in GUIRect rectIn)
    {
        rect = rectIn;
        series = seriesIn;
        level = levelIn;

        string buttonImageName = "";
        string textImageName = "";
        if (seriesIn == 0)
        {
            int displayIndex = levelIn + 1;
            buttonImageName = Sprintf("button_axis_%02d.png", displayIndex);
            textImageName = Sprintf("text_axis_%02d.png", displayIndex);
        }
        else
        {
            switch (seriesIn)
            {
                case 1:
                {
                    int displayIndex = levelIn + 1;
                    buttonImageName = Sprintf("button_allies_%02d.png", displayIndex);
                    textImageName = Sprintf("text_allies_%02d.png", displayIndex);
                    break;
                }
                case 2:
                {
                    int displayIndex = levelIn + 1;
                    buttonImageName = Sprintf("button_wto_%02d.png", displayIndex);
                    textImageName = Sprintf("text_wto_%02d.png", displayIndex);
                    break;
                }
                case 3:
                {
                    int displayIndex = levelIn + 1;
                    buttonImageName = Sprintf("button_nato_%02d.png", displayIndex);
                    textImageName = Sprintf("text_nato_%02d.png", displayIndex);
                    break;
                }
                case 4:
                    buttonImageName = Sprintf("button_multiplay_%02d.png", levelIn + 1);
                    textImageName = kMultiplayerBattleTextImages[levelIn];
                    break;
            }
        }

        imageButton = new ecImage(s_TextureRes.GetImage(buttonImageName));
        imageText = new ecImage(s_TextureRes.GetImage(textImageName));
        imageSmallRankStar = new ecImage(s_TextureRes.GetImage("small_rankstar.png"));
        var markLocked = new ecImage(s_TextureRes.GetImage("mark_locked.png"));

        touched = false;
        selected = false;
        locked = false;
        rank = 0;
        imageMarkLocked = markLocked;
    }

    /// <summary>
    /// Draws the level button, its text, the locked mark and the victory stars. The selected
    /// button and text are scaled by 1.15; locked entries draw no text and use a grey button.
    /// </summary>
    public override void OnRender()
    {
        GetAbsRect(out GUIRect absRect);

        int color = (locked || touched) ? -2960686 : -1;
        imageButton.SetColor(color, -1);
        if (imageText != null)
        {
            imageText.SetColor(color, -1);
        }

        if (selected)
        {
            imageButton.RenderEx(absRect.x, absRect.y - absRect.h * 0.075f, 0f, 1.15f, 0f);
            if (imageText != null && !locked)
            {
                imageText.RenderEx(absRect.x, absRect.y + absRect.h * 0.5f, 0f, 1.15f, 0f);
            }
        }
        else
        {
            imageButton.Render(absRect.x, absRect.y);
            if (imageText != null && !locked)
            {
                imageText.Render(absRect.x, absRect.y + absRect.h * 0.5f);
            }
        }

        float x = absRect.x;
        float y = absRect.y;
        if (ecGraphics.Instance().widthMode != 3)
        {
            if (locked)
            {
                imageMarkLocked.RenderEx(x + 67f, y + 22f, 0f, 0.8f, 0f);
            }

            // normal layout: three stars on top, two below
            switch (rank)
            {
                case 1:
                    imageSmallRankStar.Render(x + 22f, y + 33f);
                    break;
                case 2:
                    imageSmallRankStar.Render(x + 16f, y + 33f);
                    imageSmallRankStar.Render(x + 28f, y + 33f);
                    break;
                case 3:
                    imageSmallRankStar.Render(x + 10f, y + 33f);
                    imageSmallRankStar.Render(x + 22f, y + 33f);
                    imageSmallRankStar.Render(x + 34f, y + 33f);
                    break;
                case 4:
                    imageSmallRankStar.Render(x + 16f, y + 23f);
                    imageSmallRankStar.Render(x + 16f, y + 34f);
                    imageSmallRankStar.Render(x + 28f, y + 23f);
                    imageSmallRankStar.Render(x + 28f, y + 34f);
                    break;
                case 5:
                    imageSmallRankStar.Render(x + 10f, y + 23f);
                    imageSmallRankStar.Render(x + 22f, y + 23f);
                    imageSmallRankStar.Render(x + 34f, y + 23f);
                    imageSmallRankStar.Render(x + 16f, y + 34f);
                    imageSmallRankStar.Render(x + 28f, y + 34f);
                    break;
                default:
                    break;
            }
            return;
        }

        if (locked)
        {
            imageMarkLocked.RenderEx(x + 134f, y + 44f, 0f, 0.8f, 0f);
        }

        // HD layout: same pattern, every star coordinate doubled
        switch (rank)
        {
            case 1:
                imageSmallRankStar.Render(x + 44f, y + 66f);
                break;
            case 2:
                imageSmallRankStar.Render(x + 32f, y + 66f);
                imageSmallRankStar.Render(x + 56f, y + 66f);
                break;
            case 3:
                imageSmallRankStar.Render(x + 20f, y + 66f);
                imageSmallRankStar.Render(x + 44f, y + 66f);
                imageSmallRankStar.Render(x + 68f, y + 66f);
                break;
            case 4:
                imageSmallRankStar.Render(x + 32f, y + 46f);
                imageSmallRankStar.Render(x + 32f, y + 68f);
                imageSmallRankStar.Render(x + 56f, y + 46f);
                imageSmallRankStar.Render(x + 56f, y + 68f);
                break;
            case 5:
                imageSmallRankStar.Render(x + 20f, y + 46f);
                imageSmallRankStar.Render(x + 44f, y + 46f);
                imageSmallRankStar.Render(x + 68f, y + 46f);
                imageSmallRankStar.Render(x + 32f, y + 68f);
                imageSmallRankStar.Render(x + 56f, y + 68f);
                break;
            default:
                break;
        }
    }

    /// <summary>Turns a tap on the entry into a GUI BUTTON event for the parent.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH)
        {
            return base.OnEvent(evt);
        }

        uint currentFlags = flags;
        if ((currentFlags & (ACTIVE | SHOWN)) != (ACTIVE | SHOWN))
        {
            return false;
        }
        if ((currentFlags & ENABLED) == 0)
        {
            return false;
        }

        switch (evt.info.touch.type)
        {
            case Event.Info.TouchInfo.BEGIN:
                if (CheckInRect(evt.info.touch.pos[0], evt.info.touch.pos[1]))
                {
                    touched = true;
                }
                break;

            case Event.Info.TouchInfo.MOVE:
                // leaving the entry cancels the tap; re-entering does not press it again
                if (touched &&
                    !CheckInRect(evt.info.touch.pos[0], evt.info.touch.pos[1]))
                {
                    touched = false;
                }
                break;

            case Event.Info.TouchInfo.END:
                if (touched)
                {
                    touched = false;
                    CCSoundBox.GetInstance().PlaySE(AudioDefs.Ui("click"));

                    // dispatched through the base so the parent receives a "this entry was clicked" BUTTON event
                    base.OnEvent(Event.Button(this));
                }
                break;
        }

        return base.OnEvent(evt);
    }

    public void SetSelected(bool newSelected)
    {
        selected = newSelected;
    }
}
