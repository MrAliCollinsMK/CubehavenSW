#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEditorInternal;

public class CubehavenDecorPainter : EditorWindow
{
    // ===== Persistent Palette Asset =====
    [Serializable]
    public class DecorEntry
    {
        public GameObject prefab;
        public bool enabled = true;
        public Vector2 scaleRange = new Vector2(1f, 1f);
        public Vector2 yawRange = new Vector2(0f, 360f);
        public float yOffset = 0f;
        public bool randomYaw = true;
        public bool randomScale = true;
    }

    public class DecorPaletteAsset : ScriptableObject
    {
        public List<DecorEntry> entries = new List<DecorEntry>();
        public int selectedIndex = 0;
        public bool useRandomFromEnabled = false;
    }

    const string PaletteAssetPath = "Assets/Editor/CubehavenDecorPalette.asset";
    DecorPaletteAsset palette;

    // ===== Grid / Target Root =====
    [Header("Grid Snapping")]
    string generatedRootName = "GeneratedGrid";
    Transform parentOptional;
    Vector3 origin = Vector3.zero;
    float tileSize = 1f;
    float surfaceY = 1f; // your cube top / decorSurfaceY equivalent
                         // ===== Cell / Snapping Bias =====
    bool halfTilePivotBias = false;                 // enables +0.5 tile bias before flooring
    Vector2Int cellIndexOffset = Vector2Int.zero;   // if you're consistently "2 tiles off", set to (2,2)

    // ===== Painting =====
    bool paintMode = true;
    bool eraseMode = false;
    int brushRadiusTiles = 0;         // 0=single cell, 1=3x3, etc
    float density = 1f;               // 0..1 chance per cell in brush
    bool allowMultiplePerCell = false;

    // ===== Alt Free Scatter =====
    bool altFreeScatter = true;
    float freeScatterJitter01 = 0.45f; // 0..0.5 of a tile (0.45 = near full cell, but not edges)
    bool freeScatterIgnoresOnePerCell = true;
    // --- Free scatter rate limiting ---
    bool freeScatterThrottle = true;
    float freeScatterMinWorldStep = 0.35f; // in world units (0.35 = nice for 1m tiles)
    float freeScatterMinSeconds = 0.02f;   // optional time gate
    Vector3 lastScatterWorld = new Vector3(float.PositiveInfinity, 0, 0);
    double lastScatterTime = -999;
    bool freeScatterMinSpacingEnabled = true;
    float freeScatterMinSpacing = 0.9f; // world units
    int freeScatterCheckMax = 60;       // how many recent items to check

    // ===== Placement rules =====
    bool snapToCellCenter = true;
    bool raycastForY = false;         // optional: use raycast to sit on mesh
    LayerMask raycastMask = ~0;
    float raycastMax = 500f;

    // ===== Visuals =====
    bool showOverlay = true;

    // More obvious overlay
    Color overlayFill = new Color(1f, 0f, 0f, 0.12f);      // soft red fill
    Color overlayOutline = new Color(1f, 0f, 0f, 0.95f);   // strong red outline
    float overlayLineThickness = 4f;                       // thicker outline

    // Debug “gizmo” label
    bool showCellLabel = true;
    Color labelColor = new Color(1f, 0.35f, 0.35f, 1f);
    Vector3 labelOffset = new Vector3(0f, 0.03f, 0f);

    // ===== Palette Thumbnails =====
    int thumbSize = 56;
    int thumbsPerRow = 6;
    bool showThumbPicker = true;

    Vector2 scroll;

    [MenuItem("Tools/Cubehaven/Decor Painter")]
    public static void ShowWindow() => GetWindow<CubehavenDecorPainter>("Decor Painter");

    void OnEnable()
    {
        EnsurePaletteAsset();
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += EditorTick;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= EditorTick;
    }

