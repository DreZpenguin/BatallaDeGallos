// ============================================================
//  EnemyScalingConfigV2.cs  — v2 (tabla de oleadas explícita)
//  Hereda de EnemyScalingConfigBase para ser intercambiable
//  con EnemyScalingConfig en el InfiniteLevelManager.
// ============================================================
using UnityEngine;

[CreateAssetMenu(menuName = "BatallaGallos/EnemyScalingConfig V2", fileName = "EnemyScalingConfigV2")]
public class EnemyScalingConfigV2 : EnemyScalingConfigBase
{
    [System.Serializable]
    public struct WaveEntry
    {
        [Tooltip("Número de oleada (empieza en 1).")]
        public int wave;
        public int normal;
        public int variant;
        public int ranged;
        public int bull;
    }

    [Header("── Composición por oleada ──────────────────────")]
    [Tooltip("Define cuántos de cada tipo aparecen en oleadas específicas. " +
             "Ordénalas de menor a mayor. Las oleadas no definidas usan la " +
             "última entrada como base y escalan automáticamente.")]
    public WaveEntry[] waveTable = new WaveEntry[]
    {
        new WaveEntry { wave = 1, normal = 1, variant = 0, ranged = 0, bull = 0 },
        new WaveEntry { wave = 2, normal = 2, variant = 0, ranged = 0, bull = 0 },
        new WaveEntry { wave = 3, normal = 2, variant = 1, ranged = 0, bull = 0 },
        new WaveEntry { wave = 4, normal = 2, variant = 1, ranged = 1, bull = 0 },
        new WaveEntry { wave = 5, normal = 2, variant = 1, ranged = 1, bull = 1 },
        new WaveEntry { wave = 6, normal = 3, variant = 1, ranged = 1, bull = 1 },
        new WaveEntry { wave = 7, normal = 3, variant = 2, ranged = 1, bull = 1 },
        new WaveEntry { wave = 8, normal = 3, variant = 2, ranged = 2, bull = 1 },
        new WaveEntry { wave = 9, normal = 3, variant = 2, ranged = 2, bull = 2 },
        new WaveEntry { wave =10, normal = 4, variant = 2, ranged = 2, bull = 2 },
    };

    [Tooltip("Cada cuántas oleadas (tras la última de la tabla) se suma +1 normal.")]
    public int repeatScaleInterval = 3;

    [Tooltip("Máximo de enemigos totales por oleada.")]
    public int maxEnemiesPerWave = 12;

    [Header("── Escalado de Stats ───────────────────────────")]
    public AnimationCurve healthScale        = AnimationCurve.Linear(0, 1, 50, 4);
    public AnimationCurve damageScale        = AnimationCurve.Linear(0, 1, 50, 3);
    public AnimationCurve speedScale         = AnimationCurve.Linear(0, 1, 50, 2);
    public AnimationCurve bulletSpeedScale   = AnimationCurve.Linear(0, 1, 50, 2);
    public AnimationCurve shootCooldownScale = AnimationCurve.Linear(0, 1, 50, 0.4f);

    // ── Composición ────────────────────────────────────────────

    private WaveEntry GetEntryForWave(int wave)
    {
        if (waveTable == null || waveTable.Length == 0)
            return new WaveEntry { wave = wave, normal = 1 };

        WaveEntry best = waveTable[0];
        foreach (WaveEntry e in waveTable)
            if (e.wave <= wave) best = e;

        WaveEntry last = waveTable[waveTable.Length - 1];
        if (wave > last.wave && repeatScaleInterval > 0)
        {
            int extra = (wave - last.wave) / repeatScaleInterval;
            best = last;
            best.normal += extra;
        }

        int total = best.normal + best.variant + best.ranged + best.bull;
        if (total > maxEnemiesPerWave)
            best.normal = Mathf.Max(0, best.normal - (total - maxEnemiesPerWave));

        return best;
    }

    public override int GetNormalCount(int wave)  => GetEntryForWave(wave).normal;
    public override int GetVariantCount(int wave) => GetEntryForWave(wave).variant;
    public override int GetRangedCount(int wave)  => GetEntryForWave(wave).ranged;
    public override int GetBullCount(int wave)    => GetEntryForWave(wave).bull;

    // ── Stats ──────────────────────────────────────────────────

    public override float GetHealthMult(int wave)        => healthScale.Evaluate(wave);
    public override float GetDamageMult(int wave)        => damageScale.Evaluate(wave);
    public override float GetSpeedMult(int wave)         => speedScale.Evaluate(wave);
    public override float GetBulletSpeedMult(int wave)   => bulletSpeedScale.Evaluate(wave);
    public override float GetShootCooldownMult(int wave) => shootCooldownScale.Evaluate(wave);
}
