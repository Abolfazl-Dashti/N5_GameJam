using UnityEngine;

/// Implement by 'PlayerController' and 'BotAIController'
public interface IStaggerable
{
    // این تابع ضربه زدن را اعمال می‌کند، 'knockBackDirection' جهت پرتاب دیسک بر اثر ضربه برای بازپس گیری دیسک است
    void ApplyStagger(Vector3 knockBackDirection, float knockBackForce);
    
    // اگر بازیکنی تحت ضربه برای گرفتن دیسک قرار گرفت، این تابع مقدار true برمیگرداند
    bool IsStaggered();
}