    void EnsurePaletteAsset()
    {
        palette = AssetDatabase.LoadAssetAtPath<DecorPaletteAsset>(PaletteAssetPath);
        if(palette != null) return;

        palette = CreateInstance<DecorPaletteAsset>();
        AssetDatabase.CreateAsset(palette, PaletteAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SerializedObject so = new SerializedObject(palette);
        so.Update();

        // draw using SerializedProperty
        EditorGUILayout.PropertyField(so.FindProperty("entries"), true);
        EditorGUILayout.PropertyField(so.FindProperty("selectedIndex"));
        EditorGUILayout.PropertyField(so.FindProperty("useRandomFromEnabled"));

        so.ApplyModifiedProperties();
    }

    void OnGUI()
    {
        EnsurePaletteAsset();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Cubehaven Decor Painter", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Target Root", EditorStyles.boldLabel);
        generatedRootName = EditorGUILayout.TextField("Generated Root Name", generatedRootName);
        parentOptional = (Transform)EditorGUILayout.ObjectField("Parent (optional)", parentOptional, typeof(Transform), true);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Grid Snapping", EditorStyles.boldLabel);
        origin = EditorGUILayout.Vector3Field("Origin", origin);
        tileSize = EditorGUILayout.FloatField("Tile Size", Mathf.Max(0.01f, tileSize));
        surfaceY = EditorGUILayout.FloatField("Surface Y", surfaceY);
        halfTilePivotBias = EditorGUILayout.Toggle("Half-Tile Pivot Bias", halfTilePivotBias);
        cellIndexOffset = EditorGUILayout.Vector2IntField("Cell Index Offset", cellIndexOffset);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        paintMode = EditorGUILayout.Toggle("Paint Mode (Scene View)", paintMode);

        using(new EditorGUI.DisabledScope(!paintMode))
        {
            eraseMode = EditorGUILayout.Toggle("Erase Mode", eraseMode);
            brushRadiusTiles = EditorGUILayout.IntSlider("Brush Radius (tiles)", brushRadiusTiles, 0, 8);
            density = EditorGUILayout.Slider("Density", density, 0f, 1f);
            allowMultiplePerCell = EditorGUILayout.Toggle("Allow Multiple Per Cell", allowMultiplePerCell);

            snapToCellCenter = EditorGUILayout.Toggle("Snap To Cell Center", snapToCellCenter);
            raycastForY = EditorGUILayout.Toggle("Raycast For Y (optional)", raycastForY);
            if(raycastForY)
                raycastMask = LayerMaskField("Raycast Mask", raycastMask);

            showOverlay = EditorGUILayout.Toggle("Show Brush Overlay", showOverlay);
            showCellLabel = EditorGUILayout.Toggle("Show Cell Label", showCellLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Decor Scatter", EditorStyles.boldLabel);
            altFreeScatter = EditorGUILayout.Toggle("Alt = Free Scatter", altFreeScatter);

            using(new EditorGUI.DisabledScope(!altFreeScatter))
            {
                freeScatterJitter01 = EditorGUILayout.Slider("Free Scatter Jitter", freeScatterJitter01, 0f, 0.5f);
                freeScatterIgnoresOnePerCell = EditorGUILayout.Toggle("Ignore One-Per-Cell", freeScatterIgnoresOnePerCell);
            }
            freeScatterThrottle = EditorGUILayout.Toggle("Throttle Free Scatter", freeScatterThrottle);
            using(new EditorGUI.DisabledScope(!freeScatterThrottle))
            {
                freeScatterMinWorldStep = EditorGUILayout.Slider("Min World Step", freeScatterMinWorldStep, 0.01f, 2f);
                freeScatterMinSeconds = EditorGUILayout.Slider("Min Seconds", freeScatterMinSeconds, 0f, 0.25f);
            }
            freeScatterMinSpacingEnabled = EditorGUILayout.Toggle("Min Spacing", freeScatterMinSpacingEnabled);
            using(new EditorGUI.DisabledScope(!freeScatterMinSpacingEnabled))
            {
                freeScatterMinSpacing = EditorGUILayout.Slider("Spacing (world)", freeScatterMinSpacing, 0f, 4f);
                freeScatterCheckMax = EditorGUILayout.IntSlider("Check Last N", freeScatterCheckMax, 10, 200);
            }

            EditorGUILayout.HelpBox(
                "Scene View:\n" +
                "- LMB drag: paint\n" +
                "- Hold Shift: temporarily invert erase/paint\n" +
                "- Ctrl+Z works (Undo supported)\n" +
                "- Alt: free scatter mode\n",
                MessageType.Info
            );
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

        palette.useRandomFromEnabled = EditorGUILayout.Toggle("Random From Enabled", palette.useRandomFromEnabled);
        showThumbPicker = EditorGUILayout.Foldout(showThumbPicker, "Thumbnail Picker", true);
        if(showThumbPicker)
        {
            DrawSelectedPreview();
            EditorGUILayout.Space(4);
            DrawThumbnailPickerGrid();
            EditorGUILayout.Space(8);
        }
        DrawPaletteList();

        EditorGUILayout.Space(8);

        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("Add Entry"))
            {
                Undo.RecordObject(palette, "Add Decor Entry");
                palette.entries.Add(new DecorEntry());
                EditorUtility.SetDirty(palette);
            }

            if(GUILayout.Button("Remove Selected"))
            {
                if(palette.entries.Count > 0 && palette.selectedIndex >= 0 && palette.selectedIndex < palette.entries.Count)
                {
                    Undo.RecordObject(palette, "Remove Decor Entry");
                    palette.entries.RemoveAt(palette.selectedIndex);
                    palette.selectedIndex = Mathf.Clamp(palette.selectedIndex, 0, palette.entries.Count - 1);
                    EditorUtility.SetDirty(palette);
                }
            }

            if(GUILayout.Button("Ping Palette Asset"))
            {
                EditorGUIUtility.PingObject(palette);
                Selection.activeObject = palette;
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Maintenance", EditorStyles.boldLabel);
        if(GUILayout.Button("Clear Painted Decor Under Root"))
        {
            ClearPaintedDecor();
        }

        EditorGUILayout.EndScrollView();
    }
    bool TryGetMouseWorld(out Vector3 world)
    {
        world = default;

        Ray r = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

        if(raycastForY && Physics.Raycast(r, out RaycastHit hit, raycastMax, raycastMask))
        {
            world = hit.point;
            return true;
        }

        Plane p = new Plane(Vector3.up, new Vector3(0f, origin.y, 0f));
        if(!p.Raycast(r, out float enter)) return false;

        world = r.GetPoint(enter);
        return true;
    }
    bool IsTooCloseToRecent(Transform paintedRoot, Vector3 pos, float minSpacing, int checkLastN)
    {
        int start = Mathf.Max(0, paintedRoot.childCount - checkLastN);
        float minSqr = minSpacing * minSpacing;

        for(int i = paintedRoot.childCount - 1; i >= start; i--)
        {
            Transform t = paintedRoot.GetChild(i);
            Vector3 p = t.position;
            p.y = pos.y;

            if((p - pos).sqrMagnitude < minSqr)
                return true;
        }
        return false;
    }

    void DrawPaletteList()
    {

        if(palette.entries == null) palette.entries = new List<DecorEntry>();

        for(int i = 0; i < palette.entries.Count; i++)
        {
            var e = palette.entries[i];

            using(new EditorGUILayout.VerticalScope("box"))
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    bool selected = (i == palette.selectedIndex);
                    if(GUILayout.Toggle(selected, $"Entry {i}", "Button"))
                    {
                        if(!selected)
                        {
                            Undo.RecordObject(palette, "Select Decor Entry");
                            palette.selectedIndex = i;
                            EditorUtility.SetDirty(palette);
                        }
                    }

                    e.enabled = EditorGUILayout.ToggleLeft("Enabled", e.enabled, GUILayout.Width(80));
                }

                e.prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", e.prefab, typeof(GameObject), false);

                using(new EditorGUI.DisabledScope(e.prefab == null))
                {
                    e.yOffset = EditorGUILayout.FloatField("Y Offset", e.yOffset);

                    e.randomYaw = EditorGUILayout.Toggle("Random Yaw", e.randomYaw);
                    if(e.randomYaw)
                        e.yawRange = EditorGUILayout.Vector2Field("Yaw Range", e.yawRange);

                    e.randomScale = EditorGUILayout.Toggle("Random Scale", e.randomScale);
                    if(e.randomScale)
                    {
                        e.scaleRange = EditorGUILayout.Vector2Field("Scale Range", e.scaleRange);
                        if(e.scaleRange.x < 0.01f) e.scaleRange.x = 0.01f;
                        if(e.scaleRange.y < 0.01f) e.scaleRange.y = 0.01f;
                    }
                }
            }
        }

        // Mark palette dirty if modified via GUI
        if(GUI.changed)
        {
            EditorUtility.SetDirty(palette);
        }
    }

