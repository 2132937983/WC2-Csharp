namespace WC2;

/// <summary>Medal pick-up animation on the map (CMedal.cpp).</summary>
public sealed class CMedal
{
    public float[] pos = new float[2];
    public float offsetY;
    public float alpha;
    public bool animationCompleted;

    /// <summary>Constructs the map medal animation object.</summary>
    public CMedal()
    {
    }

    /// <summary>Initialises the position and display state of the medal animation.</summary>
    /// <param name="x">Scene X coordinate.</param>
    /// <param name="y">Scene Y coordinate.</param>
    public void Init(float x, float y)
    {
        pos[0] = x;
        pos[1] = y;
        offsetY = 0.0f;
        alpha = 1.0f;
        animationCompleted = false;
    }

    /// <summary>Updates the medal floating up and fading out.</summary>
    /// <param name="deltaTimeS">Seconds since the previous update.</param>
    public void Upate(float deltaTimeS)
    {
        if (animationCompleted)
            return;

        offsetY -= deltaTimeS * 30.0f;
        float newAlpha = alpha - deltaTimeS * 0.5f;
        if (newAlpha < 0.0f)
        {
            alpha = 0.0f;
            animationCompleted = true;
        }
        else
        {
            alpha = newAlpha;
        }
    }

    public void Update(float time) => Upate(time);

    /// <summary>Draws the medal glow and the medal itself.</summary>
    public void Render()
    {
        if (alpha <= 0.0f)
            return;

        ecImage medalLightImage = g_GameRes.imageMedalLight;
        ecImage medalImage = g_GameRes.imageMedal;
        float renderY = pos[1] + offsetY;

        if (medalLightImage != null)
        {
            medalLightImage.blendMode = 1;
            medalLightImage.SetAlpha(alpha, -1);
            medalLightImage.Render(pos[0], renderY);
        }

        if (medalImage != null)
        {
            medalImage.SetAlpha(alpha, -1);
            medalImage.Render(pos[0], renderY);
        }
    }
}
