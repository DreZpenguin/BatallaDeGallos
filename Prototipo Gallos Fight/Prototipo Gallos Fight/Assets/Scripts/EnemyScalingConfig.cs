// ============================================================
//  EnemyScalingConfig.cs  — ScriptableObject
//
//  Define cómo escalan las estadísticas de cada tipo de enemigo
//  en función del número de oleada.
//
//  CREAR EN UNITY:
//   Assets → clic derecho → Create → BatallaGallos → EnemyScalingConfig
//
//  CURVAS:
//   Cada AnimationCurve tiene el eje X = oleada (0–100+) y el
//   eje Y = multiplicador que se aplica sobre el valor base.
//   Ejemplo: curva de daño con valor 2.0 en oleada 10 →
//   el enemigo hace el doble de daño en oleada 10.
// ============================================================
using UnityEngine;

[CreateAssetMenu(menuName = "BatallaGallos/EnemyScalingConfig", fileName = "EnemyScalingConfig")]
public class EnemyScalingConfig : ScriptableObject
{
    [Header("── Escalado de HP ────────────────────────────────")]
    [Tooltip("Multiplicador de HP base según la oleada (eje X).")]
    public AnimationCurve healthScale = AnimationCurve.Linear(0, 1, 50, 4);

    [Header("── Escalado de Daño ─────────────────────────────")]
    [Tooltip("Multiplicador de daño base según la oleada.")]
    public AnimationCurve damageScale = AnimationCurve.Linear(0, 1, 50, 3);

    [Header("── Escalado de Velocidad ───────────────────────")]
    [Tooltip("Multiplicador de velocidad de movimiento según la oleada.")]
    public AnimationCurve speedScale = AnimationCurve.Linear(0, 1, 50, 2);

    [Header("── Escalado de Velocidad de Proyectil ──────────")]
    [Tooltip("Multiplicador de velocidad de bala (solo enemigos a distancia).")]
    public AnimationCurve bulletSpeedScale = AnimationCurve.Linear(0, 1, 50, 2);

    [Header("── Escalado de Cooldown de Disparo ────────────")]
    [Tooltip("Multiplicador del cooldown de disparo (valor < 1 = dispara más rápido). " +
             "Usa una curva decreciente: oleada 0 → 1.0, oleada 50 → 0.4")]
    public AnimationCurve shootCooldownScale = AnimationCurve.Linear(0, 1, 50, 0.4f);

    [Header("── Composición de enemigos por oleada ──────────")]
    [Tooltip("Cuántos enemigos normales (EnemyAI) spawnear. " +
             "AnimationCurve: X = oleada, Y = cantidad (se redondea).")]
    public AnimationCurve normalEnemyCount = AnimationCurve.Linear(0, 1, 20, 3);

    [Tooltip("Cuántos enemigos variante (EnemyAI alternativo) spawnear. " +
             "Empieza a aparecer en oleadas medias para variar la composición.")]
    public AnimationCurve variantEnemyCount = AnimationCurve.Linear(0, 0, 20, 2);

    [Tooltip("Cuántos enemigos a distancia (RangedEnemyAI) spawnear.")]
    public AnimationCurve rangedEnemyCount = AnimationCurve.Linear(0, 0, 20, 2);

    [Tooltip("Cuántos toros (BullEnemyAI) spawnear.")]
    public AnimationCurve bullEnemyCount = AnimationCurve.Linear(0, 0, 10, 1);

    // ── API ────────────────────────────────────────────────────

    /// Devuelve el multiplicador de HP para la oleada indicada.
    public float GetHealthMult(int wave)        => healthScale.Evaluate(wave);
    public float GetDamageMult(int wave)        => damageScale.Evaluate(wave);
    public float GetSpeedMult(int wave)         => speedScale.Evaluate(wave);
    public float GetBulletSpeedMult(int wave)   => bulletSpeedScale.Evaluate(wave);
    public float GetShootCooldownMult(int wave) => shootCooldownScale.Evaluate(wave);

    public int GetNormalCount(int wave)   => Mathf.Max(0, Mathf.RoundToInt(normalEnemyCount.Evaluate(wave)));
    public int GetVariantCount(int wave)  => Mathf.Max(0, Mathf.RoundToInt(variantEnemyCount.Evaluate(wave)));
    public int GetRangedCount(int wave)   => Mathf.Max(0, Mathf.RoundToInt(rangedEnemyCount.Evaluate(wave)));
    public int GetBullCount(int wave)     => Mathf.Max(0, Mathf.RoundToInt(bullEnemyCount.Evaluate(wave)));
}
