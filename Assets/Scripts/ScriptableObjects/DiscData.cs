using UnityEngine;

[CreateAssetMenu(fileName = "DiscData", menuName = "DiscData/new Disc", order = 0)]
public class DiscData : ScriptableObject
{
    [Header("Speed Settings")]
    [Tooltip("Speed when the disc is thrown or passed")]
    public float throwSpeed = 20f;

    [Tooltip("Absolute maximum speed the disc is ever allowed to reach.")]
    public float maxSpeed = 40f;

    [Tooltip("Minimum speed the disc must have when free(not thrown or passed)")]
    public float minFreeSpeed = 5f;

    [Header("Rebound Settings")]
    [Range(1.0f, 1.5f)] public float wallReboundBoostMultiplier = 1.12f;
    [Range(1.0f, 1.5f)] public float floorReboundBoostMultiplier = 1.06f;
    [Range(1.0f, 1.5f)] public float ceilingReboundBoostMultiplier = 1.10f;

    [Tooltip("angular spin for disc when thrown or passed")]
    public float spinTorque = 15f;

    [Header("Drag & Gravity")]
    public float freeDrag = 0.05f;
    public float gravityScale = 0.4f;

    [Header("Hold Offset")]
    [Tooltip("Local offset from the holder's camera/hand position where the disc sits.")]
    public Vector3 holdOffset = new Vector3(0f, -0.2f, 0.8f);

    [Header("Layer & Tag Names")]
    [Tooltip("Physics Layer name assigned to arena walls.")]
    public string wallLayerName = "ArenaWall";

    [Tooltip("Physics Layer name assigned to the arena floor.")]
    public string floorLayerName = "ArenaFloor";

    [Tooltip("Physics Layer name assigned to the arena ceiling.")]
    public string ceilingLayerName = "ArenaCeiling";
}
