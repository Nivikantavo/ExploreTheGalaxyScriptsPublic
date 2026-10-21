using System.Collections.Generic;

public static class GatedThresholdRules
{
    public static int? ConsumeHighestNewlyReached(
        int currentValue,
        IEnumerable<int> thresholds,
        HashSet<int> acknowledged)
    {
        int? highest = null;
        if (thresholds == null || acknowledged == null)
            return null;

        foreach (int threshold in thresholds)
        {
            if (threshold <= 0 || currentValue < threshold || !acknowledged.Add(threshold))
                continue;

            if (highest == null || threshold > highest.Value)
                highest = threshold;
        }

        return highest;
    }
}
