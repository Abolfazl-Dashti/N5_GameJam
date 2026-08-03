using UnityEngine;

/// Implemented by PlayerController and BotAIController
public interface IResettable
{
    void ResetToSpawn(Vector3 spawnPosition, Quaternion spawnRotation);
    void FreezePlayer();  // این تابع ورودی ها و حرکت پلیر را قفل میکند
    void UnfreezePlayer();  // این تابع حرکت پلیر را از حالت فریز شده و توقف بازی بعد از گل، خارج میکند
}