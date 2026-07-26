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
}
