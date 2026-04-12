using UnityEngine;

public class CubehavenGridSettings : MonoBehaviour
{
    public int width = 24;
    public int height = 18;
    public float tileSize = 1f;
    public Vector3 origin = Vector3.zero;

    // optional: top surface y where you want gizmos/hedges to sit
    public float surfaceY = 1f;
}