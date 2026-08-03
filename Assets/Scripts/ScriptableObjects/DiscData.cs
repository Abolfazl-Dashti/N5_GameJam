using UnityEngine;

[CreateAssetMenu(fileName = "DiscData", menuName = "DiscData/new Disc", order = 0)]
public class DiscData : ScriptableObject
{
    [Header("Speed Settings")]
    public float throwSpeed = 20f;  // سرعت اولیه دیسک هنگام پرتاب یا پاس
    public float maxSpeed = 40f;  // نهایت سرعتی که دیسک حتی با شتاب گرفتن بر اثر برخورد با دیواره های آرنا میتونه بگیره
    public float spinTorque = 15f;  // میزان چرخش دیسک وقتی پرتاب میشه یا پاس داده میشه
    [Tooltip("Minimum speed of disc when free(not thrown or passed)")] 
    public float minFreeSpeed = 5f;  // حداقل سرعت دیسک وقتی آزاد است تا کند یا متوقف نشود

    [Header("Rebound Settings")]
    [Range(1.0f, 1.5f)] public float wallReboundBoostMultiplier;  // ضریب افزایش سرعت پس از برخورد با دیوارها
    [Range(1.0f, 1.5f)] public float floorReboundBoostMultiplier;  // ضریب افزایش سرعت پس از برخورد با کف
    [Range(1.0f, 1.5f)] public float ceilingReboundBoostMultiplier;  // ضریب افزایش سرعت پس از برخورد با سقف
    
    [Header("Drag & Gravity")]
    public float freeDrag = 0.05f;  // مقاومت هوا وقتی دیسک آزاد است
    public float gravityScale = 0.4f;  // ضریب جاذبه سفارشی برای رفتار پرتابی دیسک (کمتر از جاذبه واقعی پیشفرض Unity)
    
    [Header("Disc Position on player")]
    // وقتی پلیر دیسک رو میگیره در موقعیت زیر نسبت به بدنش دیسک رو نگه میداره
    public Vector3 holdOffset = new Vector3(0f, -0.2f, 0.8f);
    
    // نام لایه هایی که باید در Inspector به دیوار، کف و سقف آرنا اختصاص داده شود. نام وارد شده باید با نام لایه یکی باشد
    [Header("Layer & Tag Names")]
    [Tooltip("Physics Layer name assigned to arena walls")]
    public string wallLayerName = "ArenaWall";

    [Tooltip("Physics Layer name assigned to the arena floor")]
    public string floorLayerName = "ArenaFloor";

    [Tooltip("Physics Layer name assigned to the arena ceil")]
    public string ceilingLayerName = "ArenaCeiling";
}