    // Unity doesn’t have a built-in LayerMask field in IMGUI, so do a helper
    static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        var layers = InternalEditorUtility.layers;
        int maskWithoutEmpty = 0;
        for(int i = 0; i < layers.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layers[i]);
            if(layer >= 0)
                maskWithoutEmpty |= (1 << layer);
        }

        int newMask = EditorGUILayout.MaskField(label, selected.value, layers);
        selected.value = newMask;
        return selected;
    }
    void DrawSelectedPreview()
    {
        if(palette.entries == null || palette.entries.Count == 0) return;

        int idx = Mathf.Clamp(palette.selectedIndex, 0, palette.entries.Count - 1);
        var e = palette.entries[idx];
        if(e == null) return;

        using(new EditorGUILayout.HorizontalScope("box"))
        {
            Texture2D tex = GetPrefabPreview(e.prefab);
            GUILayout.Label(tex, GUILayout.Width(64), GUILayout.Height(64));

            using(new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Selected", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(e.prefab ? e.prefab.name : "(None)");
                e.enabled = EditorGUILayout.ToggleLeft("Enabled", e.enabled);
            }
        }

        // if we changed enabled state here
        if(GUI.changed)
            EditorUtility.SetDirty(palette);
    }

    void DrawThumbnailPickerGrid()
    {
        if(palette.entries == null) palette.entries = new List<DecorEntry>();

        using(new EditorGUILayout.HorizontalScope())
        {
            thumbSize = EditorGUILayout.IntSlider("Thumb Size", thumbSize, 32, 96);
            thumbsPerRow = EditorGUILayout.IntSlider("Per Row", thumbsPerRow, 3, 12);
        }

        int count = palette.entries.Count;
        if(count == 0)
        {
            EditorGUILayout.HelpBox("Add palette entries to see thumbnails here.", MessageType.Info);
            return;
        }

        int rows = Mathf.CeilToInt(count / (float)thumbsPerRow);

        for(int r = 0; r < rows; r++)
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                for(int c = 0; c < thumbsPerRow; c++)
                {
                    int i = r * thumbsPerRow + c;
                    if(i >= count) break;

                    var entry = palette.entries[i];
                    GameObject prefab = entry != null ? entry.prefab : null;

                    Texture2D tex = GetPrefabPreview(prefab);

                    // highlight selected
                    GUIStyle style = new GUIStyle(GUI.skin.button);
                    if(i == palette.selectedIndex)
                    {
                        style.normal.background = style.active.background;
                    }

                    // dim disabled
                    Color old = GUI.color;
                    if(entry != null && !entry.enabled) GUI.color = new Color(1f, 1f, 1f, 0.35f);

                    if(GUILayout.Button(tex, style, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize)))
                    {
                        Undo.RecordObject(palette, "Select Decor Entry");
                        palette.selectedIndex = i;
                        EditorUtility.SetDirty(palette);

                        // nice: ping prefab in project
                        if(prefab) EditorGUIUtility.PingObject(prefab);
                    }

                    GUI.color = old;
                }

                GUILayout.FlexibleSpace();
            }
        }
    }

    Texture2D GetPrefabPreview(GameObject prefab)
    {
        if(prefab == null)
            return (Texture2D)EditorGUIUtility.IconContent("Prefab Icon").image;

        // Bigger preview (can be null until Unity generates it)
        Texture2D tex = AssetPreview.GetAssetPreview(prefab);
        if(tex != null) return tex;

        // Reliable fallback
        tex = AssetPreview.GetMiniThumbnail(prefab);
        if(tex != null) return tex;

        return (Texture2D)EditorGUIUtility.IconContent("Prefab Icon").image;
    }
    void OnSceneGUI(SceneView sv)
    {
        if(!paintMode) return;
        if(palette == null) return;

        Event e = Event.current;
        bool freeScatter = altFreeScatter && e.alt;
        if(e == null) return;

        // draw overlay + debug label at mouse cell
        if(showOverlay || showCellLabel)
        {
            if(TryGetMouseCell(out Vector2Int cell, out Vector3 cellWorld))
            {
                if(showOverlay)
                    DrawBrushOverlay(cell, cellWorld);

                if(showCellLabel)
                    DrawCellLabel(cell, cellWorld, e);
            }
        }


        // only paint on left down/drag
        if(e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
        if(e.button != 0) return;

        // Don’t let normal scene selection happen while painting
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        bool erase = eraseMode;
        if(e.shift) erase = !erase;
        if(freeScatter)
        {
            if(TryGetMouseWorld(out Vector3 w))
            {
                if(freeScatterThrottle)
                {
                    double t = EditorApplication.timeSinceStartup;

                    // distance gate
                    float dist = Vector3.Distance(new Vector3(w.x, 0f, w.z), new Vector3(lastScatterWorld.x, 0f, lastScatterWorld.z));
                    bool distOk = dist >= freeScatterMinWorldStep;

                    // time gate
                    bool timeOk = (t - lastScatterTime) >= freeScatterMinSeconds;

                    if(!distOk || !timeOk)
                        return;

                    lastScatterWorld = w;
                    lastScatterTime = t;
                }
                ApplyBrushFreeScatter(w, brushRadiusTiles, erase);
                e.Use();
                sv.Repaint();
            }
        }
        else
        {
            if(TryGetMouseCell(out Vector2Int centerCell, out _))
            {
                ApplyBrush(centerCell, brushRadiusTiles, erase);
                e.Use();
                sv.Repaint();
            }
        }
    }
    void ApplyBrushFreeScatter(Vector3 centerWorld, int radiusTiles, bool erase)
    {
        Transform root = GetOrCreateRoot();
        Transform paintedRoot = GetOrCreatePaintedDecorRoot(root);

        // Use the cell under mouse as the “anchor” for the tile brush
        Vector2Int centerCell = WorldToCell(centerWorld);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // Free scatter: still uses the tile brush footprint, but positions are random inside each cell
        for(int dy = -radiusTiles; dy <= radiusTiles; dy++)
            for(int dx = -radiusTiles; dx <= radiusTiles; dx++)
            {
                if(!erase && density < 1f && UnityEngine.Random.value > density)
                    continue;

                int cx = centerCell.x + dx;
                int cy = centerCell.y + dy;

                if(erase)
                {
                    // keep your existing fast erase-by-cell behaviour
                    EraseAtCell(paintedRoot, cx, cy);
                }
                else
                {
                    // random point inside the cell
                    Vector3 cellCorner = CellToWorld(cx, cy);

                    float jitter = tileSize * freeScatterJitter01;
                    float rx = UnityEngine.Random.Range(-jitter, tileSize + jitter);
                    float rz = UnityEngine.Random.Range(-jitter, tileSize + jitter);

                    Vector3 p = new Vector3(cellCorner.x + rx, cellCorner.y, cellCorner.z + rz);

                    // place using same entry rules but without snapping to center
                    PlaceAtWorld(paintedRoot, p, cx, cy, snapCenter: false);
                }
            }

        Undo.CollapseUndoOperations(group);
        MarkSceneDirty();
    }
    void PlaceAtWorld(Transform paintedRoot, Vector3 worldPos, int cellX, int cellY, bool snapCenter)
    {
        DecorEntry entry = PickEntry();
        if(entry == null || entry.prefab == null) return;

        bool blockDuplicates = !allowMultiplePerCell && !(altFreeScatter && freeScatterIgnoresOnePerCell);
        if(blockDuplicates && HasAnyAtCell(paintedRoot, cellX, cellY))
            return;
        if(altFreeScatter && freeScatterMinSpacingEnabled)
        {
            Vector3 testPos = worldPos;
            testPos.y = 0f;

            if(IsTooCloseToRecent(paintedRoot, testPos, freeScatterMinSpacing, freeScatterCheckMax))
                return;
        }
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
        Undo.RegisterCreatedObjectUndo(go, "Paint Decor");

        float yPos = origin.y + surfaceY + entry.yOffset;

        // Optional “true surface” y via raycast
        if(raycastForY)
        {
            Vector3 from = new Vector3(worldPos.x, origin.y + 50f, worldPos.z);
            if(Physics.Raycast(from, Vector3.down, out RaycastHit hit, 200f, raycastMask))
                yPos = hit.point.y + entry.yOffset;
        }

        Vector3 pos = worldPos;
        pos.y = yPos;

        if(snapCenter)
        {
            // snap to cell center (used by normal mode)
            Vector3 cellCorner = CellToWorld(cellX, cellY);
            pos.x = cellCorner.x + tileSize * 0.5f;
            pos.z = cellCorner.z + tileSize * 0.5f;
        }

        go.transform.position = pos;

        float yaw = 0f;
        if(entry.randomYaw)
            yaw = UnityEngine.Random.Range(entry.yawRange.x, entry.yawRange.y);

        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if(entry.randomScale)
        {
            float s = UnityEngine.Random.Range(entry.scaleRange.x, entry.scaleRange.y);
            go.transform.localScale = go.transform.localScale * s;
        }

        // still tag with cell so erase-by-cell works even in scatter mode
        go.name = $"{entry.prefab.name}_Cell_{cellX}_{cellY}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        go.transform.SetParent(paintedRoot, true);
    }
    void PlaceAtCell(Transform paintedRoot, int x, int y)
    {
        Vector3 cellCorner = CellToWorld(x, y);
        PlaceAtWorld(paintedRoot, cellCorner, x, y, snapCenter: snapToCellCenter);
    }
    void DrawBrushOverlay(Vector2Int centerCell, Vector3 centerWorld)
    {
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        float yDraw = origin.y + surfaceY + 0.05f;

        for(int dy = -brushRadiusTiles; dy <= brushRadiusTiles; dy++)
            for(int dx = -brushRadiusTiles; dx <= brushRadiusTiles; dx++)
            {
                int x = centerCell.x + dx;
                int y = centerCell.y + dy;

                Vector3 p = CellToWorld(x, y);

                // Cell corners in XZ
                Vector3 a = new Vector3(p.x, yDraw, p.z);
                Vector3 b = new Vector3(p.x + tileSize, yDraw, p.z);
                Vector3 c = new Vector3(p.x + tileSize, yDraw, p.z + tileSize);
                Vector3 d = new Vector3(p.x, yDraw, p.z + tileSize);

                // Filled rect + outline
                Handles.DrawSolidRectangleWithOutline(
                    new Vector3[] { a, b, c, d },
                    overlayFill,
                    overlayOutline
                );

                // Extra thickness pass
                Handles.color = overlayOutline;
                Handles.DrawAAPolyLine(overlayLineThickness, new Vector3[] { a, b, c, d, a });
            }
    }
    void DrawCellLabel(Vector2Int cell, Vector3 cellWorld, Event e)
    {
        float yDraw = origin.y + surfaceY + 0.08f;

        Vector3 labelPos = new Vector3(
            cellWorld.x + tileSize * 0.5f,
            yDraw,
            cellWorld.z + tileSize * 0.5f
        ) + labelOffset;

        bool effectiveErase = eraseMode;
        if(e != null && e.shift) effectiveErase = !effectiveErase;

        string mode = effectiveErase ? "ERASE" : "PAINT";

        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = labelColor;

        Handles.Label(labelPos, $"Cell: {cell.x},{cell.y}   [{mode}]", style);
    }

    bool TryGetMouseCell(out Vector2Int cell, out Vector3 cellWorld)
    {
        cell = default;
        cellWorld = default;

        Ray r = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

        // Prefer raycast if requested (lets you paint over meshes)
        if(raycastForY && Physics.Raycast(r, out RaycastHit hit, raycastMax, raycastMask))
        {
            cell = WorldToCell(hit.point);
            cellWorld = CellToWorld(cell.x, cell.y);
            return true;
        }

        // Otherwise project onto horizontal plane at origin.y
        Plane p = new Plane(Vector3.up, new Vector3(0f, origin.y, 0f));
        if(!p.Raycast(r, out float enter)) return false;

        Vector3 world = r.GetPoint(enter);
        cell = WorldToCell(world);
        cellWorld = CellToWorld(cell.x, cell.y);
        return true;
    }

    void ApplyBrush(Vector2Int center, int radius, bool erase)
    {
        Transform root = GetOrCreateRoot();
        Transform paintedRoot = GetOrCreatePaintedDecorRoot(root);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        for(int dy = -radius; dy <= radius; dy++)
            for(int dx = -radius; dx <= radius; dx++)
            {
                // density roll
                if(!erase && density < 1f && UnityEngine.Random.value > density)
                    continue;

                int x = center.x + dx;
                int y = center.y + dy;

                // Optional: you can clamp to your grid size if you want.
                // Here we let it be infinite, but it will still snap.

                if(erase)
                {
                    EraseAtCell(paintedRoot, x, y);
                }
                else
                {
                    PlaceAtCell(paintedRoot, x, y);
                }
            }

        Undo.CollapseUndoOperations(group);
        MarkSceneDirty();
    }

    //void PlaceAtCell(Transform paintedRoot, int x, int y)
    //{
    //    DecorEntry entry = PickEntry();
    //    if(entry == null || entry.prefab == null) return;

    //    if(!allowMultiplePerCell && HasAnyAtCell(paintedRoot, x, y))
    //        return;

    //    GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
    //    Undo.RegisterCreatedObjectUndo(go, "Paint Decor");

    //    Vector3 pos = CellToWorld(x, y);

    //    float yPos = origin.y + surfaceY + entry.yOffset;

    //    if(raycastForY)
    //    {
    //        Vector3 from = new Vector3(pos.x + tileSize * 0.5f, origin.y + 50f, pos.z + tileSize * 0.5f);
    //        if(Physics.Raycast(from, Vector3.down, out RaycastHit hit, 200f, raycastMask))
    //            yPos = hit.point.y + entry.yOffset;
    //    }

    //    if(snapToCellCenter)
    //        pos = new Vector3(pos.x + tileSize * 0.5f, yPos, pos.z + tileSize * 0.5f);
    //    else
    //        pos = new Vector3(pos.x, yPos, pos.z);

    //    go.transform.position = pos;

    //    float yaw = 0f;
    //    if(entry.randomYaw)
    //        yaw = UnityEngine.Random.Range(entry.yawRange.x, entry.yawRange.y);

    //    go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

    //    if(entry.randomScale)
    //    {
    //        float s = UnityEngine.Random.Range(entry.scaleRange.x, entry.scaleRange.y);
    //        go.transform.localScale = go.transform.localScale * s;
    //    }

    //    go.name = $"{entry.prefab.name}_Cell_{x}_{y}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
    //    go.transform.SetParent(paintedRoot, true);
    //}

    void EraseAtCell(Transform paintedRoot, int x, int y)
    {
        // Fast path: delete anything with "_Cell_x_y_" in the name
        string token = $"_Cell_{x}_{y}_";

        // iterate backwards
        for(int i = paintedRoot.childCount - 1; i >= 0; i--)
        {
            var c = paintedRoot.GetChild(i);
            if(c.name.Contains(token))
                Undo.DestroyObjectImmediate(c.gameObject);
        }
    }

    bool HasAnyAtCell(Transform paintedRoot, int x, int y)
    {
        string token = $"_Cell_{x}_{y}_";
        for(int i = 0; i < paintedRoot.childCount; i++)
        {
            if(paintedRoot.GetChild(i).name.Contains(token))
                return true;
        }
        return false;
    }

    DecorEntry PickEntry()
    {
        if(palette.entries == null || palette.entries.Count == 0) return null;

        if(!palette.useRandomFromEnabled)
        {
            int idx = Mathf.Clamp(palette.selectedIndex, 0, palette.entries.Count - 1);
            var e = palette.entries[idx];
            if(e != null && e.enabled) return e;

            // if selected disabled, fall back to first enabled
            foreach(var t in palette.entries)
                if(t != null && t.enabled) return t;

            return null;
        }
        else
        {
            // pick random enabled
            List<DecorEntry> enabled = new List<DecorEntry>();
            foreach(var t in palette.entries)
                if(t != null && t.enabled && t.prefab != null)
                    enabled.Add(t);

            if(enabled.Count == 0) return null;
            return enabled[UnityEngine.Random.Range(0, enabled.Count)];
        }
    }

    Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = world - origin;

        // This fixes “center-pivot tiles but corner-based math” issues
        //if(halfTilePivotBias)
        //{
        //    local.x += tileSize * 0.5f;// ===== Cell / Snapping Bias =====
        //    bool halfTilePivotBias = false;                 // enables +0.5 tile bias before flooring
        //    Vector2Int cellIndexOffset = Vector2Int.zero;   // if you're consistently "2 tiles off", set to (2,2)surfaceY
        //    local.z += tileSize * 0.5f;
        //}

        if(halfTilePivotBias)
        {
            local.x += tileSize * 0.5f;
            local.z += tileSize * 0.5f;
        }

        int x = Mathf.FloorToInt(local.x / tileSize);
        int y = Mathf.FloorToInt(local.z / tileSize);

        // This fixes “origin is 2 tiles off” style issues
        x -= cellIndexOffset.x;
        y -= cellIndexOffset.y;

        return new Vector2Int(x, y);
    }

    Vector3 CellToWorld(int x, int y)
    {
        return origin + new Vector3(x * tileSize, 0f, y * tileSize);
    }

    Transform GetOrCreateRoot()
    {
        Transform existing = FindRoot();
        if(existing != null) return existing;

        GameObject go = new GameObject(generatedRootName);
        Undo.RegisterCreatedObjectUndo(go, "Create Generated Root");

        if(parentOptional != null)
            go.transform.SetParent(parentOptional, true);

        go.transform.position = Vector3.zero;
        return go.transform;
    }

    Transform FindRoot()
    {
        if(parentOptional != null)
        {
            var t = parentOptional.Find(generatedRootName);
            if(t != null) return t;
        }

        var found = GameObject.Find(generatedRootName);
        return found ? found.transform : null;
    }

    Transform GetOrCreatePaintedDecorRoot(Transform root)
    {
        Transform decor = root.Find("DecorPainted");
        if(decor != null) return decor;

        GameObject go = new GameObject("DecorPainted");
        Undo.RegisterCreatedObjectUndo(go, "Create DecorPainted Root");
        go.transform.SetParent(root, true);
        return go.transform;
    }

    void ClearPaintedDecor()
    {
        Transform root = FindRoot();
        if(root == null) return;

        Transform painted = root.Find("DecorPainted");
        if(painted == null) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        for(int i = painted.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(painted.GetChild(i).gameObject);

        Undo.CollapseUndoOperations(group);
        MarkSceneDirty();
    }
    void EditorTick()
    {
        if(!paintMode) return;
        if(!showOverlay && !showCellLabel) return;

        SceneView.RepaintAll();
    }
    void MarkSceneDirty()
    {
        if(!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
#endif