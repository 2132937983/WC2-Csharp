namespace WC2;

/// <summary>Border boxes of the map (CBackground::RenderBox from Camera.cpp).</summary>
public sealed partial class CBackground
{
    private const float pi = 3.14159265358979f;

    /// <summary>Sandbox border rendering; additionally hides everything outside the border.</summary>
    public void RenderBox(CCamera camera, float x, float y, float w, float h)
    {
        int i;
        for (i = 0; i < (int)((h + 139.0 - 1.0) / 139.0); i++)
        {
            boxImage[2].Render(x - 82.0f, y + (float)i * 139.0f);
            boxImage[2].RenderEx(x + w + 82.0f, y + (float)i * 139.0f, 0.0f, -1.0f, 1.0f);
        }
        for (i = 0; i < (int)((w + 139.0 - 1.0) / 139.0); i++)
        {
            boxImage[1].Render(x + (float)i * 139.0f, y - 82.0f);
            boxImage[1].RenderEx(x + (float)i * 139.0f, y + h + 82.0f, 0.0f, 1.0f, -1.0f);
        }
        boxImage[0].Render(x - 82.0f, y - 82.0f);
        boxImage[0].RenderEx(x + w + 82.0f, y - 82.0f, pi * 0.5f, 1.0f, 0.0f);
        boxImage[0].RenderEx(x + w + 82.0f, y + h + 82.0f, pi, 1.0f, 0.0f);
        boxImage[0].RenderEx(x - 82.0f, y + h + 82.0f, pi * 1.5f, 1.0f, 0.0f);
        ecGraphics Graphics = ecGraphics.Instance();
        float fx = camera.centerPos[0] - camera.screenHalfSize[0] / camera.scale;
        float fx2 = x + w + 82.0f;
        float fy = camera.centerPos[1] - camera.screenHalfSize[1] / camera.scale;
        float fy2 = y + h + 82.0f;
        float fWidth = x - fx - 82.0f;
        float fHeight = (camera.screenHalfSize[1] + camera.screenHalfSize[1]) / camera.scale;
        if (fWidth > 0.0f)
            Graphics.RenderRect(fx, fy, fWidth, fHeight, 0xFF000000);
        fWidth = camera.centerPos[0] + camera.screenHalfSize[0] / camera.scale - fx2;
        if (fWidth > 0.0f)
            Graphics.RenderRect(fx2, fy, fWidth, fHeight, 0xFF000000);
        fWidth = (camera.screenHalfSize[0] + camera.screenHalfSize[0]) / camera.scale;
        fHeight = y - fy - 82.0f;
        if (fHeight > 0.0f)
            Graphics.RenderRect(fx, fy, fWidth, fHeight, 0xFF000000);
        fHeight = camera.centerPos[1] + camera.screenHalfSize[1] / camera.scale - fy2;
        if (fHeight > 0.0f)
            Graphics.RenderRect(fx, fy2, fWidth, fHeight, 0xFF000000);
    }
}

/// <summary>Scene camera (Camera.cpp). CGameManager::InitCameraPos belongs to CGameManager.</summary>
public sealed class CCamera
{
    public float[] centerPos = new float[2];
    public float scale = 1f;
    public float[] speed = new float[2];
    public float[] targetPos = new float[2];
    public bool isMoving;
    public bool autoFixPos;
    public float[] topLeftPos = new float[2];
    public float[] mapSize = new float[2];
    public float[] screenHalfSize = new float[2];

    /// <summary>The original constructor does not initialise anything; the state comes from Init().</summary>
    public CCamera() { }

    /// <summary>Initialises the scene range, movement state and screen size used by the camera.</summary>
    public void Init(float x, float y, float w, float h)
    {
        topLeftPos[0] = x;
        topLeftPos[1] = y;
        mapSize[0] = w;
        scale = 1.0f;
        speed[0] = 0.0f;
        speed[1] = 0.0f;
        mapSize[1] = h;
        isMoving = false;
        autoFixPos = false;
        centerPos[0] = w * 0.5f;
        centerPos[1] = h * 0.5f;
        screenHalfSize[0] = ecGraphics.Instance().screenW * 0.5f;
        screenHalfSize[1] = ecGraphics.Instance().screenH * 0.5f;
    }

    /// <summary>Sets the camera pose: position and zoom.</summary>
    public void SetPosAndScale(float x, float y, float Scale)
    {
        float fWidth = (screenHalfSize[0] + screenHalfSize[0]) / mapSize[0];
        float fMinScale = (screenHalfSize[1] + screenHalfSize[1]) / mapSize[1];
        if (fMinScale > fWidth)
            fMinScale = fWidth;
        if (Scale < fMinScale)
            Scale = fMinScale;
        if (Scale > 1.0f)
            Scale = 1.0f;
        scale = Scale;
        SetPos(x, y, true);
    }

    /// <summary>Sets the camera centre; focuses on the middle when the view is bigger than the map.</summary>
    public bool SetPos(float x, float y, bool border)
    {
        centerPos[0] = x;
        centerPos[1] = y;
        int i;
        bool bFixed = false;
        for (i = 0; i < 2; i++)
        {
            float fMinPos = topLeftPos[i];
            float fMaxPos = fMinPos + mapSize[i];
            if (border)
            {
                float fMinDistance = screenHalfSize[i] / scale;
                fMinPos += fMinDistance;
                fMaxPos -= fMinDistance;
            }
            float fCenterPos = centerPos[i];
            if (fCenterPos < fMinPos)
            {
                fCenterPos = fMinPos;
                bFixed = true;
            }
            if (fCenterPos > fMaxPos)
            {
                fCenterPos = fMaxPos;
                bFixed = true;
            }
            if (fMinPos > fMaxPos)
                fCenterPos = (fMinPos + fMaxPos) * 0.5f;
            if (bFixed)
                centerPos[i] = fCenterPos;
        }
        return bFixed;
    }

