// ============================================================
//  EnemyScalingConfigBase.cs
//
//  Clase base abstracta que comparten EnemyScalingConfig y
//  EnemyScalingConfigV2. InfiniteLevelManager referencia esta
//  base, así acepta cualquiera de las dos en el Inspector
//  sin necesidad de cambiar código.
// ============================================================
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
