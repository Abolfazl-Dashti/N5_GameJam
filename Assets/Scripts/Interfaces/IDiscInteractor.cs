using UnityEngine;

public interface IDiscInteractor
{
    // Transform the player who interacts with the Disc
    Transform GetTransform();

    // Called by possession system when someone gains the disc
    void OnDiscReceived(DiscController disc);

    // Called by possession system when someone loses the disc
    void OnDiscLost();

    // Returns true if someone currently holds the disc
    bool IsHoldingDisc();
}
