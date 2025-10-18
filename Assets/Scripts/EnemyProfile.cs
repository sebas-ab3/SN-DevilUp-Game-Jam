using UnityEngine;

[CreateAssetMenu(fileName = "EnemyProfile", menuName = "Dudo/Enemy Profile", order = 0)]
public class EnemyProfile : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Rival";
    public Sprite portrait; // optional UI art

    [Header("Setup")]
    [Range(1, 10)] public int startingDice = 5;

    [Header("AI Behavior")]
    [Tooltip("0 = super timid, 1 = super aggressive")]
    [Range(0f, 1f)] public float aggression = 0.5f;

    [Tooltip("Base chance to call Dudo when unsure")]
    [Range(0f, 1f)] public float callDudoBase = 0.15f;

    [Tooltip("Base chance to call Spot On sometimes")]
    [Range(0f, 1f)] public float spotOnBase = 0.10f;

    [Tooltip("How often AI prefers raising quantity vs face when possible")]
    [Range(0f, 1f)] public float raiseQuantityBias = 0.6f;

    [Header("Timing (flavor)")]
    [Range(0.2f, 3f)] public float thinkTimeMin = 0.8f;
    [Range(0.2f, 3f)] public float thinkTimeMax = 1.6f;

    [Header("Optional Rules Tuning")]
    [Tooltip("If true, AI tries to stay near safe/legal edges, else pushes harder")]
    public bool respectEdges = true;
}