using UnityEngine;

/// Match Timing Settings, that used by 'MatchManager'
[CreateAssetMenu(fileName = "MatchData", menuName = "Match/Match Data", order = 2)]
public class MatchData : ScriptableObject
{
    [Header("Match Timer")]
    [Tooltip("Total match duration in seconds")]
    public float matchDuration = 480f;
    
    // زمان انتظار پس از گل تا respawn همه بازیکنان. در این مدت بازی pause است. کمتر، ادامه سریع‌تر بازی و بیشتر، فرصت بیشتر برای نمایش UI گل و انیمیشن
    public float postGoalRespawnDelay = 4f;
    public float centerSpawnFreezeTime = 2f;  // مدت زمانی که بازیکنان پس از spawn در زمین freeze می ‌مانند

    [Header("Spawn Positions")]
    [Tooltip("World position where the disc respawns at center")]
    public Vector3 discSpawnPosition;  // موقعیتی که دیسک پس از هر گل در آن spawn می‌شود

    [Tooltip("World rotation, the disc has at spawn moment")]
    public Vector3 discSpawnRotation = Vector3.zero;

    [Tooltip("Spawn point for the human Player")]
    public Vector3 playerSpawnPosition;

    [Tooltip("Spawn point for the TeamA Bot(teammate of human player)")]
    public Vector3 teamABotSpawnPosition;

    [Tooltip("Spawn point for TeamB Bot 1")]
    public Vector3 teamBBot1SpawnPosition;

    [Tooltip("Spawn point for TeamB Bot 2")]
    public Vector3 teamBBot2SpawnPosition;

    [Tooltip("World rotation all players face at spawn(Y axis only)")]
    public float spawnFacingYRotation;  // زاویه Y که همه بازیکنان هنگام spawn به آن سمت می ‌ایستند
}
