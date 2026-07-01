
using UnityEngine;

[CreateAssetMenu(menuName = "BatallaGallos/EnemyScalingConfig", fileName = "EnemyScalingConfig")]
public class EnemyScalingConfig : EnemyScalingConfigBase
{
    [Header("── Composición de enemigos por oleada ──────────")]
    [Tooltip("AnimationCurve: X = oleada, Y = cantidad (se redondea).")]
    public AnimationCurve normalEnemyCount  = AnimationCurve.Linear(0, 1, 20, 3);
    public AnimationCurve variantEnemyCount = AnimationCurve.Linear(0, 0, 20, 2);
    public AnimationCurve rangedEnemyCount  = AnimationCurve.Linear(0, 0, 20, 2);
    public AnimationCurve bullEnemyCount    = AnimationCurve.Linear(0, 0, 10, 1);

    [Header("── Escalado de Stats ───────────────────────────")]
    public AnimationCurve healthScale        = AnimationCurve.Linear(0, 1, 50, 4);
    public AnimationCurve damageScale        = AnimationCurve.Linear(0, 1, 50, 3);
    public AnimationCurve speedScale         = AnimationCurve.Linear(0, 1, 50, 2);
    public AnimationCurve bulletSpeedScale   = AnimationCurve.Linear(0, 1, 50, 2);
    public AnimationCurve shootCooldownScale = AnimationCurve.Linear(0, 1, 50, 0.4f);

    public override int   GetNormalCount(int wave)        => Mathf.Max(0, Mathf.RoundToInt(normalEnemyCount.Evaluate(wave)));
    public override int   GetVariantCount(int wave)       => Mathf.Max(0, Mathf.RoundToInt(variantEnemyCount.Evaluate(wave)));
    public override int   GetRangedCount(int wave)        => Mathf.Max(0, Mathf.RoundToInt(rangedEnemyCount.Evaluate(wave)));
    public override int   GetBullCount(int wave)          => Mathf.Max(0, Mathf.RoundToInt(bullEnemyCount.Evaluate(wave)));
    public override float GetHealthMult(int wave)         => healthScale.Evaluate(wave);
    public override float GetDamageMult(int wave)         => damageScale.Evaluate(wave);
    public override float GetSpeedMult(int wave)          => speedScale.Evaluate(wave);
    public override float GetBulletSpeedMult(int wave)    => bulletSpeedScale.Evaluate(wave);
    public override float GetShootCooldownMult(int wave)  => shootCooldownScale.Evaluate(wave);
}
