#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;

public class CubehavenGridStamper : EditorWindow
{
    Vector2 scrollPos;

    [Header("Grid")]
    GameObject floorPrefab;
    Transform parent;
    int width = 24;
    int height = 18;
    float tileSize = 1f;
    Vector3 origin = Vector3.zero;
    bool clearExisting = false;
    string generatedRootName = "GeneratedGrid";

    [Header("Decoration")]
    bool scatterDecorations = true;
    GameObject[] decorPrefabs;
    int decorCount = 35;
    float decorMinSpacing = 1.75f;     // in world units
    float decorYOffset = 0f;
    float decorRandomYaw = 360f;
    Vector2 decorScaleRange = new Vector2(1f, 1f);
    int safeRadiusFromOrigin = 3;      // keep start area clear (in tiles)
    float decorSurfaceY = 1f; // you want 1


    [Header("Island Cliffs")]

    bool buildCliffs = true;
    float cliffHeightTiles = 5f;          // how many tiles tall
    float cliffThickness = 0.12f;         // world units thickness of the skirt
    GameObject cliffBlockPrefab;
    int cliffBlocksDown = 5;     // how many 1x1 blocks downward
    float cliffTopY = 1f;        // tile top surface Y (same as decorSurfaceY usually)
    float cliffBlockScale = 1f;  // optional: scale multiplier for the prefab
    Material cliffMaterial;
    bool addBottomCap = false;
    enum CliffStyle
    {
        StretchedSkirtCubes,
        BlockStacksFromOccupancy
    }
    CliffStyle cliffStyle = CliffStyle.BlockStacksFromOccupancy;

    [Header("Perimeter Fences (Optional)")]
    bool buildFences = true;
    bool fencesOnlyOuterPerimeter = true;   // false = also edges around holes
    GameObject[] fencePrefabs;
    float fenceSurfaceY = 1f;              // same as tile top usually
    float fenceYOffset = 0f;
    float fenceOutwardOffset = 0.0f;       // push slightly outward if needed
    float fenceEdgeInset = 0.25f;
    float fenceRandomYawJitter = 0f;       // e.g. 2-5 degrees for organic
    bool clearFencesOnRebuild = true;

    [Header("Procedural Holes")]
    public bool createProceduralHoles = false;
    int seed = 12345;
    public bool randomizeSeedEachBuild = true;

    [Range(0f, 0.9f)]
    float holeChance = 0.25f;          // 0.25 = 25% missing tiles

    bool smoothHoles = true;
    int smoothPasses = 3;              // 2-5 is usually enough

    [Header("Hole Fill (Optional)")]
    bool fillHoles = false;
    GameObject holeFillPrefab;
    float holeFillY = 0f;        // usually 0 if your tile centers are at y=0
    float holeFillYOffset = 0f;  // small tweak if needed
    bool clearHoleFillOnRebuild = true;

    bool keepBorderSolid = true;
    int borderThickness = 1;

    bool ensureConnectedToStart = true;
    // start is treated as origin tile (0,0) in grid space by default
    Vector2Int startCell = new Vector2Int(1, 1); // safer than (0,0) if border is solid
    // ===== Paint / Hole Carving =====
    bool paintMode = false;
    bool paintErase = true;              // true = erase tiles, false = paint tiles back
    int brushRadiusTiles = 0;            // 0 = single tile, 1 = 3x3, etc
    bool showGridOverlay = true;
    bool liveRepaintWhilePainting = true;
    // Hover cell preview (Scene View)
    bool showHoverCell = true;
    Color hoverOutline = new Color(1f, 0f, 0f, 0.95f);
    Color hoverFill = new Color(1f, 0f, 0f, 0.10f);
    float hoverLineThickness = 4f;
    // Hover cell label
    bool showHoverLabel = true;
    Color hoverLabelColor = new Color(1f, 0.35f, 0.35f, 1f);
    Vector3 hoverLabelOffset = new Vector3(0f, 0.03f, 0f);

    bool[,] occupancy;                   // [x,y] tile present?

    // Cached tile positions for current grid
    List<Vector3> tilePositions = new List<Vector3>();

