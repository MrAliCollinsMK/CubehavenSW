#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;

public class CubehavenDioramaStamper : EditorWindow
{
    // ===== UI =====
    Vector2 scroll;

    [Header("Diorama")]
    GameObject topTilePrefab;          // floor tile prefab
    GameObject pillarPrefab;           // 1x1 block for vertical supports (optional)
    Transform parent;
    string rootName = "GeneratedDiorama";

    int width = 8;
    int depth = 8;
    float tileSize = 1f;
    Vector3 origin = Vector3.zero;

    [Header("Heights")]
    int baseHeight = 0;                // sea level in "steps"
    int maxHeight = 4;                 // cap height for random gen / paint
    bool buildPillarsToBase = true;    // stack pillar blocks down to baseHeight
    bool clearOnBuild = true;

    [Header("Procedural Height")]
    bool useProcedural = false;
    int seed = 12345;
    bool randomizeSeedEachBuild = true;
    [Range(0f, 1f)] float noiseScale = 0.25f;
    [Range(0f, 1f)] float jaggedness = 0.45f; // higher = more spiky
    int smoothPasses = 2;

    [Header("Paint Mode (Scene View)")]
    bool paintMode = false;
    int brushRadius = 0;
    int paintDelta = 1;                // raise/lower per click
    bool holdShiftToLower = true;
    bool showOverlay = true;
    Color overlayLow = new Color(0.2f, 0.9f, 1f, 0.15f);
    Color overlayHigh = new Color(1f, 0.4f, 0.2f, 0.15f);
    Color outline = new Color(1f, 1f, 1f, 0.35f);

    // ===== HOLES =====
    [Header("Holes / Void Cutter")]
    bool holePaintMode = false;        // when true, SceneView paint edits occupancy instead of heights
    bool paintErase = true;            // true = erase tiles (make holes), false = paint tiles back
    bool showHoleOverlay = true;
    Color holeOverlayFilled = new Color(0f, 1f, 0f, 0.18f);
    Color holeOverlayEmpty = new Color(1f, 0f, 0f, 0.18f);

    [Header("Procedural Holes (Optional)")]
    bool createProceduralHoles = false;
    bool randomizeHoleSeedEachBuild = true;
    int holeSeed = 12345;
    [Range(0f, 0.9f)] float holeChance = 0.25f; // chance to REMOVE a tile
    bool smoothHoles = true;
    int smoothHolePasses = 3;

    bool keepBorderSolid = true;
    int borderThickness = 1;

    // ===== HOLE FILLS =====
    [Header("Hole Fills (Optional)")]
    bool buildHoleFills = false;
    GameObject holeFillPrefab;
    float holeFillYOffset = 0f;     // tweak if your cube pivot isn't centered
    bool clearHoleFillsOnRebuild = true;

    bool ensureConnectedToStart = false;
    Vector2Int startCell = new Vector2Int(1, 1);

    // ===== FENCES =====
    [Header("Perimeter Fences (Optional)")]
    bool buildFences = false;
    bool fencesOnlyOuterPerimeter = true;   // false = also edges around holes
    GameObject[] fencePrefabs;
    float fenceYOffset = 1f;
    float fenceOutwardOffset = 0.0f;        // push slightly outward if needed
    float fenceEdgeInset = 0.25f;           // inset into tile so fence doesn't hang over edge
    float fenceRandomYawJitter = 0f;        // e.g. 2-5 degrees
    bool clearFencesOnRebuild = true;

    // ===== Data =====
    int[,] heights;                    // [x,z] in "steps"
    bool[,] occupancy;                 // [x,z] tile present? (holes)

