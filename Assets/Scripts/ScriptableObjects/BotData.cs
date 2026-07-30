using UnityEngine;

[CreateAssetMenu(fileName = "Bot", menuName = "Team/Bot Data", order = 1)]
public class BotData : ScriptableObject
{
    [Header("Identity")] 
    [Tooltip("Which team this bot belongs to.")]
    public TeamType team;

    [Header("Movement")]
    [Tooltip("NavMeshAgent movement speed.")]
    public float moveSpeed = 6f;

    [Tooltip("NavMeshAgent angular speed.")]
    public float angularSpeed = 300f;

    [Tooltip("NavMeshAgent acceleration.")]
    public float acceleration = 12f;

    [Header("Catch Settings")]
    [Tooltip("Distance within which bot auto-catches a free disc.")]
    public float catchRadius = 1.8f;
    public float catchVerticalTolerance = 1.5f;

    [Tooltip("Distance within which bot can redirect a moving disc.")]
    public float redirectRadius = 2.5f;

    [Header("Shoot Settings")]
    [Tooltip("Distance from the goal at which the bot decides to shoot.")]
    public float shootRange = 12f;

    [Tooltip("How accurately the bot aims at the goal center. " +
             "0 = perfect, 1 = very inaccurate.")]
    [Range(0f, 1f)]
    public float aimInaccuracy = 0.15f;

    [Header("Pass Settings")]
    [Tooltip("Distance within which the bot looks for a teammate to pass to.")]
    public float passSearchRadius = 18f;

    [Tooltip("Seconds the bot holds the disc before deciding to pass or shoot.")]
    public float holdDecisionTime = 1.2f;

    [Header("Defend Settings")]
    [Tooltip("Distance from own goal the bot defends when the enemy has the disc.")]
    public float defendRadius = 8f;

    [Tooltip("If enemy with disc is within this range, switch to Intercept.")]
    public float interceptTriggerRange = 5f;

    [Header("Dash / Intercept Settings")]
    [Tooltip("Distance within which the bot will attempt a dash at a disc carrier.")]
    public float dashTriggerRange = 3f;

    [Tooltip("Cooldown between bot dash attempts.")]
    public float dashCooldown = 2f;

    [Header("Decision Timings")]
    [Tooltip("How often (seconds) the bot re-evaluates its current FSM state.")]
    public float stateEvaluationInterval = 0.4f;
    
    [Header("Navigation")]
    [Tooltip("Max distance to search for a valid NavMesh point when sampling a chase target " +
             "that may be airborne/elevated (e.g., right after a lofted throw).")]
    public float navMeshSampleDistance = 10f;
    
    [Header("Navigation — Airborne Disc Tracking")]
    [Tooltip("Layer mask for the arena floor, used to project an airborne disc's " +
             "position straight down onto walkable ground for pathfinding.")]
    public LayerMask floorLayerMask;

    [Tooltip("Max ray length when projecting the disc's position down onto the floor. " +
             "Should comfortably exceed the arena's ceiling height.")]
    public float floorProjectionRayLength = 50f;

    [Tooltip("Small tolerance radius used to snap the floor-projected point onto the " +
             "actual NavMesh surface (corrects minor floor mesh irregularities only).")]
    public float navMeshSnapTolerance = 2f;

    [Tooltip("Fallback search radius used ONLY if the floor raycast fails entirely " +
             "(e.g., disc is over a gap/goal mouth with no floor collider beneath it).")]
    public float navMeshFallbackSampleDistance = 10f;

    [Tooltip("How far ahead (seconds) to predict the disc's horizontal movement when " +
             "generating a chase target — creates more aggressive, anticipatory pursuit.")]
    public float chaseLeadTime = 0.3f;
    
    [Header("Navigation — Airborne Disc Tracking")]
    [Tooltip("The world-space Y height of your arena's physical ceiling (or slightly above it). " +
             "Used as a FIXED raycast origin so floor-projection works identically no matter " +
             "how high the disc currently is — removes any height-based tracking threshold.")]
    public float arenaCeilingHeight = 15f;
    
    [Header("Pass Safety Net")]
    [Tooltip("If the bot has held the disc this long with NO valid pass target, NO valid " +
             "shot, and NO pressure trigger, force a safe forward throw anyway. Prevents " +
             "the bot from freezing indefinitely while holding the disc.")]
    public float forcedReleaseTimeout = 3f;
}
