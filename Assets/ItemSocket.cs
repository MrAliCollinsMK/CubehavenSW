using UnityEngine;

public class ItemSocket : MonoBehaviour, IInteractable
{
    [Header("Requirement")]
    public CarryType requiredType = CarryType.Key;
    public string requiredItemId = "GoldKey";
    public bool consumeOnUse = true;

    [Header("Output")]
    public LogicBlock[] targets;
    public LogicState successState = LogicState.On;

    [Header("One shot")]
    public bool oneShot = true;
    bool used;

    [Header("Destroy on use")]
    public bool destroyOnUse = true;
    public void Interact(SimplePlayerController player)
    {
        if(player == null) return;
        if(oneShot && used) return;

        if(!player.HasItem(requiredItemId, requiredType))
        {
            // Optional: play "denied" sound
            return;
        }

        if(consumeOnUse)
            player.ConsumeItem(requiredItemId, requiredType);

        for(int i = 0; i < targets.Length; i++)
            if(targets[i] != null) targets[i].SetState(successState);

        used = true;

        if(oneShot && destroyOnUse)
            Destroy(transform.parent.gameObject);
    }
}
