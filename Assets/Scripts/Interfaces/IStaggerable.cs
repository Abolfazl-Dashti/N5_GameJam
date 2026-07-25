using UnityEngine;

public interface IStaggerable
{
    // 'knockBackDirection': worldSpace direction to knock the disc
    void ApplyStagger(Vector3 knockBackDirection, float knockBackForce);
    bool IsStaggered();  // return true if a character is currently staggered
}