    // ===== Menu =====
    [MenuItem("Tools/Cubehaven/Diorama Stamper")]
    public static void ShowWindow() => GetWindow<CubehavenDioramaStamper>("Diorama Stamper");

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EnsureArrays();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void EnsureArrays()
    {
        if(heights == null || heights.GetLength(0) != width || heights.GetLength(1) != depth)
            heights = new int[width, depth];

        if(occupancy == null || occupancy.GetLength(0) != width || occupancy.GetLength(1) != depth)
        {
            occupancy = new bool[width, depth];
            for(int z = 0; z < depth; z++)
                for(int x = 0; x < width; x++)
                    occupancy[x, z] = true;
        }

        // clamp existing heights
        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
                heights[x, z] = Mathf.Clamp(heights[x, z], baseHeight, maxHeight);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Cubehaven Diorama Stamper", EditorStyles.boldLabel);

        topTilePrefab = (GameObject)EditorGUILayout.ObjectField("Top Tile Prefab", topTilePrefab, typeof(GameObject), false);
        pillarPrefab = (GameObject)EditorGUILayout.ObjectField("Pillar Prefab (optional)", pillarPrefab, typeof(GameObject), false);
        parent = (Transform)EditorGUILayout.ObjectField("Parent (optional)", parent, typeof(Transform), true);
        rootName = EditorGUILayout.TextField("Root Name", rootName);

        EditorGUILayout.Space(8);
        width = Mathf.Max(1, EditorGUILayout.IntField("Width (tiles)", width));
        depth = Mathf.Max(1, EditorGUILayout.IntField("Depth (tiles)", depth));
        tileSize = Mathf.Max(0.01f, EditorGUILayout.FloatField("Tile Size", tileSize));
        origin = EditorGUILayout.Vector3Field("Origin", origin);

        EditorGUILayout.Space(8);
        baseHeight = EditorGUILayout.IntField("Base Height (steps)", baseHeight);
        maxHeight = Mathf.Max(baseHeight, EditorGUILayout.IntField("Max Height (steps)", maxHeight));
        buildPillarsToBase = EditorGUILayout.Toggle("Build Pillars To Base", buildPillarsToBase);
        clearOnBuild = EditorGUILayout.Toggle("Clear On Build", clearOnBuild);

        // ===== HEIGHT GEN =====
        EditorGUILayout.Space(10);
        GUILayout.Label("Procedural Height", EditorStyles.boldLabel);
        useProcedural = EditorGUILayout.Toggle("Use Procedural Height", useProcedural);
        using(new EditorGUI.DisabledScope(!useProcedural))
        {
            randomizeSeedEachBuild = EditorGUILayout.Toggle("Randomize Seed", randomizeSeedEachBuild);
            using(new EditorGUI.DisabledScope(randomizeSeedEachBuild))
                seed = EditorGUILayout.IntField("Seed", seed);

            noiseScale = EditorGUILayout.Slider("Noise Scale", noiseScale, 0.01f, 1f);
            jaggedness = EditorGUILayout.Slider("Jaggedness", jaggedness, 0f, 1f);
            smoothPasses = EditorGUILayout.IntSlider("Smooth Passes", smoothPasses, 0, 8);

            if(GUILayout.Button("Generate Heights"))
            {
                EnsureArrays();
                GenerateHeights();
                Repaint();
                SceneView.RepaintAll();
            }
        }

        // ===== HOLES =====
        EditorGUILayout.Space(12);
        GUILayout.Label("Holes / Void Cutter", EditorStyles.boldLabel);

        holePaintMode = EditorGUILayout.Toggle("Hole Paint Mode (Scene View)", holePaintMode);
        using(new EditorGUI.DisabledScope(!holePaintMode))
        {
            paintErase = EditorGUILayout.Toggle("Erase (unchecked = Paint)", paintErase);
            showHoleOverlay = EditorGUILayout.Toggle("Show Hole Overlay", showHoleOverlay);

            EditorGUILayout.HelpBox(
                "Scene view: Left click/drag to paint holes.\nHold Shift to invert Erase/Paint.",
                MessageType.Info
            );

            if(GUILayout.Button("Fill All Tiles (No Holes)"))
                InitOccupancyFillAll();
        }

        createProceduralHoles = EditorGUILayout.Toggle("Create Procedural Holes", createProceduralHoles);
        using(new EditorGUI.DisabledScope(!createProceduralHoles))
        {
            randomizeHoleSeedEachBuild = EditorGUILayout.Toggle("Randomize Hole Seed", randomizeHoleSeedEachBuild);
            using(new EditorGUI.DisabledScope(randomizeHoleSeedEachBuild))
                holeSeed = EditorGUILayout.IntField("Hole Seed", holeSeed);

            holeChance = EditorGUILayout.Slider("Hole Chance", holeChance, 0f, 0.9f);

            smoothHoles = EditorGUILayout.Toggle("Smooth Holes", smoothHoles);
            using(new EditorGUI.DisabledScope(!smoothHoles))
                smoothHolePasses = EditorGUILayout.IntSlider("Smooth Passes", smoothHolePasses, 0, 10);

            keepBorderSolid = EditorGUILayout.Toggle("Keep Border Solid", keepBorderSolid);
            using(new EditorGUI.DisabledScope(!keepBorderSolid))
                borderThickness = EditorGUILayout.IntSlider("Border Thickness", borderThickness, 0, 6);

            ensureConnectedToStart = EditorGUILayout.Toggle("Ensure Connected To Start", ensureConnectedToStart);
            using(new EditorGUI.DisabledScope(!ensureConnectedToStart))
                startCell = EditorGUILayout.Vector2IntField("Start Cell", startCell);

            if(GUILayout.Button("Generate Holes"))
            {
                EnsureArrays();
                GenerateProceduralOccupancy();
                if(ensureConnectedToStart) KeepOnlyConnectedRegion(startCell);
                Repaint();
                SceneView.RepaintAll();
            }
        }
        // ===== HOLE FILLS =====
        EditorGUILayout.Space(10);
        GUILayout.Label("Hole Fills (Optional)", EditorStyles.boldLabel);

        buildHoleFills = EditorGUILayout.Toggle("Build Hole Fills", buildHoleFills);
        using(new EditorGUI.DisabledScope(!buildHoleFills))
        {
            holeFillPrefab = (GameObject)EditorGUILayout.ObjectField("Hole Fill Prefab", holeFillPrefab, typeof(GameObject), false);
            holeFillYOffset = EditorGUILayout.FloatField("Fill Y Offset", holeFillYOffset);
            clearHoleFillsOnRebuild = EditorGUILayout.Toggle("Clear Old Hole Fills", clearHoleFillsOnRebuild);

            using(new EditorGUI.DisabledScope(holeFillPrefab == null))
            {
                if(GUILayout.Button("Build/Refresh Hole Fills From Map"))
                {
                    Transform root = GetOrCreateRoot();
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();

                    BuildHoleFillsFromOccupancy(root);

                    Undo.CollapseUndoOperations(group);
                    Selection.activeObject = root.gameObject;
                }
            }
        }
        // ===== FENCES =====
        EditorGUILayout.Space(12);
        GUILayout.Label("Perimeter Fences (Optional)", EditorStyles.boldLabel);

        buildFences = EditorGUILayout.Toggle("Build Fences", buildFences);
        using(new EditorGUI.DisabledScope(!buildFences))
        {
            fencesOnlyOuterPerimeter = EditorGUILayout.Toggle("Only Outer Perimeter", fencesOnlyOuterPerimeter);
            DrawFenceArray();

            fenceYOffset = EditorGUILayout.FloatField("Fence Y Offset", fenceYOffset);
            fenceOutwardOffset = EditorGUILayout.FloatField("Outward Offset", fenceOutwardOffset);
            fenceEdgeInset = EditorGUILayout.FloatField("Edge Inset (into tile)", fenceEdgeInset);
            fenceRandomYawJitter = EditorGUILayout.Slider("Yaw Jitter", fenceRandomYawJitter, 0f, 15f);
            clearFencesOnRebuild = EditorGUILayout.Toggle("Clear Old Fences", clearFencesOnRebuild);

            if(GUILayout.Button("Build/Refresh Fences From Map"))
            {
                Transform root = GetOrCreateRoot();
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();

                BuildFencesFromOccupancy(root);

                Undo.CollapseUndoOperations(group);
                Selection.activeObject = root.gameObject;
            }
        }

        // ===== PAINT MODE =====
        EditorGUILayout.Space(10);
        GUILayout.Label("Height Paint Mode", EditorStyles.boldLabel);
        paintMode = EditorGUILayout.Toggle("Paint Heights in Scene", paintMode);
        using(new EditorGUI.DisabledScope(!paintMode))
        {
            brushRadius = EditorGUILayout.IntSlider("Brush Radius", brushRadius, 0, 6);
            paintDelta = EditorGUILayout.IntSlider("Delta Per Click", paintDelta, 1, 4);
            holdShiftToLower = EditorGUILayout.Toggle("Hold Shift To Lower", holdShiftToLower);
            showOverlay = EditorGUILayout.Toggle("Show Height Overlay", showOverlay);

            EditorGUILayout.HelpBox(
                "Scene view: Left click/drag to raise. Hold Shift to lower.",
                MessageType.Info
            );
        }

        // ===== BUILD =====
        EditorGUILayout.Space(12);

        using(new EditorGUI.DisabledScope(topTilePrefab == null))
        {
            if(GUILayout.Button("Build Diorama (Tiles + Pillars + Fences)"))
            {
                EnsureArrays();
                if(useProcedural) GenerateHeights();

                if(createProceduralHoles)
                {
                    GenerateProceduralOccupancy();
                    if(ensureConnectedToStart) KeepOnlyConnectedRegion(startCell);
                }

                BuildDiorama();
            }
        }

        if(GUILayout.Button("Clear Diorama Root"))
            ClearRoot();

        EditorGUILayout.EndScrollView();

        EnsureArrays();
    }