    [MenuItem("Tools/Cubehaven/Grid Stamper")]
    public static void ShowWindow() => GetWindow<CubehavenGridStamper>("Grid Stamper");
    void ClearChildRoot(Transform root, string childName)
    {
        if(root == null) return;
        var t = root.Find(childName);
        if(t == null) return;

        for(int i = t.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(t.GetChild(i).gameObject);
    }
    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Cubehaven Grid Stamper", EditorStyles.boldLabel);

        floorPrefab = (GameObject)EditorGUILayout.ObjectField("Floor Prefab", floorPrefab, typeof(GameObject), false);
        parent = (Transform)EditorGUILayout.ObjectField("Parent (optional)", parent, typeof(Transform), true);

        EditorGUILayout.Space(6);
        width = EditorGUILayout.IntField("Width", Mathf.Max(1, width));
        height = EditorGUILayout.IntField("Height", Mathf.Max(1, height));
        tileSize = EditorGUILayout.FloatField("Tile Size", Mathf.Max(0.01f, tileSize));
        origin = EditorGUILayout.Vector3Field("Origin", origin);

        EditorGUILayout.Space(6);
        clearExisting = EditorGUILayout.Toggle("Clear Previous Grid", clearExisting);
        generatedRootName = EditorGUILayout.TextField("Generated Root Name", generatedRootName);

        EditorGUILayout.Space(12);
        GUILayout.Label("Decoration Scatter (Optional)", EditorStyles.boldLabel);

        scatterDecorations = EditorGUILayout.Toggle("Scatter Decorations", scatterDecorations);

        using(new EditorGUI.DisabledScope(!scatterDecorations))
        {
            DrawDecorArray();
            decorCount = EditorGUILayout.IntField("Decor Count", Mathf.Max(0, decorCount));
            decorMinSpacing = EditorGUILayout.FloatField("Min Spacing", Mathf.Max(0f, decorMinSpacing));
            safeRadiusFromOrigin = EditorGUILayout.IntField("Safe Radius From Origin (tiles)", Mathf.Max(0, safeRadiusFromOrigin));
            decorYOffset = EditorGUILayout.FloatField("Y Offset", decorYOffset);
            decorRandomYaw = EditorGUILayout.Slider("Random Yaw", decorRandomYaw, 0f, 360f);
            decorScaleRange = EditorGUILayout.Vector2Field("Scale Range", decorScaleRange);
            if(decorScaleRange.x < 0.01f) decorScaleRange.x = 0.01f;
            if(decorScaleRange.y < 0.01f) decorScaleRange.y = 0.01f;
        }

        EditorGUILayout.Space(12);
        GUILayout.Label("Island Cliffs (Optional)", EditorStyles.boldLabel);

        buildCliffs = EditorGUILayout.Toggle("Build Cliff Skirt", buildCliffs);

        using(new EditorGUI.DisabledScope(!buildCliffs))
        {
            cliffBlockPrefab = (GameObject)EditorGUILayout.ObjectField("Cliff Block Prefab", cliffBlockPrefab, typeof(GameObject), false);
            cliffBlocksDown = EditorGUILayout.IntField("Blocks Down", Mathf.Max(1, cliffBlocksDown));
            cliffTopY = EditorGUILayout.FloatField("Cliff Top Y", cliffTopY);
            cliffBlockScale = EditorGUILayout.FloatField("Cliff Block Scale", Mathf.Max(0.01f, cliffBlockScale));

            cliffHeightTiles = EditorGUILayout.FloatField("Cliff Height (tiles)", Mathf.Max(0.1f, cliffHeightTiles));
            cliffThickness = EditorGUILayout.FloatField("Cliff Thickness", Mathf.Max(0.01f, cliffThickness));
            cliffMaterial = (Material)EditorGUILayout.ObjectField("Cliff Material", cliffMaterial, typeof(Material), false);
            addBottomCap = EditorGUILayout.Toggle("Add Bottom Cap", addBottomCap);
            cliffStyle = (CliffStyle)EditorGUILayout.EnumPopup("Cliff Style", cliffStyle);
            if(GUILayout.Button("Build/Refresh Cliffs From Map"))
            {
                Transform root = GetOrCreateRoot();
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();

                ClearChildRoot(root, "Cliffs");

                if(cliffStyle == CliffStyle.BlockStacksFromOccupancy)
                    BuildCliffsFromOccupancy(root);
                else
                    BuildCliffSkirt(root);

                Undo.CollapseUndoOperations(group);
            }
        }
        EditorGUILayout.Space(12);
        GUILayout.Label("Perimeter Fences (Optional)", EditorStyles.boldLabel);

        buildFences = EditorGUILayout.Toggle("Build Fences", buildFences);
        using(new EditorGUI.DisabledScope(!buildFences))
        {
            fencesOnlyOuterPerimeter = EditorGUILayout.Toggle("Only Outer Perimeter", fencesOnlyOuterPerimeter);
            DrawFenceArray();

            fenceSurfaceY = EditorGUILayout.FloatField("Fence Surface Y", fenceSurfaceY);
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

                ClearChildRoot(root, "Fences");
                BuildFencesFromOccupancy(root);

                Undo.CollapseUndoOperations(group);
            }
        }
        EditorGUILayout.Space(12);
        GUILayout.Label("Procedural Holes (Optional)", EditorStyles.boldLabel);

        createProceduralHoles = EditorGUILayout.Toggle("Create Procedural Holes", createProceduralHoles);

        using(new EditorGUI.DisabledScope(!createProceduralHoles))
        {
            randomizeSeedEachBuild = EditorGUILayout.Toggle("Randomize Seed Each Build", randomizeSeedEachBuild);

            using(new EditorGUI.DisabledScope(randomizeSeedEachBuild))
                seed = EditorGUILayout.IntField("Seed", seed);

            holeChance = EditorGUILayout.Slider("Hole Chance", holeChance, 0f, 0.9f);

            smoothHoles = EditorGUILayout.Toggle("Smooth Holes", smoothHoles);
            using(new EditorGUI.DisabledScope(!smoothHoles))
                smoothPasses = EditorGUILayout.IntSlider("Smooth Passes", smoothPasses, 0, 10);

            keepBorderSolid = EditorGUILayout.Toggle("Keep Border Solid", keepBorderSolid);
            using(new EditorGUI.DisabledScope(!keepBorderSolid))
                borderThickness = EditorGUILayout.IntSlider("Border Thickness", borderThickness, 0, 10);

            ensureConnectedToStart = EditorGUILayout.Toggle("Ensure Connected To Start", ensureConnectedToStart);
            using(new EditorGUI.DisabledScope(!ensureConnectedToStart))
                startCell = EditorGUILayout.Vector2IntField("Start Cell", startCell);
        }
        EditorGUILayout.Space(12);
        GUILayout.Label("Hole Fill (Optional)", EditorStyles.boldLabel);
        fillHoles = EditorGUILayout.Toggle("Fill Holes", fillHoles);
        using(new EditorGUI.DisabledScope(!fillHoles))
        {
            holeFillPrefab = (GameObject)EditorGUILayout.ObjectField("Hole Fill Prefab", holeFillPrefab, typeof(GameObject), false);
            holeFillY = EditorGUILayout.FloatField("Hole Fill Y", holeFillY);
            holeFillYOffset = EditorGUILayout.FloatField("Y Offset", holeFillYOffset);
            clearHoleFillOnRebuild = EditorGUILayout.Toggle("Clear Old Hole Fill", clearHoleFillOnRebuild);
            if(GUILayout.Button("Build/Refresh Hole Fill"))
            {
                Transform root = GetOrCreateRoot();
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();

                ClearChildRoot(root, "HoleFill");
                BuildHoleFillFromOccupancy(root);

                Undo.CollapseUndoOperations(group);
            }

        }
        EditorGUILayout.Space(12);
        GUILayout.Label("Void Cutter / Paint Holes", EditorStyles.boldLabel);

        paintMode = EditorGUILayout.Toggle("Paint Mode (Scene View)", paintMode);
        using(new EditorGUI.DisabledScope(!paintMode))
        {
            paintErase = EditorGUILayout.Toggle("Erase (unchecked = Paint)", paintErase);
            brushRadiusTiles = EditorGUILayout.IntSlider("Brush Radius (tiles)", brushRadiusTiles, 0, 6);
            showGridOverlay = EditorGUILayout.Toggle("Show Grid Overlay", showGridOverlay);
            showHoverCell = EditorGUILayout.Toggle("Show Hover Cell", showHoverCell);
            showHoverLabel = EditorGUILayout.Toggle("Show Hover Label", showHoverLabel);

            EditorGUILayout.HelpBox(
                "Scene view: Left click/drag to paint. Hold Shift to invert mode.",
                MessageType.Info
            );
        }

