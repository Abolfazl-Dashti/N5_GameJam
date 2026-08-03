using UnityEngine;

[CreateAssetMenu(fileName = "DiscHandler", menuName = "DiscData/DiscHandler", order = 1)]
public class DiscHandlerData : ScriptableObject
{
    [Header("Catch Settings")]
    [Tooltip("Radius of the proximity sphere that auto-catches a disc close to the player")]
    public float catchRadius = 1.8f;  // فاصله ای که باعث میشود دیسک آزاد نزدیک بازیکن به صورت خودکار جذب شود

    [Tooltip("Range of the forward SphereCast used to catch disc the player is facing")]
    public float catchCastRange = 4f;  // فاصله از روبروی بازیکن تا دیسک آزاد که باعث میشود دیسک به صورت خودکار جذب شود

    [Tooltip("Layer mask for the disc object")]
    public LayerMask discLayerMask;
    
    // [Tooltip("Radius of the forward SphereCast")]
    // public float catchCastRadius = 0.6f;

    [Header("Throw Settings")]
    [Tooltip("Minimum throw speed when releasing immediately(no charge)")]
    public float minThrowSpeed = 12f;  // سرعت حداقلی پرتاب دیسک بدون شارژ سرعت

    [Tooltip("Maximum throw speed reached at full charge")]
    public float maxThrowSpeed = 35f;  // حداکثر سرعت دیسک هنگام شارژ کامل سرعت

    [Tooltip("Time in seconds to reach full throw charge from zero")]
    public float maxChargeTime = 1.2f;  // زمانی که برای رسیدن به شارژ کامل با نگه داشتن چپ کلیک نیاز است
    
    [Tooltip("upward angle offset added to throw direction in degrees")]
    public float throwLoftAngle = 3f;  // زاویه انحراف رو به بالا برای قوس دادن به جهت پرتاب دیسک

    [Header("Redirect Settings")]
    [Tooltip("Range which the player can redirect a passing disc")]
    public float redirectRange = 2.5f;

    [Tooltip("The greater the value, the higher the speed after redirect and vice versa")] [Range(0f, 1f)]
    public float redirectSpeedRetention = 0.95f;  // هرچه کمتر، دیسک بعد از redirect کند می‌شود و هرچه بیشتر، سرعت تقریبا بدون افت حفظ می‌شود
    
    [Tooltip("Seconds after a redirect before the player can redirect again")]
    public float redirectCooldown = 0.4f;  // هرچند ثانیه یکبار میتوان redirect کرد؟
}
