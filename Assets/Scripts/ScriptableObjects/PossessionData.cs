using UnityEngine;

[CreateAssetMenu(fileName = "PossessionData", menuName = "Match/Possession System", order = 0)]
public class PossessionData : ScriptableObject
{
    [Tooltip("Seconds a team has to score after gaining possession before chain resets")]
    public float attackTimerDuration = 30f;
    
    [Tooltip("Maximum pass multiplier achievable")]
    public int maxPassMultiplier = 5;

    [Tooltip("Multiplier value set after the FIRST successful pass (always 1)")]
    public int firstPassMultiplier = 1;

    [Header("Teams")]
    [Tooltip("Tags assigned to TeamA characters (Human player + Teammate bot)")]
    public string[] teamATags;

    [Tooltip("Tags assigned to TeamB characters (Enemy bots)")]
    public string[] teamBTags;
}
