using UnityEngine;

[CreateAssetMenu(fileName = "DiscHandler", menuName = "DiscData/DiscHandler", order = 1)]
public class DiscHandlerData : ScriptableObject
{
    [Header("Catch Settings")]
    [Tooltip("Radius of the proximity sphere that auto-catches a disc close to the player.")]
    public float catchRadius = 1.8f;

    [Tooltip("Range of the forward SphereCast used to catch discs the player is facing.")]
    public float catchCastRange = 4f;

    [Tooltip("Radius of the forward SphereCast.")]
    public float catchCastRadius = 0.6f;

    [Tooltip("Layer mask for the disc object. Set to your Disc layer only.")]
    public LayerMask discLayerMask;

    [Header("Throw Settings")]
    [Tooltip("Minimum throw speed when releasing immediately (no charge).")]
    public float minThrowSpeed = 12f;

    [Tooltip("Maximum throw speed reached at full charge.")]
    public float maxThrowSpeed = 35f;

    [Tooltip("Time in seconds to reach full throw charge from zero.")]
    public float maxChargeTime = 1.2f;

    [Tooltip("Upward angle offset added to throw direction in degrees. " +
             "Slight upward loft prevents throws from hitting the floor immediately.")]
    public float throwLoftAngle = 3f;

    [Header("Redirect Settings")]
    [Tooltip("Range within which the player can redirect a passing disc.")]
    public float redirectRange = 2.5f;

    [Tooltip("Fraction of disc speed retained after a redirect (0-1).")]
    [Range(0f, 1f)]
    public float redirectSpeedRetention = 0.95f;

    [Header("Cooldowns")]
    [Tooltip("Seconds after a redirect before the player can redirect again.")]
    public float redirectCooldown = 0.4f;
}
