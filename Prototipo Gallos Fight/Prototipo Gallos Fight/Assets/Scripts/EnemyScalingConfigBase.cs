
using UnityEngine;

public abstract class EnemyScalingConfigBase : ScriptableObject
{
    public abstract int   GetNormalCount(int wave);
    public abstract int   GetVariantCount(int wave);
    public abstract int   GetRangedCount(int wave);
    public abstract int   GetBullCount(int wave);

    public abstract float GetHealthMult(int wave);
    public abstract float GetDamageMult(int wave);
    public abstract float GetSpeedMult(int wave);
    public abstract float GetBulletSpeedMult(int wave);
    public abstract float GetShootCooldownMult(int wave);
}
