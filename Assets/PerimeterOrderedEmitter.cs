using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ColliderPerimeterEmitter : MonoBehaviour
{
    public BoxCollider sourceCollider;
    public int pointsPerEdge = 12;
    public float emitsPerSecond = 30f;
    public float yOffset = 0.02f;      // lift above surface
    public bool clockwise = true;

    ParticleSystem ps;
    ParticleSystem.EmitParams emitParams;
    Vector3[] points;
    int index;
    float emitAccumulator;

    [Header("Stretch Randomisation")]
    public bool randomiseSpeed = true;
    public Vector2 speedRange = new Vector2(1.5f, 4f);

    public bool randomiseLifetime = true;
    public Vector2 lifetimeRange = new Vector2(0.4f, 1.2f);

    public bool randomiseSize = false;
    public Vector2 sizeRange = new Vector2(0.04f, 0.12f);

    [Header("Hero Streaks")]
    public bool enableHeroStreaks = true;
    [Range(0f, 1f)] public float heroChance = 0.15f;
    public float heroSpeedMultiplier = 1.8f;
    public float heroLifetimeMultiplier = 1.5f;
    public float heroSizeMultiplier = 1.5f;


    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // IMPORTANT
        if(!sourceCollider)
            sourceCollider = GetComponentInParent<BoxCollider>();

        BuildPoints();
    }

    void OnValidate()
    {
        if(Application.isPlaying && sourceCollider)
            BuildPoints();
    }

    void BuildPoints()
    {
        Bounds b = sourceCollider.bounds;

        float minX = b.min.x;
        float maxX = b.max.x;
        float minZ = b.min.z;
        float maxZ = b.max.z;
        float y = b.max.y + yOffset;

        int total = pointsPerEdge * 4;
        points = new Vector3[total];
        int i = 0;

        // Edge 1: front (minZ), left -> right
        for(int p = 0; p < pointsPerEdge; p++)
        {
            float t = p / (pointsPerEdge - 1f);
            points[i++] = new Vector3(Mathf.Lerp(minX, maxX, t), y, minZ);
        }

        // Edge 2: right (maxX), front -> back
        for(int p = 0; p < pointsPerEdge; p++)
        {
            float t = p / (pointsPerEdge - 1f);
            points[i++] = new Vector3(maxX, y, Mathf.Lerp(minZ, maxZ, t));
        }

        // Edge 3: back (maxZ), right -> left
        for(int p = 0; p < pointsPerEdge; p++)
        {
            float t = p / (pointsPerEdge - 1f);
            points[i++] = new Vector3(Mathf.Lerp(maxX, minX, t), y, maxZ);
        }

        // Edge 4: left (minX), back -> front
        for(int p = 0; p < pointsPerEdge; p++)
        {
            float t = p / (pointsPerEdge - 1f);
            points[i++] = new Vector3(minX, y, Mathf.Lerp(maxZ, minZ, t));
        }

        if(!clockwise)
            System.Array.Reverse(points);

        index = 0;
    }

    void Update()
    {
        emitAccumulator += emitsPerSecond * Time.deltaTime;
        int emitCount = Mathf.FloorToInt(emitAccumulator);
        if(emitCount <= 0) return;
        emitAccumulator -= emitCount;

        for(int e = 0; e < emitCount; e++)
        {
            Vector3 worldPos = points[index];
            index = (index + 1) % points.Length;

            emitParams.position = worldPos;

            // Base values
            float speed = randomiseSpeed
                ? Random.Range(speedRange.x, speedRange.y)
                : speedRange.y;

            float lifetime = randomiseLifetime
                ? Random.Range(lifetimeRange.x, lifetimeRange.y)
                : lifetimeRange.y;

            float size = randomiseSize
                ? Random.Range(sizeRange.x, sizeRange.y)
                : sizeRange.y;

            // Hero streak override
            bool hero = enableHeroStreaks && Random.value < heroChance;
            if(hero)
            {
                speed *= heroSpeedMultiplier;
                lifetime *= heroLifetimeMultiplier;
                size *= heroSizeMultiplier;
            }

            emitParams.velocity = Vector3.up * speed;
            emitParams.startLifetime = lifetime;
            emitParams.startSize = size;

            ps.Emit(emitParams, 1);
        }
    }
    void OnDrawGizmosSelected()
    {
        if(points == null) return;
        Gizmos.color = Color.red;
        for(int i = 0; i < points.Length; i++)
            Gizmos.DrawSphere(points[i], 0.03f);
    }
}
