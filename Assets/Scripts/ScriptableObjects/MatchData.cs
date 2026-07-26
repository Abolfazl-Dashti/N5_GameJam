using UnityEngine;

[CreateAssetMenu(fileName = "MatchData", menuName = "Team/Match Data", order = 2)]
public class MatchData : ScriptableObject
{
    [Header("Match Timer")]
    [Tooltip("Total match duration in seconds. Default = 8 minutes.")]
    public float matchDuration = 480f;
    
    public float postGoalRespawnDelay = 4f;
    public float centerSpawnFreezeTime = 2f;

    [Header("Spawn Positions")] 
    [Tooltip("World position where the disc respawns at center")]
    public Vector3 discSpawnPosition;

    [Tooltip("World rotation the disc has at spawn")]
    public Vector3 discSpawnRotation = Vector3.zero;

    [Tooltip("Spawn point for the human Player")]
    public Vector3 playerSpawnPosition;

    [Tooltip("Spawn point for the TeamA Bot (teammate)")]
    public Vector3 teamABotSpawnPosition;

    [Tooltip("Spawn point for TeamB Bot 1")]
    public Vector3 teamBBot1SpawnPosition;

    [Tooltip("Spawn point for TeamB Bot 2")]
    public Vector3 teamBBot2SpawnPosition;

    [Tooltip("World rotation all players face at spawn (Y axis only)")]
    public float spawnFacingYRotation;
}
