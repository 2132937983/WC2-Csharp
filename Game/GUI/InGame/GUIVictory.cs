namespace WC2;

/// <summary>Victory / game over banner animation (GUIVictory.cpp).</summary>
public sealed class GUIVictory : GUIElement
{
    public ecTexture textureBoardEnd;
    public ecImage imageBoardEnd;
    public ecTexture textureText;
    public ecImage imageText;
    public float playTime;
    public float xScale;
    public float textAlpha;
    public int playing;
    public float yBoard;
    public float yScale;
    public bool victorious;

    public GUIVictory() { }

    /// <summary>Releases the images and standalone textures of the animation.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageBoardEnd = null;
        if (textureBoardEnd != null)
        {
            ecGraphics.Instance().FreeTexture(textureBoardEnd);
            textureBoardEnd = null;
        }

        imageText = null;
        if (textureText != null)
        {
            ecGraphics.Instance().FreeTexture(textureText);
            textureText = null;
        }
        base.Dispose();
    }

    /// <summary>Initializes the board and the text picture of the victory or defeat animation.</summary>
    public void Init(in GUIRect rectIn, bool victoriousIn)
    {
        rect = rectIn;
        victorious = victoriousIn;
        xScale = 1.0f;
        playTime = -1.0f;
        textAlpha = 1.0f;
        playing = 0;
        yScale = 1.0f;

        ecTextureRect textureRect;
        if (Layout.Tablet)
        {
            textureRect = new ecTextureRect(2.0f, 0.0f, 1.0f, 182.0f, 0.0f, 0.0f);
            textureBoardEnd = ecGraphics.Instance().LoadTexture("board_end@2x.png");
            imageBoardEnd = new ecImage(textureBoardEnd, textureRect);

            if (victoriousIn)
            {
                textureText = ecGraphics.Instance().LoadTexture("victory@2x.png");
                textureRect = new ecTextureRect(6.0f, 6.0f, 596.0f, 132.0f, 298.0f, 66.0f);
            }
            else
            {
                textureText = ecGraphics.Instance().LoadTexture("gameover@2x.png");
                textureRect = new ecTextureRect(6.0f, 6.0f, 760.0f, 106.0f, 380.0f, 53.0f);
            }
            imageText = new ecImage(textureText, textureRect);
            yBoard = 182.0f;
        }
        else
        {
            textureRect = new ecTextureRect(2.0f, 0.0f, 1.0f, 91.0f, 0.0f, 0.0f);
            textureBoardEnd = ecGraphics.Instance().LoadTexture("board_end.png");
            imageBoardEnd = new ecImage(textureBoardEnd, textureRect);

            if (victoriousIn)
            {
                textureText = ecGraphics.Instance().LoadTexture("victory.png");
                textureRect = new ecTextureRect(3.0f, 3.0f, 298.0f, 66.0f, 149.0f, 33.0f);
            }
            else
            {
                textureText = ecGraphics.Instance().LoadTexture("gameover.png");
                textureRect = new ecTextureRect(3.0f, 3.0f, 380.0f, 53.0f, 190.0f, 26.0f);
            }
            imageText = new ecImage(textureText, textureRect);
            yBoard = 92.0f;
        }
    }

    /// <summary>Draws the horizontal board and the text picture of the animation.</summary>
    public override void OnRender()
    {
        float screenWidth = ecGraphics.Instance().screenW;
        float screenHeight = ecGraphics.Instance().screenH;

        if (imageBoardEnd != null)
            imageBoardEnd.RenderEx(0.0f, (screenHeight - yBoard * yScale) * 0.5f, 0.0f, screenWidth, yScale);

        if (imageText != null)
        {
            imageText.SetAlpha(textAlpha, -1);
            imageText.RenderEx(screenWidth * 0.5f, screenHeight * 0.5f, 0.0f, xScale, 0.0f);
        }
    }

    /// <summary>Entrance animation; after five seconds of display the finish event is sent.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        if (playTime < 0.0f) return;

        float nextPlayTime = playTime + deltaTimeS;
        if (nextPlayTime > 5.0f)
        {
            playTime = -1.0f;
            OnEvent(Event.Gui(Event.Info.GUIInfo.OTHER, this, 3));
        }
        else
        {
            playTime = nextPlayTime;
        }

        if (playing == 1)
        {
            xScale -= deltaTimeS * 3.0f;
            textAlpha += deltaTimeS * 1.5f;
            bool animationCompleted = textAlpha >= 1.0f;
            if (animationCompleted)
            {
                xScale = 1.0f;
                textAlpha = 1.0f;
                playing = 0;
            }

            yScale += deltaTimeS * 6.0f;
            if (yScale > 1.0f) yScale = 1.0f;
        }
    }

    /// <summary>Starts the animation and plays the matching sound or music.</summary>
    public void Play()
    {
        playTime = 0.0f;
        textAlpha = 0.0f;
        xScale = 3.0f;
        playing = 1;
        yScale = 0.2f;

        if (victorious)
        {
            g_SoundRes.PlayCharSE(CELEBRATE_WAV);
        }
        else
        {
            CCSoundBox soundBox = CCSoundBox.GetInstance();
            soundBox.UnloadMusic();
            soundBox.LoadMusic(AudioDefs.Music("defeat") ?? "defeat_music.mp3", null);
            soundBox.PlayMusic(true);
        }
    }

    /// <summary>Blocks touches and the back key while the animation plays.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH || (evt.type == Event.BACK_PRESSED && (flags & SHOWN) != 0))
            return true;

        return base.OnEvent(evt);
    }
}