        using(new EditorGUI.DisabledScope(occupancy == null))
        {
            if(GUILayout.Button("Rebuild From Map"))
                RebuildFromOccupancy();
        }

        using(new EditorGUI.DisabledScope(floorPrefab == null))
        {
            if(GUILayout.Button("Init Map (Fill All)"))
                InitOccupancyFillAll();
        }

        using(new EditorGUI.DisabledScope(floorPrefab == null))
        {
            if(GUILayout.Button("Build Grid"))
                BuildGridAndMaybeDecor();
        }

        if(GUILayout.Button("Clear Grid"))
            ClearGridOnly();

        EditorGUILayout.EndScrollView();
    }
    void BuildHoleFillFromOccupancy(Transform root)
    {
        if(!fillHoles) return;
        if(occupancy == null) return;
        if(holeFillPrefab == null) return;

        Transform fillRoot = root.Find("HoleFill");
        if(fillRoot == null)
        {
            GameObject go = new GameObject("HoleFill");
            Undo.RegisterCreatedObjectUndo(go, "Create HoleFill Root");
            go.transform.SetParent(root, true);
            fillRoot = go.transform;
        }
        else if(clearHoleFillOnRebuild)
        {
            for(int i = fillRoot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(fillRoot.GetChild(i).gameObject);
        }

        for(int y = 0; y < height; y++)
            for(int x = 0; x < width; x++)
            {
                if(occupancy[x, y]) continue; // only holes (in-bounds)

                Vector3 pos = CellCenterWorld(x, y);
                pos.y = origin.y + holeFillY + holeFillYOffset;

                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(holeFillPrefab);
                Undo.RegisterCreatedObjectUndo(go, "Create Hole Fill");

                go.transform.SetParent(fillRoot, true);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.identity;
                go.name = $"HoleFill_{x}_{y}";
            }
    }
    void DrawHoverLabel(Vector2Int cell, Event e)
    {
        float yDraw = origin.y + decorSurfaceY + 0.09f;

        Vector3 labelPos = origin + new Vector3(
            cell.x * tileSize + tileSize * 0.5f,
            yDraw,
            cell.y * tileSize + tileSize * 0.5f
        ) + hoverLabelOffset;

        bool effectiveErase = paintErase;
        if(e != null && e.shift) effectiveErase = !effectiveErase;

        string mode = effectiveErase ? "ERASE" : "PAINT";

        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = hoverLabelColor;

        Handles.Label(labelPos, $"Cell: {cell.x},{cell.y}   [{mode}]", style);
    }
    bool TryGetMouseCell(out Vector2Int cell)
    {
        cell = default;

        Ray r = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

        // Prefer raycast first (lets hover work over colliders)
        if(Physics.Raycast(r, out RaycastHit hit, 9999f))
        {
            cell = WorldToCell(hit.point);
            return true;
        }

        // Fallback: project onto plane at surface height
        Plane p = new Plane(Vector3.up, new Vector3(0f, origin.y + decorSurfaceY, 0f));
        if(!p.Raycast(r, out float enter)) return false;

        Vector3 world = r.GetPoint(enter);
        cell = WorldToCell(world);
        return true;
    }

    void DrawHoverCell(Vector2Int cell)
    {
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        float yDraw = origin.y + decorSurfaceY + 0.06f;

        Vector3 p = origin + new Vector3(cell.x * tileSize, 0f, cell.y * tileSize);

        Vector3 a = new Vector3(p.x, yDraw, p.z);
        Vector3 b = new Vector3(p.x + tileSize, yDraw, p.z);
        Vector3 c = new Vector3(p.x + tileSize, yDraw, p.z + tileSize);
        Vector3 d = new Vector3(p.x, yDraw, p.z + tileSize);

        Handles.DrawSolidRectangleWithOutline(
            new Vector3[] { a, b, c, d },
            hoverFill,
            hoverOutline
        );

        Handles.color = hoverOutline;
        Handles.DrawAAPolyLine(hoverLineThickness, new Vector3[] { a, b, c, d, a });
    }

    void DrawFenceArray()
    {
        int size = Mathf.Max(0, EditorGUILayout.IntField("Fence Prefabs Size", fencePrefabs == null ? 0 : fencePrefabs.Length));
        if(fencePrefabs == null || fencePrefabs.Length != size)
            System.Array.Resize(ref fencePrefabs, size);

        if(fencePrefabs == null) return;

        EditorGUI.indentLevel++;
        for(int i = 0; i < fencePrefabs.Length; i++)
            fencePrefabs[i] = (GameObject)EditorGUILayout.ObjectField($"Fence {i}", fencePrefabs[i], typeof(GameObject), false);
        EditorGUI.indentLevel--;
    }
    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += EditorTick;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= EditorTick;
    }
    void EditorTick()
    {
        if(!paintMode) return;
        if(!showHoverCell && !showGridOverlay) return;
        if(!liveRepaintWhilePainting) return;

        SceneView.RepaintAll();

    }
    Transform GetTilesRoot(Transform root)
    {
        var t = root.Find("Tiles");
        if(t != null) return t;

        var go = new GameObject("Tiles");
        Undo.RegisterCreatedObjectUndo(go, "Create Tiles Root");
        go.transform.SetParent(root, true);
        return go.transform;
    }

    GameObject FindTileGO(int x, int y)
    {
        Transform root = FindRoot();
        if(root == null) return null;

        Transform tiles = root.Find("Tiles");
        if(tiles == null) return null;

        string n = $"Tile_{x}_{y}";
        var t = tiles.Find(n);
        return t ? t.gameObject : null;
    }

    void SpawnTile(int x, int y)
    {
        if(floorPrefab == null) return;

        Transform root = GetOrCreateRoot();
        Transform tiles = GetTilesRoot(root);

        // avoid duplicates
        if(FindTileGO(x, y) != null) return;

        Vector3 pos = CellCenterWorld(x, y);
        GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
        Undo.RegisterCreatedObjectUndo(tile, "Create Tile");

        tile.transform.position = pos;
        tile.transform.rotation = Quaternion.identity;
        tile.transform.SetParent(tiles, true);
        tile.name = $"Tile_{x}_{y}";
    }

    void DeleteTile(int x, int y)
    {
        var go = FindTileGO(x, y);
        if(go != null)
            Undo.DestroyObjectImmediate(go);
    }

    void DrawGridOverlay()
    {
        if(occupancy == null) return;

        // Draw on top of everything
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        float y = origin.y + decorSurfaceY + 0.05f; // cube top (1) + a tiny lift

        for(int gy = 0; gy < height; gy++)
            for(int gx = 0; gx < width; gx++)
            {
                Vector3 c0 = CellCenterWorld(gx, gy);
                Vector3 center = new Vector3(c0.x, y, c0.z);

                Color col = occupancy[gx, gy]
                    ? new Color(0f, 1f, 0f, 0.18f)
                    : new Color(1f, 0f, 0f, 0.18f);

                Handles.color = col;
                Vector3 size = new Vector3(tileSize * 0.95f, 0.01f, tileSize * 0.95f);
                Handles.DrawWireCube(center, size);
            }
    }
    void OnSceneGUI(SceneView sv)
    {
        if(!paintMode) return;
        if(occupancy == null) return;

        Event e = Event.current;
        if(e == null) return;
        // Make sure we get mouse-move driven repaints
        if(e.type == EventType.MouseMove)
        {
            sv.Repaint();
        }

        // show overlay first so it's always visible
        if(showGridOverlay)
            DrawGridOverlay();

        // ✅ NEW: always show hovered cell
        if(showHoverCell && TryGetMouseCell(out Vector2Int hover))
        {
            DrawHoverCell(hover);

            if(showHoverLabel)
                DrawHoverLabel(hover, e);
        }

        // only paint on left down/drag
        if(e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
        if(e.button != 0) return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // Raycast into scene to find which tile was clicked
        Ray rr = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if(Physics.Raycast(rr, out RaycastHit rh, 999f))
        {
            if(TryGetTileXY(rh.collider.gameObject, out int tx, out int ty))
            {
                bool eraser = paintErase;
                if(e.shift) eraser = !eraser;

                // apply brush to occupancy AND to scene
                ApplyBrushLive(tx, ty, brushRadiusTiles, eraser);

                e.Use();
                sv.Repaint();
                return;
            }
        }

        // fallback: click empty space using plane -> cell
        Ray r = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane p = new Plane(Vector3.up, new Vector3(0f, origin.y + decorSurfaceY, 0f));
        if(!p.Raycast(r, out float enter)) return;

        Vector3 hit = r.GetPoint(enter);
        Vector2Int cell = WorldToCell(hit);

        bool erase2 = paintErase;
        if(e.shift) erase2 = !erase2;

        ApplyBrushLive(cell.x, cell.y, brushRadiusTiles, erase2);

        e.Use();
        sv.Repaint();
    }
    void ApplyBrushLive(int cx, int cy, int radius, bool erase)
    {
        for(int dy = -radius; dy <= radius; dy++)
            for(int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx;
                int y = cy + dy;
                if(x < 0 || y < 0 || x >= width || y >= height) continue;

                bool newVal = !erase;
                if(occupancy[x, y] == newVal) continue;

                occupancy[x, y] = newVal;

                if(erase) DeleteTile(x, y);
                else SpawnTile(x, y);
            }

        // cliffs/decor won't auto-update here (by design)
        // For cliffs: keep your "Rebuild From Map" button for when you're done carving.
    }
    Vector3 CellCenterWorld(int x, int y)
    {
        return origin + new Vector3((x + 0.5f) * tileSize, 0f, (y + 0.5f) * tileSize);
    }
    Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = world - origin;
        int x = Mathf.FloorToInt(local.x / tileSize);
        int y = Mathf.FloorToInt(local.z / tileSize);
        return new Vector2Int(x, y);
    }
    void InitOccupancyFillAll()
    {
        occupancy = new bool[width, height];
        for(int y = 0; y < height; y++)
            for(int x = 0; x < width; x++)
                occupancy[x, y] = true;
    }

    void RebuildFromOccupancy()
    {
        if(floorPrefab == null) return;

        Transform root = GetOrCreateRoot();
        tilePositions.Clear();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // Clear everything under root (tiles, decor, cliffs)
        for(int i = root.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

        // Tiles parent
        GameObject tilesGo = new GameObject("Tiles");
        Undo.RegisterCreatedObjectUndo(tilesGo, "Create Tiles Root");
        tilesGo.transform.SetParent(root, true);

        // Spawn tiles based on occupancy
        for(int y = 0; y < height; y++)
        {
            for(int x = 0; x < width; x++)
            {
                if(!occupancy[x, y]) continue;

                Vector3 pos = CellCenterWorld(x, y);

                GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                Undo.RegisterCreatedObjectUndo(tile, "Create Tile");
                tile.transform.position = pos;
                tile.transform.rotation = Quaternion.identity;
                tile.transform.SetParent(tilesGo.transform, true);
                tile.name = $"Tile_{x}_{y}";

                tilePositions.Add(pos);
            }
        }
        BuildHoleFillFromOccupancy(root);
        // Rebuild cliffs based on edges-to-void (outer + hole edges)
        if(buildCliffs)
        {
            if(cliffStyle == CliffStyle.BlockStacksFromOccupancy)
                BuildCliffsFromOccupancy(root);
            else
                BuildCliffSkirt(root); // your old stretched-white-cubes method
        }

        // Optional decor scatter still works, but it’ll scatter on remaining tiles only
        if(scatterDecorations)
            ScatterDecorations(root);

        if(buildFences)
            BuildFencesFromOccupancy(root);

        var settings = root.GetComponent<CubehavenGridSettings>();
        if(settings == null) settings = root.gameObject.AddComponent<CubehavenGridSettings>();
        settings.width = width;
        settings.height = height;
        settings.tileSize = tileSize;
        settings.origin = origin;
        settings.surfaceY = decorSurfaceY; // or 1f

        Undo.CollapseUndoOperations(group);
        Selection.activeObject = root.gameObject;
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
            for(int i = fencesRoot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(fencesRoot.GetChild(i).gameObject);
        }

        bool Has(int x, int y)
        {
            if(x < 0 || y < 0 || x >= width || y >= height) return false;
            return occupancy[x, y];
        }

        bool IsOuterEdge(int x, int y)
        {
            // “outer perimeter only” means: neighbour missing because out-of-bounds (not because of a hole)
            // So if the neighbour coordinate is OOB, count it. If neighbour is in-bounds but empty, that's a hole edge.
            return (x < 0 || y < 0 || x >= width || y >= height);
        }

        GameObject PickFence()
        {
            for(int tries = 0; tries < 10; tries++)
            {
                int i = UnityEngine.Random.Range(0, fencePrefabs.Length);
                if(fencePrefabs[i] != null) return fencePrefabs[i];
            }
            for(int i = 0; i < fencePrefabs.Length; i++)
                if(fencePrefabs[i] != null) return fencePrefabs[i];
            return null;
        }

        void SpawnFence(int x, int y, Vector3 edgeCenter, Vector3 outwardDir, string dirTag)
        {
            GameObject prefab = PickFence();
            if(prefab == null) return;

            Vector3 dir = outwardDir.normalized;

            // Start exactly on the tile edge
            Vector3 pos = edgeCenter;

            // Push inward into the tile (this is what you want)
            pos -= dir * fenceEdgeInset;

            // Optional: push outward (rarely needed, but keep it)
            pos += dir * fenceOutwardOffset;
            pos.y = origin.y + fenceSurfaceY + fenceYOffset;

            // Rotation: assume fence “forward” should face outward.
            Quaternion rot = Quaternion.LookRotation(outwardDir, Vector3.up);
            rot *= Quaternion.Euler(0f, 180f, 0f); // tweak 0/90/180/270 until correct
            // optional tiny jitter
            if(fenceRandomYawJitter > 0f)
                rot *= Quaternion.Euler(0f, UnityEngine.Random.Range(-fenceRandomYawJitter, fenceRandomYawJitter), 0f);

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Create Fence");
            Vector3 fenceLocalPivotFix = Vector3.zero; // tweak in inspector, e.g. (0.05,0,0)
            go.transform.SetParent(fencesRoot, true);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.position += go.transform.right * fenceLocalPivotFix.x;
            go.transform.position += go.transform.up * fenceLocalPivotFix.y;
            go.transform.position += go.transform.forward * fenceLocalPivotFix.z;

            go.name = $"Fence_{x}_{y}_{dirTag}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }

        // For each occupied tile, if neighbour is missing, place fence on that edge.
        for(int y = 0; y < height; y++)
            for(int x = 0; x < width; x++)
            {
                if(!Has(x, y)) continue;

                Vector3 tileCorner = origin + new Vector3(x * tileSize, 0f, y * tileSize);
                Vector3 tileCenter = tileCorner + new Vector3(tileSize * 0.5f, 0f, tileSize * 0.5f);


                // West neighbour (x-1)
                if(!Has(x - 1, y))
                {
                    if(!fencesOnlyOuterPerimeter || IsOuterEdge(x - 1, y))
                    {
                        Vector3 c = tileCenter + new Vector3(-tileSize * 0.5f, 0f, 0f);
                        SpawnFence(x, y, c, Vector3.left, "W");
                    }
                }

                // East neighbour (x+1)
                if(!Has(x + 1, y))
                {
                    if(!fencesOnlyOuterPerimeter || IsOuterEdge(x + 1, y))
                    {
                        Vector3 c = tileCenter + new Vector3(tileSize * 0.5f, 0f, 0f);
                        SpawnFence(x, y, c, Vector3.right, "E");
                    }
                }

                // South neighbour (y-1)
                if(!Has(x, y - 1))
                {
                    if(!fencesOnlyOuterPerimeter || IsOuterEdge(x, y - 1))
                    {
                        Vector3 c = tileCenter + new Vector3(0f, 0f, -tileSize * 0.5f);
                        SpawnFence(x, y, c, Vector3.back, "S");
                    }
                }

                // North neighbour (y+1)
                if(!Has(x, y + 1))
                {
                    if(!fencesOnlyOuterPerimeter || IsOuterEdge(x, y + 1))
                    {
                        Vector3 c = tileCenter + new Vector3(0f, 0f, tileSize * 0.5f);
                        SpawnFence(x, y, c, Vector3.forward, "N");
                    }
                }
            }
    }
    void BuildCliffsFromOccupancy(Transform root)
    {
        if(occupancy == null) return;
        if(cliffBlockPrefab == null) return;

        Transform cliffRoot = root.Find("Cliffs");
        if(cliffRoot == null)
        {
            GameObject cliffsGo = new GameObject("Cliffs");
            Undo.RegisterCreatedObjectUndo(cliffsGo, "Create Cliffs Root");
            cliffsGo.transform.SetParent(root, true);
            cliffRoot = cliffsGo.transform;
        }
        else
        {
            for(int i = cliffRoot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(cliffRoot.GetChild(i).gameObject);
        }

        bool Has(int x, int y)
        {
            if(x < 0 || y < 0 || x >= width || y >= height) return false;
            return occupancy[x, y];
        }

        void SpawnStack(Vector3 edgeCenterOnTop, string nameTag)
        {
            // top of the cliff starts at tile top
            // each block goes down by tileSize
            for(int i = 0; i < cliffBlocksDown; i++)
            {
                Vector3 p = edgeCenterOnTop;
                p.y = origin.y + cliffTopY - tileSize - (i * tileSize);
                p.y -= tileSize; // <- drop the whole stack down one tile

                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(cliffBlockPrefab);
                Undo.RegisterCreatedObjectUndo(go, "Create Cliff Block");
                go.transform.SetParent(cliffRoot, true);
                go.transform.position = p;
                go.transform.rotation = Quaternion.identity;

                if(Mathf.Abs(cliffBlockScale - 1f) > 0.0001f)
                    go.transform.localScale = go.transform.localScale * cliffBlockScale;

                go.name = $"Cliff_{nameTag}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            }
        }

        // IMPORTANT: base everything off TILE CENTER, not corner (fixes the 0.5 offset weirdness)
        for(int y = 0; y < height; y++)
            for(int x = 0; x < width; x++)
            {
                if(!Has(x, y)) continue;

                //Vector3 tileCenter = CellCenterWorld(x, y);

                //// West neighbour cell
                //if(!Has(x - 1, y))
                //{
                //    Vector3 voidCellCenter = CellCenterWorld(x - 1, y);
                //    SpawnStack(voidCellCenter, $"W_{x}_{y}");
                //}

                //// East neighbour cell
                //if(!Has(x + 1, y))
                //{
                //    Vector3 voidCellCenter = CellCenterWorld(x + 1, y);
                //    SpawnStack(voidCellCenter, $"E_{x}_{y}");
                //}

                //// South neighbour cell
                //if(!Has(x, y - 1))
                //{
                //    Vector3 voidCellCenter = CellCenterWorld(x, y - 1);
                //    SpawnStack(voidCellCenter, $"S_{x}_{y}");
                //}

                //// North neighbour cell
                //if(!Has(x, y + 1))
                //{
                //    Vector3 voidCellCenter = CellCenterWorld(x, y + 1);
                //    SpawnStack(voidCellCenter, $"N_{x}_{y}");
                //}

                Vector3 tileCenter = CellCenterWorld(x, y);

                // If neighbour is missing, hang the cliff UNDER THIS TILE (not in the void)
                if(!Has(x - 1, y)) SpawnStack(tileCenter, $"W_{x}_{y}");
                if(!Has(x + 1, y)) SpawnStack(tileCenter, $"E_{x}_{y}");
                if(!Has(x, y - 1)) SpawnStack(tileCenter, $"S_{x}_{y}");
                if(!Has(x, y + 1)) SpawnStack(tileCenter, $"N_{x}_{y}");
            }
    }


    void PaintAt(Vector2Int center, int radius, bool erase)
    {
        for(int dy = -radius; dy <= radius; dy++)
            for(int dx = -radius; dx <= radius; dx++)
            {
                int x = center.x + dx;
                int y = center.y + dy;
                if(x < 0 || y < 0 || x >= width || y >= height) continue;

                occupancy[x, y] = !erase;
            }
    }

    void DrawDecorArray()
    {
        int size = Mathf.Max(0, EditorGUILayout.IntField("Decor Prefabs Size", decorPrefabs == null ? 0 : decorPrefabs.Length));
        if(decorPrefabs == null || decorPrefabs.Length != size)
            System.Array.Resize(ref decorPrefabs, size);

        if(decorPrefabs == null) return;

        EditorGUI.indentLevel++;
        for(int i = 0; i < decorPrefabs.Length; i++)
            decorPrefabs[i] = (GameObject)EditorGUILayout.ObjectField($"Decor {i}", decorPrefabs[i], typeof(GameObject), false);
        EditorGUI.indentLevel--;
    }

    void BuildGridAndMaybeDecor()
    {
        if(occupancy == null || occupancy.GetLength(0) != width || occupancy.GetLength(1) != height)
            InitOccupancyFillAll();

        if(createProceduralHoles)
        {
            if(randomizeSeedEachBuild)
                seed = System.DateTime.Now.Millisecond + UnityEngine.Random.Range(0, 999999);

            GenerateProceduralOccupancy();

            if(ensureConnectedToStart)
                KeepOnlyConnectedRegion(startCell);

            RebuildFromOccupancy();   // <-- THIS is the key
            return;                   // <-- stop here
        }

        // otherwise build full grid as before (your existing code)
        Transform root = GetOrCreateRoot();
        tilePositions.Clear();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        if(clearExisting)
        {
            for(int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        }

        for(int y = 0; y < height; y++)
            for(int x = 0; x < width; x++)
            {
                Vector3 pos = CellCenterWorld(x, y);

                GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                Undo.RegisterCreatedObjectUndo(tile, "Create Tile");
                tile.transform.position = pos;
                tile.transform.rotation = Quaternion.identity;
                tile.transform.SetParent(root, true);
                tile.name = $"Tile_{x}_{y}";

                tilePositions.Add(pos);
            }

        if(buildCliffs) BuildCliffSkirt(root);
        if(scatterDecorations) ScatterDecorations(root);

        Undo.CollapseUndoOperations(group);
        Selection.activeObject = root.gameObject;
    }

    void GenerateProceduralOccupancy()
    {
        UnityEngine.Random.InitState(seed);

        // Start from all filled
        for(int y = 0; y < height; y++)
            for(int x = 0; x < width; x++)
                occupancy[x, y] = true;

        // Carve holes
        for(int y = 0; y < height; y++)
            for(int x = 0; x < width; x++)
            {
                if(keepBorderSolid && IsInBorder(x, y, borderThickness))
                {
                    occupancy[x, y] = true;
                    continue;
                }

                // holeChance = chance to REMOVE tile
                bool makeHole = UnityEngine.Random.value < holeChance;
                occupancy[x, y] = !makeHole;
            }

        if(smoothHoles)
        {
            for(int pass = 0; pass < smoothPasses; pass++)
                SmoothOnce();
        }
    }

    bool IsInBorder(int x, int y, int b)
    {
        return x < b || y < b || x >= (width - b) || y >= (height - b);
    }

    void SmoothOnce()
    {
        // Classic cellular-automata smoothing:
        // If many neighbors are filled -> fill, if many are empty -> empty
        bool[,] next = new bool[width, height];

        for(int y = 0; y < height; y++)
            for(int x = 0; x < width; x++)
            {
                if(keepBorderSolid && IsInBorder(x, y, borderThickness))
                {
                    next[x, y] = true;
                    continue;
                }

                int filled = CountFilledNeighbors8(x, y);

                // tweak thresholds to taste
                if(filled >= 5) next[x, y] = true;
                else if(filled <= 3) next[x, y] = false;
                else next[x, y] = occupancy[x, y];
            }

        occupancy = next;
    }
    void KeepOnlyConnectedRegion(Vector2Int start)
    {
        if(start.x < 0 || start.y < 0 || start.x >= width || start.y >= height)
            return;

        if(!occupancy[start.x, start.y])
        {
            // If start is currently a hole, find nearest filled tile
            if(!TryFindNearestFilled(start, out start))
                return;
        }

        bool[,] visited = new bool[width, height];
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(start);
        visited[start.x, start.y] = true;

        // 4-neighbour flood fill for “walk connectivity”
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while(q.Count > 0)
        {
            var p = q.Dequeue();

            for(int i = 0; i < 4; i++)
            {
                int nx = p.x + dx[i];
                int ny = p.y + dy[i];

                if(nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                if(visited[nx, ny]) continue;
                if(!occupancy[nx, ny]) continue;

                visited[nx, ny] = true;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        // Anything not visited becomes a hole
        for(int y = 0; y < height; y++)
            for(int x = 0; x < width; x++)
                occupancy[x, y] = visited[x, y];
    }

    bool TryFindNearestFilled(Vector2Int from, out Vector2Int found)
    {
        found = from;
        int bestDist = int.MaxValue;
        bool ok = false;

        for(int y = 0; y < height; y++)
            for(int x = 0; x < width; x++)
            {
                if(!occupancy[x, y]) continue;
                int d = Mathf.Abs(x - from.x) + Mathf.Abs(y - from.y);
                if(d < bestDist)
                {
                    bestDist = d;
                    found = new Vector2Int(x, y);
                    ok = true;
                }
            }
        return ok;
    }
    int CountFilledNeighbors8(int cx, int cy)
    {
        int c = 0;
        for(int y = cy - 1; y <= cy + 1; y++)
            for(int x = cx - 1; x <= cx + 1; x++)
            {
                if(x == cx && y == cy) continue;

                // out of bounds counts as empty (or filled) depending on what you want.
                // For islands, treat out of bounds as empty.
                if(x < 0 || y < 0 || x >= width || y >= height) continue;

                if(occupancy[x, y]) c++;
            }
        return c;
    }

    void BuildCliffSkirt(Transform root)
    {
        // Put cliffs under their own parent
        Transform cliffRoot = root.Find("Cliffs");
        if(cliffRoot == null)
        {
            GameObject cliffsGo = new GameObject("Cliffs");
            Undo.RegisterCreatedObjectUndo(cliffsGo, "Create Cliffs Root");
            cliffsGo.transform.SetParent(root, true);
            cliffRoot = cliffsGo.transform;
        }
        else
        {
            // Clear old cliffs if rebuilding
            for(int i = cliffRoot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(cliffRoot.GetChild(i).gameObject);
        }

        float h = cliffHeightTiles * tileSize;
        float yCenter = origin.y - (h * 0.5f); // wall hangs down from floor level

        // Helper that spawns one wall segment
        void SpawnWall(Vector3 center, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CliffWall";
            Undo.RegisterCreatedObjectUndo(go, "Create Cliff Wall");

            go.transform.SetParent(cliffRoot, true);
            go.transform.position = new Vector3(center.x, yCenter, center.z);
            go.transform.localScale = size;

            // Material (sharedMaterial avoids instancing)
            var r = go.GetComponent<Renderer>();
            if(r != null && cliffMaterial != null)
                r.sharedMaterial = cliffMaterial;
        }

        // Perimeter walls (since grid is a solid rectangle)
        // Left + Right
        for(int y = 0; y < height; y++)
        {
            Vector3 tilePosLeft = origin + new Vector3(0 * tileSize, 0f, y * tileSize);
            Vector3 tilePosRight = origin + new Vector3((width - 1) * tileSize, 0f, y * tileSize);

            // Left wall segment (just outside the left edge)
            SpawnWall(
                tilePosLeft + new Vector3(-tileSize * 0.5f, 0f, 0f),
                new Vector3(cliffThickness, h, tileSize)
            );

            // Right wall segment
            SpawnWall(
                tilePosRight + new Vector3(tileSize * 0.5f, 0f, 0f),
                new Vector3(cliffThickness, h, tileSize)
            );
        }

        // Bottom + Top
        for(int x = 0; x < width; x++)
        {
            Vector3 tilePosBottom = origin + new Vector3(x * tileSize, 0f, 0 * tileSize);
            Vector3 tilePosTop = origin + new Vector3(x * tileSize, 0f, (height - 1) * tileSize);

            // Bottom wall
            SpawnWall(
                tilePosBottom + new Vector3(0f, 0f, -tileSize * 0.5f),
                new Vector3(tileSize, h, cliffThickness)
            );

            // Top wall
            SpawnWall(
                tilePosTop + new Vector3(0f, 0f, tileSize * 0.5f),
                new Vector3(tileSize, h, cliffThickness)
            );
        }

        // Optional: bottom cap (purely visual)
        if(addBottomCap)
        {
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = "CliffBottomCap";
            Undo.RegisterCreatedObjectUndo(cap, "Create Cliff Bottom Cap");

            cap.transform.SetParent(cliffRoot, true);

            // Center under the whole grid
            Vector3 center = origin + new Vector3((width - 1) * tileSize * 0.5f, 0f, (height - 1) * tileSize * 0.5f);
            cap.transform.position = new Vector3(center.x, origin.y - h, center.z);

            // Slightly bigger than grid footprint
            cap.transform.localScale = new Vector3(width * tileSize + tileSize, cliffThickness, height * tileSize + tileSize);

            var r = cap.GetComponent<Renderer>();
            if(r != null && cliffMaterial != null)
                r.sharedMaterial = cliffMaterial;
        }
    }
    void ScatterDecorations(Transform root)
    {
        if(decorPrefabs == null || decorPrefabs.Length == 0) return;

        // Create a separate parent for decor
        Transform decorRoot = root.Find("Decor");
        if(decorRoot == null)
        {
            GameObject decorGo = new GameObject("Decor");
            Undo.RegisterCreatedObjectUndo(decorGo, "Create Decor Root");
            decorGo.transform.SetParent(root, true);
            decorRoot = decorGo.transform;
        }

        // Build candidate list excluding “safe area” near origin
        List<Vector3> candidates = new List<Vector3>(tilePositions.Count);
        Vector3 safeCenter = origin; // treat origin as “start-ish”
        float safeRadiusWorld = safeRadiusFromOrigin * tileSize;

        for(int i = 0; i < tilePositions.Count; i++)
        {
            Vector3 p = tilePositions[i];
            if(Vector3.Distance(new Vector3(p.x, 1, p.z), new Vector3(safeCenter.x, 1, safeCenter.z)) <= safeRadiusWorld)
                continue;

            candidates.Add(p);
        }

        // Poisson-ish: accept placements only if far enough from existing placements
        List<Vector3> placed = new List<Vector3>();

        int attempts = decorCount * 20; // capped attempts
        int placedCount = 0;

        while(placedCount < decorCount && attempts-- > 0 && candidates.Count > 0)
        {
            int idx = UnityEngine.Random.Range(0, candidates.Count);
            Vector3 basePos = candidates[idx];
            candidates.RemoveAt(idx);

            Vector3 pos = basePos + new Vector3(
     Random.Range(-tileSize * 0.35f, tileSize * 0.35f),
     0f,
     Random.Range(-tileSize * 0.35f, tileSize * 0.35f)
 );

            // force it to spawn “on top”
            pos.y = origin.y + decorSurfaceY + decorYOffset;

            bool tooClose = false;
            for(int i = 0; i < placed.Count; i++)
            {
                if(Vector3.Distance(pos, placed[i]) < decorMinSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if(tooClose) continue;

            GameObject prefab = PickRandomDecor();
            if(prefab == null) continue;

            GameObject deco = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(deco, "Create Decor");

            deco.transform.position = pos;
            deco.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, decorRandomYaw), 0f);

            float s = UnityEngine.Random.Range(decorScaleRange.x, decorScaleRange.y);
            deco.transform.localScale = deco.transform.localScale * s;

            deco.transform.SetParent(decorRoot, true);

            placed.Add(pos);
            placedCount++;
        }
    }

    GameObject PickRandomDecor()
    {
        // choose non-null
        for(int tries = 0; tries < 10; tries++)
        {
            int i = Random.Range(0, decorPrefabs.Length);
            if(decorPrefabs[i] != null) return decorPrefabs[i];
        }
        // fallback scan
        for(int i = 0; i < decorPrefabs.Length; i++)
            if(decorPrefabs[i] != null) return decorPrefabs[i];
        return null;
    }
    bool TryGetTileXY(GameObject go, out int x, out int y)
    {
        x = y = -1;
        if(go == null) return false;
        var n = go.name; // "Tile_3_7"
        if(!n.StartsWith("Tile_")) return false;

        var parts = n.Split('_');
        if(parts.Length != 3) return false;

        return int.TryParse(parts[1], out x) && int.TryParse(parts[2], out y);
    }
    void ClearGridOnly()
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
        Transform existing = FindRoot();
        if(existing != null) return existing;

        GameObject go = new GameObject(generatedRootName);
        Undo.RegisterCreatedObjectUndo(go, "Create Grid Root");

        if(parent != null)
            go.transform.SetParent(parent, true);

        go.transform.position = Vector3.zero;

        var settings = go.GetComponent<CubehavenGridSettings>();
        if(settings == null) settings = go.AddComponent<CubehavenGridSettings>();
        settings.width = width;
        settings.height = height;
        settings.tileSize = tileSize;
        settings.origin = origin;
        settings.surfaceY = decorSurfaceY; // or 1f

        return go.transform;
    }

    Transform FindRoot()
    {
        if(parent != null)
        {
            Transform t = parent.Find(generatedRootName);
            if(t != null) return t;
        }

        GameObject found = GameObject.Find(generatedRootName);
        return found != null ? found.transform : null;
    }

    Transform GetOrCreateChildRoot(Transform root, string childName)
{
    var t = root.Find(childName);
    if(t != null) return t;

    var go = new GameObject(childName);
    Undo.RegisterCreatedObjectUndo(go, $"Create {childName} Root");
    go.transform.SetParent(root, true);
    return go.transform;
}

void ClearChildren(Transform t)
{
    if(t == null) return;
    for(int i = t.childCount - 1; i >= 0; i--)
        Undo.DestroyObjectImmediate(t.GetChild(i).gameObject);
}

void ClearOnlyTiles(Transform root)
{
    var tiles = root.Find("Tiles");
    if(tiles != null) ClearChildren(tiles);
}

void ClearOnlyCliffs(Transform root)
{
    var cliffs = root.Find("Cliffs");
    if(cliffs != null) ClearChildren(cliffs);
}

void ClearOnlyFences(Transform root)
{
    var fences = root.Find("Fences");
    if(fences != null) ClearChildren(fences);
}

void ClearOnlyDecor(Transform root)
{
    var decor = root.Find("Decor");
    if(decor != null) ClearChildren(decor);
}

void ClearOnlyHoleFill(Transform root)
{
    var fill = root.Find("HoleFill");
    if(fill != null) ClearChildren(fill);
}

void RebuildTilesFromOccupancy(Transform root)
{
    if(floorPrefab == null) return;
    if(occupancy == null) return;

    tilePositions.Clear();

    // Make sure Tiles root exists
    Transform tilesRoot = root.Find("Tiles");
    if(tilesRoot == null)
    {
        var go = new GameObject("Tiles");
        Undo.RegisterCreatedObjectUndo(go, "Create Tiles Root");
        go.transform.SetParent(root, true);
        tilesRoot = go.transform;
    }
    else
    {
        ClearChildren(tilesRoot);
    }

    for(int y = 0; y < height; y++)
        for(int x = 0; x < width; x++)
        {
            if(!occupancy[x, y]) continue;

            Vector3 pos = CellCenterWorld(x, y);
            GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
            Undo.RegisterCreatedObjectUndo(tile, "Create Tile");
            tile.transform.position = pos;
            tile.transform.rotation = Quaternion.identity;
            tile.transform.SetParent(tilesRoot, true);
            tile.name = $"Tile_{x}_{y}";

            tilePositions.Add(pos);
        }
}

void RegenerateProceduralHolesOnly()
{
    if(occupancy == null || occupancy.GetLength(0) != width || occupancy.GetLength(1) != height)
        InitOccupancyFillAll();

    if(randomizeSeedEachBuild)
        seed = System.DateTime.Now.Millisecond + UnityEngine.Random.Range(0, 999999);

    GenerateProceduralOccupancy();

    if(ensureConnectedToStart)
        KeepOnlyConnectedRegion(startCell);
}
}
#endif
