namespace WC2;

/// <summary>AI action progress panel (GUIAIProgress.cpp).</summary>
public sealed class GUIAIProgress : GUIElement
{
    public ecImage imageAiProgress;
    public ecImage imageProgressbar;
    public ecImage imageFlag;
    public int progressCount;

    public GUIAIProgress() { }

    /// <summary>Releases the images held by the progress panel.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageAiProgress = null;
        imageProgressbar = null;
        imageFlag = null;
        base.Dispose();
    }

    /// <summary>Initializes the board, the progress bar and the country flag placeholder.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        imageAiProgress = new ecImage(s_TextureRes.GetImage("aiprogress.png"));

        string progressImageName = ecGraphics.Instance().widthMode == 3 ? "progressbar.png" : "block.png";
        imageProgressbar = new ecImage(s_TextureRes.GetImage(progressImageName));
        progressCount = 0;

        imageFlag = new ecImage(g_GameRes.GetFlagImage("flag_gb.png"));
    }

    /// <summary>Refreshes the flag on the panel for the country that acts now.</summary>
    public void SetCurCountryName(string countryName)
    {
        string imageName = Sprintf("flag_%s.png", countryName);
        imageFlag.Init(g_GameRes.GetFlagImage(imageName));
    }

    /// <summary>Draws the board, the finished progress blocks and the flag of the current country.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absoluteRect);
        absoluteRect.y += 15.0f;

        imageAiProgress.Render(absoluteRect.x + 114.0f, absoluteRect.y + 257.0f);
        for (int index = 0; index < progressCount; ++index)
            imageProgressbar.Render(absoluteRect.x + 170.0f + 20.0f * index, absoluteRect.y + 271.0f);
        imageFlag.Render(absoluteRect.x + 118.0f, absoluteRect.y + 260.0f);
    }
}
