using UnityEngine;

public enum CarryType { Key, Block, Generic }

public class CarryableItem : MonoBehaviour
{
    [Header("Identity")]
    public string itemId = "GoldKey";
    public CarryType type = CarryType.Key;
    public string[] tags;

    [Header("Carry Visual")]
    public bool spinWhileCarried = true;
    public float spinSpeed = 180f;

    [Header("Carry Overrides")]
    public Vector3 carriedLocalRotation = Vector3.zero;
    public Vector3 carriedLocalScale = Vector3.one;

    [HideInInspector] public bool isCarried;

    void Update()
    {
        if(isCarried && spinWhileCarried)
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
    }
}