    // ===== Scene View Painting =====
    void OnSceneGUI(SceneView sv)
    {
        EnsureArrays();

        Event e = Event.current;
        if(e == null) return;

        // overlays
        if(showOverlay)
            DrawHeightOverlay();

        if(showHoleOverlay)
            DrawHoleOverlay();

        // Hole paint
        if(holePaintMode)
        {
            if(e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
            if(e.button != 0) return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if(!TryGetMouseCell(out Vector2Int cell)) return;

            bool eraser = paintErase;
            if(e.shift) eraser = !eraser;

            ApplyHoleBrush(cell.x, cell.y, brushRadius, eraser);

            e.Use();
            sv.Repaint();
            Repaint();
            return;
        }

        // Height paint
        if(!paintMode) return;

        if(e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
        if(e.button != 0) return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if(!TryGetMouseCell(out Vector2Int cell2)) return;

        bool lower = holdShiftToLower && e.shift;
        int delta = lower ? -paintDelta : paintDelta;

        ApplyHeightBrush(cell2.x, cell2.y, brushRadius, delta);

        e.Use();
        sv.Repaint();
        Repaint();
    }

    bool TryGetMouseCell(out Vector2Int cell)
    {
        cell = default;
        Ray r = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

        Plane p = new Plane(Vector3.up, new Vector3(0f, origin.y, 0f));
        if(!p.Raycast(r, out float enter)) return false;
        Vector3 world = r.GetPoint(enter);

        Vector3 local = world - origin;
        int x = Mathf.FloorToInt(local.x / tileSize);
        int z = Mathf.FloorToInt(local.z / tileSize);

        if(x < 0 || z < 0 || x >= width || z >= depth) return false;

        cell = new Vector2Int(x, z);
        return true;
    }

    void ApplyHeightBrush(int cx, int cz, int radius, int delta)
    {
        for(int dz = -radius; dz <= radius; dz++)
            for(int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx;
                int z = cz + dz;
                if(x < 0 || z < 0 || x >= width || z >= depth) continue;

                heights[x, z] = Mathf.Clamp(heights[x, z] + delta, baseHeight, maxHeight);
            }
    }

    void ApplyHoleBrush(int cx, int cz, int radius, bool erase)
    {
        for(int dz = -radius; dz <= radius; dz++)
            for(int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx;
                int z = cz + dz;
                if(x < 0 || z < 0 || x >= width || z >= depth) continue;

                occupancy[x, z] = !erase;
            }
    }

    // ===== Overlays =====
    void DrawHeightOverlay()
    {
        if(!showOverlay) return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
            {
                float t = (maxHeight == baseHeight) ? 0f : Mathf.InverseLerp(baseHeight, maxHeight, heights[x, z]);
                Color fill = Color.Lerp(overlayLow, overlayHigh, t);

                Vector3 c = CellCenterWorld(x, z);
                c.y = origin.y + 0.03f;

                Handles.DrawSolidRectangleWithOutline(
                    RectVerts(c, tileSize * 0.95f),
                    fill,
                    outline
                );

                Handles.color = new Color(1f, 1f, 1f, 0.7f);
                Handles.Label(c + new Vector3(-tileSize * 0.35f, 0f, -tileSize * 0.35f), heights[x, z].ToString());
            }
    }

    void DrawHoleOverlay()
    {
        if(occupancy == null) return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        float y = origin.y + 0.05f;

        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
            {
                Color col = occupancy[x, z] ? holeOverlayFilled : holeOverlayEmpty;
                Handles.color = col;

                Vector3 c = CellCenterWorld(x, z);
                c.y = y;

                Vector3 size = new Vector3(tileSize * 0.95f, 0.01f, tileSize * 0.95f);
                Handles.DrawWireCube(c, size);
            }
    }

    Vector3[] RectVerts(Vector3 center, float side)
    {
        float h = side * 0.5f;
        return new[]
        {
            new Vector3(center.x - h, center.y, center.z - h),
            new Vector3(center.x - h, center.y, center.z + h),
            new Vector3(center.x + h, center.y, center.z + h),
            new Vector3(center.x + h, center.y, center.z - h),
        };
    }

    // ===== Procedural Height =====
    void GenerateHeights()
    {
        if(randomizeSeedEachBuild)
            seed = System.DateTime.Now.Millisecond + Random.Range(0, 999999);

        Random.InitState(seed);

        float ox = Random.Range(-9999f, 9999f);
        float oz = Random.Range(-9999f, 9999f);

        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
            {
                float nx = (x + ox) * noiseScale;
                float nz = (z + oz) * noiseScale;

                float n = Mathf.PerlinNoise(nx, nz);
                n = Mathf.Lerp(n, Mathf.Round(n), jaggedness);

                int h = Mathf.RoundToInt(Mathf.Lerp(baseHeight, maxHeight, n));
                heights[x, z] = Mathf.Clamp(h, baseHeight, maxHeight);
            }

        for(int i = 0; i < smoothPasses; i++)
            SmoothHeightOnce();
    }

    void SmoothHeightOnce()
    {
        int[,] next = new int[width, depth];

        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
            {
                int sum = 0;
                int count = 0;

                for(int dz = -1; dz <= 1; dz++)
                    for(int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx;
                        int zz = z + dz;
                        if(xx < 0 || zz < 0 || xx >= width || zz >= depth) continue;

                        sum += heights[xx, zz];
                        count++;
                    }

                int avg = Mathf.RoundToInt((float)sum / Mathf.Max(1, count));
                next[x, z] = Mathf.Clamp(avg, baseHeight, maxHeight);
            }

        heights = next;
    }

    // ===== Procedural Holes =====
    void InitOccupancyFillAll()
    {
        occupancy = new bool[width, depth];
        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
                occupancy[x, z] = true;
    }

    void GenerateProceduralOccupancy()
    {
        if(occupancy == null || occupancy.GetLength(0) != width || occupancy.GetLength(1) != depth)
            InitOccupancyFillAll();

        if(randomizeHoleSeedEachBuild)
            holeSeed = System.DateTime.Now.Millisecond + Random.Range(0, 999999);

        Random.InitState(holeSeed);

        // start filled
        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
                occupancy[x, z] = true;

        // carve
        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
            {
                if(keepBorderSolid && IsInBorder(x, z, borderThickness))
                {
                    occupancy[x, z] = true;
                    continue;
                }

                bool makeHole = Random.value < holeChance;
                occupancy[x, z] = !makeHole;
            }

        if(smoothHoles)
        {
            for(int pass = 0; pass < smoothHolePasses; pass++)
                SmoothHolesOnce();
        }
    }

    bool IsInBorder(int x, int z, int b)
    {
        return x < b || z < b || x >= (width - b) || z >= (depth - b);
    }

    void SmoothHolesOnce()
    {
        bool[,] next = new bool[width, depth];

        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
            {
                if(keepBorderSolid && IsInBorder(x, z, borderThickness))
                {
                    next[x, z] = true;
                    continue;
                }

                int filled = CountFilledNeighbors8(x, z);

                // classic thresholds (tweak to taste)
                if(filled >= 5) next[x, z] = true;
                else if(filled <= 3) next[x, z] = false;
                else next[x, z] = occupancy[x, z];
            }

        occupancy = next;
    }

    int CountFilledNeighbors8(int cx, int cz)
    {
        int c = 0;
        for(int z = cz - 1; z <= cz + 1; z++)
            for(int x = cx - 1; x <= cx + 1; x++)
            {
                if(x == cx && z == cz) continue;
                if(x < 0 || z < 0 || x >= width || z >= depth) continue;
                if(occupancy[x, z]) c++;
            }
        return c;
    }

    void KeepOnlyConnectedRegion(Vector2Int start)
    {
        if(start.x < 0 || start.y < 0 || start.x >= width || start.y >= depth)
            return;

        if(!occupancy[start.x, start.y])
        {
            if(!TryFindNearestFilled(start, out start))
                return;
        }

        bool[,] visited = new bool[width, depth];
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(start);
        visited[start.x, start.y] = true;

        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };

        while(q.Count > 0)
        {
            var p = q.Dequeue();

            for(int i = 0; i < 4; i++)
            {
                int nx = p.x + dx[i];
                int nz = p.y + dz[i];

                if(nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
                if(visited[nx, nz]) continue;
                if(!occupancy[nx, nz]) continue;

                visited[nx, nz] = true;
                q.Enqueue(new Vector2Int(nx, nz));
            }
        }

        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
                occupancy[x, z] = visited[x, z];
    }

    bool TryFindNearestFilled(Vector2Int from, out Vector2Int found)
    {
        found = from;
        int bestDist = int.MaxValue;
        bool ok = false;

        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
            {
                if(!occupancy[x, z]) continue;

                int d = Mathf.Abs(x - from.x) + Mathf.Abs(z - from.y);
                if(d < bestDist)
                {
                    bestDist = d;
                    found = new Vector2Int(x, z);
                    ok = true;
                }
            }

        return ok;
    }

    // ===== Building =====
    void BuildDiorama()
    {
        Transform root = GetOrCreateRoot();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        if(clearOnBuild)
        {
            for(int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        }

        Transform tilesRoot = GetOrCreateChild(root, "TopTiles");
        Transform pillarsRoot = GetOrCreateChild(root, "Pillars");

        if(clearOnBuild)
        {
            ClearChildren(tilesRoot);
            ClearChildren(pillarsRoot);
        }

        // Spawn top tiles and optional pillars
        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
            {
                if(occupancy != null && !occupancy[x, z]) continue;

                int h = heights[x, z];
                Vector3 topPos = CellCenterWorld(x, z);
                topPos.y = origin.y + (h * tileSize);

                // Top tile
                {
                    GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(topTilePrefab);
                    Undo.RegisterCreatedObjectUndo(tile, "Create Top Tile");
                    tile.transform.SetParent(tilesRoot, true);
                    tile.transform.position = topPos;
                    tile.transform.rotation = Quaternion.identity;
                    tile.name = $"Top_{x}_{z}_h{h}";
                }

                // Pillars (stack down to baseHeight)
                if(buildPillarsToBase && pillarPrefab != null)
                {
                    for(int yy = baseHeight; yy < h; yy++)
                    {
                        Vector3 p = CellCenterWorld(x, z);
                        p.y = origin.y + (yy * tileSize);

                        GameObject pil = (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab);
                        Undo.RegisterCreatedObjectUndo(pil, "Create Pillar");
                        pil.transform.SetParent(pillarsRoot, true);
                        pil.transform.position = p;
                        pil.transform.rotation = Quaternion.identity;
                        pil.name = $"Pillar_{x}_{z}_y{yy}";
                    }
                }
            }

        // Fences
        if(buildFences)
            BuildFencesFromOccupancy(root);

        // Hole fills
        if(buildHoleFills && holeFillPrefab != null)
            BuildHoleFillsFromOccupancy(root);

        // Fences
        if(buildFences)
            BuildFencesFromOccupancy(root);

        Undo.CollapseUndoOperations(group);
        Selection.activeObject = root.gameObject;
    }
    void BuildHoleFillsFromOccupancy(Transform root)
    {
        if(occupancy == null) return;
        if(holeFillPrefab == null) return;

        Transform fillsRoot = root.Find("HoleFills");
        if(fillsRoot == null)
        {
            GameObject go = new GameObject("HoleFills");
            Undo.RegisterCreatedObjectUndo(go, "Create HoleFills Root");
            go.transform.SetParent(root, true);
            fillsRoot = go.transform;
        }
        else if(clearHoleFillsOnRebuild)
        {
            ClearChildren(fillsRoot);
        }

        int spawned = 0;

        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
            {
                // hole cell
                if(occupancy[x, z]) continue;

                Vector3 pos = CellCenterWorld(x, z);

                // Put fills at base height (so a hole becomes "plugged" at the bottom).
                // If you want them at sea-level/top, change baseHeight -> something else.
                pos.y = origin.y + (baseHeight * tileSize) + holeFillYOffset;

                // Robust instantiate
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(holeFillPrefab);
                if(go == null)
                    go = (GameObject)UnityEngine.Object.Instantiate(holeFillPrefab);

                if(go == null)
                {
                    Debug.LogWarning($"[DioramaStamper] Failed to instantiate holeFillPrefab: {holeFillPrefab.name}");
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(go, "Create Hole Fill");

                go.transform.SetParent(fillsRoot, true);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.identity;
                go.name = $"HoleFill_{x}_{z}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";

                spawned++;
            }

        Debug.Log($"[DioramaStamper] Hole fills spawned: {spawned}");
    }
    void BuildFencesFromOccupancy(Transform root)
    {
        if(occupancy == null) return;
        if(fencePrefabs == null || fencePrefabs.Length == 0) return;

        Transform fencesRoot = root.Find("Fences");
        if(fencesRoot == null)
        {
            GameObject go = new GameObject("Fences");
            Undo.RegisterCreatedObjectUndo(go, "Create Fences Root");
            go.transform.SetParent(root, true);
            fencesRoot = go.transform;
        }
        else if(clearFencesOnRebuild)
        {
            ClearChildren(fencesRoot);
        }

        bool Has(int x, int z)
        {
            if(x < 0 || z < 0 || x >= width || z >= depth) return false;
            return occupancy[x, z];
        }

        bool IsOuterEdge(int nx, int nz)
        {
            // outer perimeter only = neighbor is out-of-bounds (not an in-bounds hole)
            return (nx < 0 || nz < 0 || nx >= width || nz >= depth);
        }

        GameObject PickFence()
        {
            for(int tries = 0; tries < 10; tries++)
            {
                int i = Random.Range(0, fencePrefabs.Length);
                if(fencePrefabs[i] != null) return fencePrefabs[i];
            }
            for(int i = 0; i < fencePrefabs.Length; i++)
                if(fencePrefabs[i] != null) return fencePrefabs[i];
            return null;
        }

        int spawned = 0;

        void SpawnFence(int x, int z, Vector3 edgeCenter, Vector3 outwardDir, string dirTag)
        {
            GameObject prefab = PickFence();
            if(prefab == null) return;

            Vector3 dir = outwardDir.normalized;

            Vector3 pos = edgeCenter;
            pos -= dir * fenceEdgeInset;
            pos += dir * fenceOutwardOffset;

            float tileTopY = origin.y + (heights[x, z] * tileSize);
            pos.y = tileTopY + fenceYOffset;

            Quaternion rot = Quaternion.LookRotation(outwardDir, Vector3.up);
            rot *= Quaternion.Euler(0f, 180f, 0f);

            if(fenceRandomYawJitter > 0f)
                rot *= Quaternion.Euler(0f, Random.Range(-fenceRandomYawJitter, fenceRandomYawJitter), 0f);

            // ✅ Robust instantiate: try prefab asset instantiate, else fallback to normal Instantiate
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if(go == null)
                go = (GameObject)UnityEngine.Object.Instantiate(prefab);

            if(go == null)
            {
                Debug.LogWarning($"[DioramaStamper] Failed to instantiate fence prefab: {prefab.name}");
                return;
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Fence");

            go.transform.SetParent(fencesRoot, true);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.name = $"Fence_{x}_{z}_{dirTag}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            spawned++;
        }


        // for each occupied tile, if neighbor missing -> fence on that edge
        for(int z = 0; z < depth; z++)
            for(int x = 0; x < width; x++)
            {
                if(!Has(x, z)) continue;

                Vector3 tileCenter = CellCenterWorld(x, z);
                float tileTopY = origin.y + (heights[x, z] * tileSize);
                tileCenter.y = tileTopY;

                // West
                if(!Has(x - 1, z))
                {
                    if(!fencesOnlyOuterPerimeter || IsOuterEdge(x - 1, z))
                    {
                        Vector3 c = tileCenter + new Vector3(-tileSize * 0.5f, 0f, 0f);
                        SpawnFence(x, z, c, Vector3.left, "W");
                    }
                }

                // East
                if(!Has(x + 1, z))
                {
                    if(!fencesOnlyOuterPerimeter || IsOuterEdge(x + 1, z))
                    {
                        Vector3 c = tileCenter + new Vector3(tileSize * 0.5f, 0f, 0f);
                        SpawnFence(x, z, c, Vector3.right, "E");
                    }
                }

                // South
                if(!Has(x, z - 1))
                {
                    if(!fencesOnlyOuterPerimeter || IsOuterEdge(x, z - 1))
                    {
                        Vector3 c = tileCenter + new Vector3(0f, 0f, -tileSize * 0.5f);
                        SpawnFence(x, z, c, Vector3.back, "S");
                    }
                }

                // North
                if(!Has(x, z + 1))
                {
                    if(!fencesOnlyOuterPerimeter || IsOuterEdge(x, z + 1))
                    {
                        Vector3 c = tileCenter + new Vector3(0f, 0f, tileSize * 0.5f);
                        SpawnFence(x, z, c, Vector3.forward, "N");
                    }
                }
            }
        Debug.Log($"[DioramaStamper] Fences spawned: {spawned}");
    }

    void ClearRoot()
    {
        Transform root = FindRoot();
        if(root == null) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        for(int i = root.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

        Undo.CollapseUndoOperations(group);
    }

    Transform GetOrCreateRoot()
    {
        Transform t = FindRoot();
        if(t != null) return t;

        GameObject go = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(go, "Create Diorama Root");

        if(parent != null)
            go.transform.SetParent(parent, true);

        go.transform.position = Vector3.zero;
        return go.transform;
    }

    Transform FindRoot()
    {
        if(parent != null)
        {
            Transform t = parent.Find(rootName);
            if(t != null) return t;
        }

        GameObject found = GameObject.Find(rootName);
        return found != null ? found.transform : null;
    }

    Transform GetOrCreateChild(Transform root, string name)
    {
        Transform t = root.Find(name);
        if(t != null) return t;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name} Root");
        go.transform.SetParent(root, true);
        return go.transform;
    }

    void ClearChildren(Transform t)
    {
        if(t == null) return;
        for(int i = t.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(t.GetChild(i).gameObject);
    }

    void DrawFenceArray()
    {
        int size = Mathf.Max(0, EditorGUILayout.IntField("Fence Prefabs Size", fencePrefabs == null ? 0 : fencePrefabs.Length));
        if(fencePrefabs == null || fencePrefabs.Length != size)
            Array.Resize(ref fencePrefabs, size);

        if(fencePrefabs == null) return;

        EditorGUI.indentLevel++;
        for(int i = 0; i < fencePrefabs.Length; i++)
            fencePrefabs[i] = (GameObject)EditorGUILayout.ObjectField($"Fence {i}", fencePrefabs[i], typeof(GameObject), false);
        EditorGUI.indentLevel--;
    }

    Vector3 CellCenterWorld(int x, int z)
    {
        return origin + new Vector3((x + 0.5f) * tileSize, 0f, (z + 0.5f) * tileSize);
    }
}
#endif
