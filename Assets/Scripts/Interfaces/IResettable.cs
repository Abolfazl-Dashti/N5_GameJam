// IResettable.cs
// Implemented by PlayerController and BotController.
// MatchManager calls ResetToSpawn() on all characters without knowing their type.
using UnityEngine;

public interface IResettable
{
    /// Teleport this character to its assigned spawn position
    /// and freeze/unfreeze input for the center spawn countdown
    void ResetToSpawn(Vector3 spawnPosition, Quaternion spawnRotation);
    
    /// Freeze all input and movement (called during post-goal pause)
    void FreezePlayer();
    
    /// Restore all input and movement (called when match resumes)
    void UnfreezePlayer();
}