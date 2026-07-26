using UnityEngine;

[CreateAssetMenu(fileName = "StaggerData", menuName = "Team/Stagger Data", order = 0)]
public class StaggerData : ScriptableObject
{
    [Header("Dash Settings")]
    [Tooltip("Speed of the dash movement burst.")]
    public float dashSpeed = 18f;

    [Tooltip("Duration of the dash movement in seconds.")]
    public float dashDuration = 0.18f;

    [Tooltip("Cooldown in seconds before the player can dash again.")]
    public float dashCooldown = 1.2f;

    [Tooltip("Layer mask used to detect opponents during dash collision.")]
    public LayerMask opponentLayerMask;

    [Tooltip("Radius of the OverlapSphere used to detect opponents on dash impact.")]
    public float dashHitRadius = 0.8f;
    
    [Header("Stagger Settings")]
    [Tooltip("Duration in seconds that the staggered character cannot move.")]
    public float staggerDuration = 1.5f;

    [Tooltip("Force applied to the disc when knocked loose by a stagger.")]
    public float discKnockBackForce = 8f;

    [Tooltip("Upward component added to disc knockback so it pops into open space.")]
    public float discKnockBackUpward = 4f;

    [Header("Dash Collision Detection")]
    [Tooltip("How often (in seconds) during a dash we check for opponent collision. Lower = more accurate")]
    public float collisionCheckInterval = 0.03f;
}