    public bool Move(float x, float y, bool border)
    {
        return SetPos(centerPos[0] + x / scale, centerPos[1] + y / scale, border);
    }

    public void SetAutoFixPos(bool enabled)
    {
        autoFixPos = enabled;
        if (enabled)
        {
            int i;
            for (i = 0; i < 2; i++)
            {
                float MinDistance = screenHalfSize[i] / scale;
                float MinPos = topLeftPos[i] + MinDistance;
                float MaxPos = topLeftPos[i] + mapSize[i] - MinDistance;
                float TargetPos = centerPos[i];
                if (TargetPos < MinPos)
                    TargetPos = MinPos;
                if (TargetPos > MaxPos)
                    TargetPos = MaxPos;
                if (MinPos > MaxPos)
                    TargetPos = (MinPos + MaxPos) * 0.5f;
                float distance = TargetPos - centerPos[i];
                if (Math.Abs(distance) > 1.0f)
                {
                    targetPos[i] = TargetPos;
                    speed[i] = distance * 0.1f;
                    isMoving = true;
                }
                else
                {
                    speed[i] = 0.0f;
                    centerPos[i] = TargetPos;
                }
            }
        }
        else
        {
            speed[0] = 0.0f;
            speed[1] = 0.0f;
        }
    }

    public void MoveTo(float x, float y, bool border)
    {
        float[] SetSpeed = { 0.012f, 0.015f, 0.02f, 0.026f, 0.034f };
        targetPos[0] = x;
        targetPos[1] = y;
        int i;
        for (i = 0; i < 2; i++)
        {
            float fMinPos = topLeftPos[i];
            float fMaxPos = fMinPos + mapSize[i];
            if (border)
            {
                float fMinDistance = screenHalfSize[i] / scale;
                fMinPos += fMinDistance;
                fMaxPos -= fMinDistance;
            }
            float fTargetPos = targetPos[i];
            if (fTargetPos < fMinPos)
                fTargetPos = fMinPos;
            if (fTargetPos > fMaxPos)
                fTargetPos = fMaxPos;
            if (fMinPos > fMaxPos)
                fTargetPos = (fMinPos + fMaxPos) * 0.5f;
            float fStep = fTargetPos - centerPos[i];
            if (Math.Abs(fStep) > 1.0f)
            {
                targetPos[i] = fTargetPos;
                speed[i] = fStep * SetSpeed[g_GameSettings.speed];
                isMoving = true;
            }
            else
            {
                speed[i] = 0.0f;
                centerPos[i] = fTargetPos;
            }
        }
    }

    /// <summary>Updates the camera.</summary>
    public void Upate(float time)
    {
        if (!isMoving) return;
        if (g_GameManager.SkipMode)
        {
            // Stop the camera movement while skipping.
            speed[0] = 0.0f;
            speed[1] = 0.0f;
            isMoving = false;
            autoFixPos = false;
            return;
        }
        int i;
        for (i = 0; i < 2; i++)
        {
            if (speed[i] == 0.0f) continue;
            if (Math.Abs((double)targetPos[i] - centerPos[i]) < Math.Abs(speed[i] * 30.0 * time))
            {
                centerPos[i] = targetPos[i];
                speed[i] = 0.0f;
            }
            else
            {
                centerPos[i] += speed[i] * 30.0f * time;
            }
        }
        if (speed[0] == 0.0f && speed[1] == 0.0f)
        {
            isMoving = false;
            autoFixPos = false;
        }
    }

    public void Update(float deltaTimeS) => Upate(deltaTimeS);

    /// <summary>
    /// Applies the camera centre and scale to the graphics system. The graphics system uses the
    /// top-left corner of the view, so the half screen size is scaled and subtracted from the centre.
    /// </summary>
    public void Apply()
    {
        float viewX = centerPos[0] - screenHalfSize[0] / scale;
        float viewY = centerPos[1] - screenHalfSize[1] / scale;
        ecGraphics.Instance().SetViewPoint(viewX, viewY, scale);
    }

    /// <summary>True when the scene rectangle intersects the camera view.</summary>
    public bool IsRectInCamera(float x, float y, float w, float h)
    {
        float halfWidth = screenHalfSize[0] / scale;
        float halfHeight = screenHalfSize[1] / scale;

        return x + w >= centerPos[0] - halfWidth &&
               x <= centerPos[0] + halfWidth &&
               y + h >= centerPos[1] - halfHeight &&
               y <= centerPos[1] + halfHeight;
    }

    /// <summary>
    /// True when the scene rectangle lies completely inside the region not covered by the level GUI.
    /// iPad and normal layouts use different top/bottom ranges; the fixed screen distances are
    /// converted to scene units with the camera scale.
    /// </summary>
    public bool IsRectInVisibleRegion(float x, float y, float w, float h)
    {
        float halfWidth = screenHalfSize[0] / scale;
        if (x < centerPos[0] - halfWidth ||
            x + w > centerPos[0] + halfWidth)
        {
            return false;
        }

        float scaleValue = scale;
        if (Layout.Tablet)
        {
            return y >= centerPos[1] - 346.0f / scaleValue &&
                   y + h <= centerPos[1] + 320.0f / scaleValue;
        }

        return y >= centerPos[1] - 130.0f / scaleValue &&
               y + h <= centerPos[1] + 113.0f / scaleValue;
    }

    /// <summary>Sets the scene rectangle.</summary>
    public void SetSceneRect(float x, float y, float w, float h)
    {
        mapSize[0] = w;
        mapSize[1] = h;
        topLeftPos[0] = x;
        topLeftPos[1] = y;
    }
}
