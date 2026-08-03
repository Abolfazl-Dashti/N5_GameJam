using UnityEngine;

/// All Bots using a BotData file
[CreateAssetMenu(fileName = "Bot", menuName = "Team/Bot Data", order = 1)]
public class BotData : ScriptableObject
{
    [Header("Identity")]
    // اگر تیم حریف است در Inspector روی 'TeamB' و اگر با بازیکن انسان هم تیمی هست روی 'TeamA' تنظیم شود
    [Tooltip("Which team this bot belongs to")]
    public TeamType team;

    [Header("NavMeshAgent Settings")]
    public float moveSpeed = 6f;

    [Tooltip("Bot speed to change the direction & Rotation of body")]
    public float angularSpeed = 300f;  // سرعت چرخش بات برای تغییر جهت. هرچقدر بیشتر، چرخش تند و دقیق تر
    [Tooltip("NavMeshAgent acceleration")]
    public float acceleration = 12f; // شتاب بات هنگام شروع حرکت. هرچقدر کمتر، شروع حرکت نرم تر و هرچقدر بیشتر واکنش سریع تر به تغییر 

    [Header("Catch Settings")] 
    // شعاع افقی که بات در آن دیسک آزاد را به‌صورت خودکار جذب میکند
    [Tooltip("Distance for auto-catch the disc(horizontal)")]
    public float catchRadius = 1.8f;
    // ارتفاع عمودی برای گرفتن دیسک آزاد. هرچه کمتر فقط دیسک هم ارتفاع جذب میشود و هرچه بیشتر، دیسک با ارتفاع بالاتر نیز جذب میشود
    [Tooltip("Distance for auto-catch the disc(vertical)")]
    public float catchVerticalTolerance = 1.5f;

    [Header("Redirect Settings")]
    [Tooltip("Distance which bot can redirect a disc")]
    public float redirectRadius = 2.5f;

    [Header("Shoot Settings")]
    [Tooltip("Distance from the goal which the bot can shoot")]
    public float shootRange = 12f;
    [Range(0f, 1f)] public float aimInaccuracy = 0.15f;  // میزان خطا هنگام نشانه گیری برای شوت زدن. صفر یعنی کاملا دقیق، یک یعنی کاملا تصادفی

    [Header("Pass Settings")]
    [Tooltip("Distance which the bot looks for a teammate to pass to")]
    public float passSearchRadius = 18f;  // شعاع جستجو برای یافتن هم‌ تیمی جهت پاس

    [Tooltip("Seconds the bot holds the disc before deciding to pass or shoot")]
    public float holdDecisionTime = 1.2f;  // مدت زمان نگه ‌داشتن دیسک پیش از تصمیم به پاس یا شوت

    // اگر ربات این مدت دیسک را نگه داشت و نه پاس داشت نه شوت نه فشار، یک پرتاب اجباری انجام می‌دهد
    [Tooltip("If the bot has held the disc this long with no valid pass, shoot target, and no pressure trigger, " +
             "force a safe forward throw anyway")]
    public float forcedReleaseTimeout = 3f;
    
    [Header("Defend Settings")]
    [Tooltip("Distance from own goal the bot defends when the enemy has the disc")]
    public float defendRadius = 8f;  // فاصله ای که بات از دروازه میگیره تا ازش دفاع کنه

    [Header("Dash / Intercept Settings")]
    // اگر دشمنی که دیسک را دارد در این فاصله باشد، بات به حالت Intercept می‌رود هرچه بیشتر باشد بات زودتر برای گرفتن دیسک حمله میکند
    [Tooltip("The greater the range, the faster the bot, which identify the enemy")]
    public float interceptTriggerRange = 5f;
    
    [Tooltip("Distance within which the bot will attempt a dash at a disc carrier")]
    public float dashTriggerRange = 3f;  // فاصله ای که بات به نسبت دشمن حامل دیسک اقدام به dash و بازپس گیری دیسک میکند
    
    [Tooltip("Cooldown between bot dash attempts")]
    public float dashCooldown = 2f;  // هرچند ثانیه یکبار میتواند dash بزند؟

    [Header("Decision Timing")]
    [Tooltip("How often(seconds) does the bot re-evaluate its current state")]
    public float stateEvaluationInterval = 0.4f;  // هرچه کمتر، تصمیم گیری سریع تر
    
    [Header("Navigation/Airborne Disc Tracking")]
    public LayerMask floorLayerMask;
    
    [Tooltip("Must be greater than Arena height(gap between floor & ceil)")]
    public float arenaCeilingHeight = 100;
    
    [Tooltip("Small tolerance radius used to snap the floor-projected point onto the " +
             "actual NavMesh surface (corrects minor floor mesh irregularities only)")]
    public float navMeshSnapTolerance = 2f;
    
    [Tooltip("Fallback search radius used only if the floor raycast fails entirely" +
             "(e.g. disc is over a goal mouth with no floor collider beneath it)")]
    public float navMeshFallbackSampleDistance = 10f;
    
    // پیش‌بینی موقعیت آینده دیسک برای دنبال کردن آن. هرچه کمتر، ربات مستقیم به موقعیت فعلی دیسک می‌رود وهرچه بیشتر، ربات جلوتر از دیسک می‌رود
    [Tooltip("How far ahead(seconds) to predict the disc's horizontal movement")]
    public float chaseLeadTime = 0.3f;
    
    // public float floorProjectionRayLength = 50f;
    
    // [Tooltip("Max distance to search for a valid NavMesh point when sampling a chase target")]
    // public float navMeshSampleDistance = 10f;
}
