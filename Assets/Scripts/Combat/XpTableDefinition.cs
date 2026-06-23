using UnityEngine;

[CreateAssetMenu(menuName = "Ueq/XP Table")]
public class XpTableDefinition : ScriptableObject
{
    public int[] xpPerLevel = DefaultValues;

    public int Count => xpPerLevel?.Length ?? 0;

    // XP required to advance from `level` to `level+1` (1-indexed).
    public int XpForLevel(int level)
    {
        if (xpPerLevel == null || level < 1 || level > xpPerLevel.Length) return 0;
        return xpPerLevel[level - 1];
    }

    // Cumulative XP to arrive at `level` from level 1, with modifier applied.
    public int TotalXpToReach(int level, float modifier = 1f)
    {
        if (level <= 1) return 0;
        int sum = 0;
        for (int i = 1; i < level && i <= Count; i++)
            sum += Mathf.RoundToInt(xpPerLevel[i - 1] * modifier);
        return sum;
    }

    public static readonly int[] DefaultValues =
    {
            1_000,     7_000,    19_000,    37_000,    61_000,
           91_000,   127_000,   169_000,   217_000,   271_000,  // 1–10
          331_000,   397_000,   469_000,   547_000,   631_000,
          721_000,   817_000,   919_000, 1_027_000, 1_141_000,  // 11–20
        1_261_000, 1_387_000, 1_519_000, 1_657_000, 1_801_000,
        1_951_000, 2_107_000, 2_269_000, 2_437_000, 5_311_000,  // 21–30
        3_070_100, 3_274_700, 3_485_900, 3_703_700, 8_215_600,
        4_537_200, 4_796_400, 5_062_800, 5_336_400,12_017_200,  // 31–40
        6_397_300, 6_717_100, 7_044_700, 7_380_100,16_835_800,
        8_695_400, 9_081_800, 9_476_600, 9_879_800,10_291_400,  // 41–50
    };
}
