namespace WC2;

/// <summary>Touch drag inertia (CTouchInertia.cpp).</summary>
public sealed class CTouchInertia
{
    public float x0;
    public float x4;
    public float cosAngle;
    public float sinAngle;
    public float speed;
    public float slowdown;
    public bool touching;
    public int touchIndex;
    public float touchTime;
    public float[][] trackPoint = { new float[3], new float[3], new float[3], new float[3], new float[3] };
    public int trackPointIndex;
    public int trackPointCount;

    /// <summary>Creates the inertia state with the default parameters.</summary>
    public CTouchInertia()
    {
        Init();
    }

    /// <summary>Resets the inertia to the initial resting state.</summary>
    public void Init()
    {
        speed = 0.0f;
        slowdown = -150.0f;
        touching = false;
        touchIndex = 0;
    }

    /// <summary>
    /// Current inertia speed on X and Y. No speed is reported while the finger is still down
    /// or when the inertia speed has reached zero.
    /// </summary>
    public bool GetSpeed(out float xSpeed, out float ySpeed)
    {
        xSpeed = 0.0f;
        ySpeed = 0.0f;
        if (touching) return false;
        if (speed <= 0.0f) return false;

        xSpeed = cosAngle * speed;
        ySpeed = sinAngle * speed;
        return true;
    }

    /// <summary>Writes the touch position and the touch time into the five entry ring buffer.</summary>
    public void AddTrackPoint(float x, float y)
    {
        ++trackPointIndex;
        if (trackPointIndex > 4) trackPointIndex = 0;

        trackPoint[trackPointIndex][0] = x;
        trackPoint[trackPointIndex][1] = y;
        trackPoint[trackPointIndex][2] = touchTime;
        ++trackPointCount;
    }

    /// <summary>Starts tracking the touch and records the first track point.</summary>
    public bool TouchBegin(float x, float y, int index)
    {
        if (touching) return false;

        touchIndex = index;
        touching = true;
        trackPointCount = 0;
        trackPointIndex = -1;
        touchTime = 0.0f;
        AddTrackPoint(x, y);
        return true;
    }

    /// <summary>Appends a track point for the tracked touch.</summary>
    public bool TouchMove(float x, float y, int index)
    {
        if (!touching || touchIndex != index) return false;

        AddTrackPoint(x, y);
        return true;
    }

    /// <summary>
    /// Picks the start point used for the speed computation among the last five track points.
    /// The search starts at the oldest candidate and prefers entries younger than one second.
    /// Returns null when there are not enough candidates.
    /// </summary>
    public float[] GetStartPoint()
    {
        int pointCount = trackPointCount;
        if (pointCount >= 5) pointCount = 5;

        int candidateCount = pointCount - 1;
        int startIndex = trackPointIndex + 1 - pointCount;
        float[] result = null;

        for (int i = 0; i < candidateCount; ++i)
        {
            int pointIndex = ((startIndex % 5) + 5) % 5;
            result = trackPoint[pointIndex];
            if (touchTime - result[2] < 1.0f) break;
            startIndex = pointIndex + 1;
        }

        return result;
    }

    /// <summary>
    /// Ends the tracking and derives the inertia direction and speed from the recent track.
    /// No inertia when the move is below three pixels, the time below 0.01 s or too few points.
    /// </summary>
    public bool TouchEnd(float x, float y, int index)
    {
        if (!touching || touchIndex != index) return false;

        AddTrackPoint(x, y);
        touching = false;
        speed = 0.0f;

        if (touchTime > 0.01f && trackPointCount > 1)
        {
            float[] startPoint = GetStartPoint();
            if (startPoint != null)
            {
                float elapsedTime = touchTime - startPoint[2];
                if (elapsedTime > 0.01f)
                {
                    float deltaX = x - startPoint[0];
                    float deltaY = y - startPoint[1];
                    float distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared > 9.0f)
                    {
                        speed = MathF.Sqrt(distanceSquared) / elapsedTime;
                        float inverseDistance = InvSqrt(distanceSquared);
                        cosAngle = deltaX * inverseDistance;
                        sinAngle = deltaY * inverseDistance;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Stops the touch and the inertia.</summary>
    public void Stop()
    {
        speed = 0.0f;
        touching = false;
    }

    /// <summary>Advances the touch time while touching, otherwise slows the inertia down.</summary>
    public void Update(float deltaTimeS)
    {
        if (touching)
        {
            touchTime += deltaTimeS;
        }
        else
        {
            // inertia slowdown
            float rawSpeed = speed;
            if (rawSpeed > 0.0f)
            {
                float newSpeed = rawSpeed + slowdown;
                if (newSpeed <= 0.0f) newSpeed = 0.0f;
                speed = newSpeed;
            }
        }
    }
}
