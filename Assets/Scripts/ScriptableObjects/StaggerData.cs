using UnityEngine;

/// Dash/Stagger settings that used by 'PlayerCombat'
[CreateAssetMenu(fileName = "StaggerData", menuName = "Team/Stagger Data", order = 0)]
public class StaggerData : ScriptableObject
{
    [Header("Dash Settings")]
    [Tooltip("Speed of the dash movement")]
    public float dashSpeed = 18f;

    [Tooltip("Duration of the dash movement(in seconds)")]
    public float dashDuration = 0.18f;  // مدت زمان اجرای حرکت dash

    [Tooltip("How often(seconds) can player dash?")]
    public float dashCooldown = 1.2f;  // هرچند ثانیه یکبار میتواند dash بزند؟

    [Tooltip("Layer mask used to detect opponents during dash")]
    public LayerMask opponentLayerMask;  // در Inspector روی Bot تنظیم شود

    [Tooltip("Radius of the OverlapSphere used to detect opponents on dash impact")]
    public float dashHitRadius = 0.8f;  // هرچه بیشتر، برخورد از فاصله بیشتر ثبت میشود و هرچه کمتر، باید دقیقا به حریف برخورد کند
    
    [Header("Stagger Settings")]
    [Tooltip("Duration in seconds that the staggered character cannot move")]
    public float staggerDuration = 1.5f;

    // نیروی وارد‌شده به دیسک هنگام جدا شدن از دست بازیکن staggered که هرچه کمتر باشد دیسک نزدیک همان ‌جا می‌افتد و هرچه بیشتر باشد دیسک با شدت بیشتری پرتاب می‌شود
    [Tooltip("Force applied to the disc when knocked loose by a stagger")]
    public float discKnockBackForce = 8f;

    // باعث می‌شود دیسک به فضای باز پرتاب شود نه فقط زمین. هرچه کمتر، دیسک بیشتر روی زمین می‌ لغزد و هرچه بیشتر، دیسک بالاتر می‌پرد
    [Tooltip("The greater the value, the higher the disc height by a stagger")]
    public float discKnockBackUpward = 4f;

    [Header("Dash Collision Detection")]
    // اگر سرعت dash را بخواهیم افزایش دهیم این متغیر را نیز بهتر است کاهش دهیم
    [Tooltip("How often (in seconds) during a dash we check for opponent collision. Lower = more accurate")]
    public float collisionCheckInterval = 0.03f;
}
