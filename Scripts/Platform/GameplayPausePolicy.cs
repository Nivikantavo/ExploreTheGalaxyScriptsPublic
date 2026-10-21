using System;

public static class GameplayPausePolicy
{
    public static int PushDepth(int depth)
    {
        return depth + 1;
    }

    public static int PopDepth(int depth)
    {
        return depth > 0 ? depth - 1 : 0;
    }

    public static float CaptureSavedTimeScale(float currentTimeScale)
    {
        return currentTimeScale > 0f ? currentTimeScale : 1f;
    }

    public static float ResolveTimeScale(int depth, float savedTimeScale)
    {
        if (depth > 0)
            return 0f;

        return savedTimeScale > 0f ? savedTimeScale : 1f;
    }
}
