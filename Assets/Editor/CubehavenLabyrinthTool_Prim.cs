
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;

public class CubehavenLabyrinthTool_Prim : EditorWindow
{
    [Header("Target Grid Root")]
    string generatedRootName = "GeneratedGrid";
    Transform overrideRoot; // optional

    [Header("Hedges")]
    GameObject hedgePrefab;
    int hedgeHeightTiles = 2;
    bool useScaleInsteadOfStack = true;
    float hedgeScaleMultiplier = 1f;
    float hedgeYOffset = 0f;

    [Header("Maze (Prim)")]
    bool keepBorderWalls = true;
    bool randomizeSeed = true;
    int seed = 12345;

    [Range(0f, 1f)]
    float sparsity = 0.0f; // remove extra walls to add loops/openess after maze

    [Header("Perimeter Hedges")]
    int perimeterThickness = 1; // 1 = outermost ring, 2 = next ring in too
    bool placePerimeterLeft = true;
    bool placePerimeterRight = true;
    bool placePerimeterBottom = true;
    bool placePerimeterTop = true;

    [Header("Start/Finish (grid coords)")]
    Vector2Int start = new Vector2Int(1, 1);
    Vector2Int finish = new Vector2Int(21, 15);

    [Header("Solution Drawing")]
    bool drawSolution = true;
    float solutionThickness = 6f;
    float solutionLift = 0.08f;
    Color solutionColor = Color.red;

    // internal
    bool[,] isWall;
    readonly List<Vector3> solutionWorld = new List<Vector3>();
    bool hasSolution;

    const string HEDGE_ROOT = "LabyrinthHedges_Prim";

    [MenuItem("Tools/Cubehaven/Labyrinth Tool (Prim)")]
    public static void ShowWindow() => GetWindow<CubehavenLabyrinthTool_Prim>("Labyrinth (Prim)");

    void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
    void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    void OnGUI()
    {
        GUILayout.Label("Cubehaven Labyrinth Tool (Prim)", EditorStyles.boldLabel);

        overrideRoot = (Transform)EditorGUILayout.ObjectField("Grid Root Override", overrideRoot, typeof(Transform), true);
        generatedRootName = EditorGUILayout.TextField("Generated Root Name", generatedRootName);

        EditorGUILayout.Space(8);
        hedgePrefab = (GameObject)EditorGUILayout.ObjectField("Hedge Prefab", hedgePrefab, typeof(GameObject), false);
        hedgeHeightTiles = EditorGUILayout.IntSlider("Hedge Height (tiles)", hedgeHeightTiles, 1, 6);
        useScaleInsteadOfStack = EditorGUILayout.Toggle("Use Scale (else stack)", useScaleInsteadOfStack);
        hedgeScaleMultiplier = EditorGUILayout.FloatField("Scale Multiplier", Mathf.Max(0.01f, hedgeScaleMultiplier));
        hedgeYOffset = EditorGUILayout.FloatField("Hedge Y Offset", hedgeYOffset);

        EditorGUILayout.Space(8);
        randomizeSeed = EditorGUILayout.Toggle("Randomize Seed", randomizeSeed);
        using(new EditorGUI.DisabledScope(randomizeSeed))
            seed = EditorGUILayout.IntField("Seed", seed);

        keepBorderWalls = EditorGUILayout.Toggle("Keep Border Walls", keepBorderWalls);
        sparsity = EditorGUILayout.Slider("Sparsity (more open)", sparsity, 0f, 1f);

        EditorGUILayout.Space(8);
        GUILayout.Label("Perimeter Hedges", EditorStyles.boldLabel);
        perimeterThickness = EditorGUILayout.IntSlider("Perimeter Thickness", perimeterThickness, 1, 4);
        placePerimeterLeft = EditorGUILayout.Toggle("Left", placePerimeterLeft);
        placePerimeterRight = EditorGUILayout.Toggle("Right", placePerimeterRight);
        placePerimeterBottom = EditorGUILayout.Toggle("Bottom", placePerimeterBottom);
        placePerimeterTop = EditorGUILayout.Toggle("Top", placePerimeterTop);

        EditorGUILayout.Space(8);
        start = EditorGUILayout.Vector2IntField("Start", start);
        finish = EditorGUILayout.Vector2IntField("Finish", finish);

        EditorGUILayout.Space(8);
        drawSolution = EditorGUILayout.Toggle("Draw Solution", drawSolution);
        solutionThickness = EditorGUILayout.Slider("Thickness", solutionThickness, 1f, 16f);
        solutionLift = EditorGUILayout.FloatField("Lift Above Surface", solutionLift);

        EditorGUILayout.Space(10);

        using(new EditorGUI.DisabledScope(hedgePrefab == null))
        {
            if(GUILayout.Button("Generate Labyrinth (Place Hedges + Solve)"))
            {
                var root = ResolveRoot();
                if(root == null) return;

                var grid = root.GetComponent<CubehavenGridSettings>();
                if(grid == null)
                {
                    Debug.LogError("No CubehavenGridSettings found on root. Build grid once so settings are written.");
                    return;
                }

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();

                GenerateAndBuild(root, grid);

                Undo.CollapseUndoOperations(group);
                SceneView.RepaintAll();
            }
        }

        if(GUILayout.Button("Clear Labyrinth Hedges"))
        {
            var root = ResolveRoot();
            if(root == null) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            ClearChild(root, HEDGE_ROOT);
            hasSolution = false;
            solutionWorld.Clear();

            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll();
        }
    }

    void OnSceneGUI(SceneView sv)
    {
        if(!drawSolution || !hasSolution || solutionWorld.Count < 2) return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = solutionColor;
        Handles.DrawAAPolyLine(solutionThickness, solutionWorld.ToArray());
    }

    Transform ResolveRoot()
    {
        if(overrideRoot != null) return overrideRoot;

        var go = GameObject.Find(generatedRootName);
        return go ? go.transform : null;
    }

    void ClearChild(Transform root, string name)
    {
        var t = root.Find(name);
        if(t == null) return;

        for(int i = t.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(t.GetChild(i).gameObject);
    }

    void GenerateAndBuild(Transform root, CubehavenGridSettings grid)
    {
        int seedToUse = randomizeSeed ? (DateTime.Now.Millisecond + UnityEngine.Random.Range(0, 999999)) : seed;
        UnityEngine.Random.InitState(seedToUse);

        BuildMazeData_Prim(grid.width, grid.height);

        if(sparsity > 0f)
            ApplySparsity(grid.width, grid.height, sparsity);

        BuildHedges(root, grid);

        SolveAndCachePath(grid, grid.width, grid.height);
    }

    // Randomized Prim on an "odd cell" maze lattice (same coordinate conventions as your DFS tool).
    void BuildMazeData_Prim(int w, int h)
    {
        isWall = new bool[w, h];

        // all walls
        for(int yy = 0; yy < h; yy++)
            for(int xx = 0; xx < w; xx++)
                isWall[xx, yy] = true;

        Vector2Int SnapOdd(Vector2Int c)
        {
            int ox = Mathf.Clamp(c.x | 1, 1, w - 2);
            int oy = Mathf.Clamp(c.y | 1, 1, h - 2);
            return new Vector2Int(ox, oy);
        }

        bool InOddBounds(int x, int y) => x > 0 && y > 0 && x < w - 1 && y < h - 1;

        var s = SnapOdd(start);

        bool[,] inMaze = new bool[w, h];
        List<Vector2Int> frontier = new List<Vector2Int>(w * h / 4);

        Vector2Int[] dirs = new[]
        {
            new Vector2Int( 2, 0),
            new Vector2Int(-2, 0),
            new Vector2Int( 0, 2),
            new Vector2Int( 0,-2)
        };

        void AddFrontierFrom(Vector2Int cell)
        {
            for(int i = 0; i < dirs.Length; i++)
            {
                int nx = cell.x + dirs[i].x;
                int ny = cell.y + dirs[i].y;
                if(!InOddBounds(nx, ny)) continue;
                if(inMaze[nx, ny]) continue;

                var f = new Vector2Int(nx, ny);
                // avoid duplicates cheaply
                if(!frontier.Contains(f))
                    frontier.Add(f);
            }
        }

        // seed cell
        isWall[s.x, s.y] = false;
        inMaze[s.x, s.y] = true;
        AddFrontierFrom(s);

        while(frontier.Count > 0)
        {
            int pick = UnityEngine.Random.Range(0, frontier.Count);
            var f = frontier[pick];
            frontier.RemoveAt(pick);

            if(inMaze[f.x, f.y])
                continue;

            // choose a random neighbor already in maze (2 steps away)
            List<Vector2Int> neighborsIn = new List<Vector2Int>(4);
            for(int i = 0; i < dirs.Length; i++)
            {
                int nx = f.x + dirs[i].x;
                int ny = f.y + dirs[i].y;
                if(!InOddBounds(nx, ny)) continue;
                if(inMaze[nx, ny])
                    neighborsIn.Add(new Vector2Int(nx, ny));
            }

            // If none found, skip (should be rare)
            if(neighborsIn.Count == 0)
                continue;

            var ncell = neighborsIn[UnityEngine.Random.Range(0, neighborsIn.Count)];

            // carve frontier cell + wall between
            int wx = (f.x + ncell.x) / 2;
            int wy = (f.y + ncell.y) / 2;

            isWall[f.x, f.y] = false;
            isWall[wx, wy] = false;
            inMaze[f.x, f.y] = true;

            AddFrontierFrom(f);
        }

        if(keepBorderWalls)
        {
            for(int xx = 0; xx < w; xx++)
            {
                isWall[xx, 0] = true;
                isWall[xx, h - 1] = true;
            }
            for(int yy = 0; yy < h; yy++)
            {
                isWall[0, yy] = true;
                isWall[w - 1, yy] = true;
            }
        }
    }

    void ApplySparsity(int w, int h, float amt)
    {
        int attempts = Mathf.RoundToInt(w * h * amt * 1.25f);

        bool InBounds(int x, int y) => x >= 1 && y >= 1 && x < w - 1 && y < h - 1;

        for(int i = 0; i < attempts; i++)
        {
            int x = UnityEngine.Random.Range(1, w - 1);
            int y = UnityEngine.Random.Range(1, h - 1);
            if(!isWall[x, y]) continue;

            bool horiz = InBounds(x - 1, y) && InBounds(x + 1, y) && !isWall[x - 1, y] && !isWall[x + 1, y];
            bool vert  = InBounds(x, y - 1) && InBounds(x, y + 1) && !isWall[x, y - 1] && !isWall[x, y + 1];

            if(horiz || vert)
                isWall[x, y] = false;
        }
    }

    void BuildHedges(Transform root, CubehavenGridSettings grid)
    {
        ClearChild(root, HEDGE_ROOT);

        Transform hedgesRoot = root.Find(HEDGE_ROOT);
        if(hedgesRoot == null)
        {
            var go = new GameObject(HEDGE_ROOT);
            Undo.RegisterCreatedObjectUndo(go, "Create Labyrinth Hedges Root");
            go.transform.SetParent(root, true);
            hedgesRoot = go.transform;
        }

        int w = grid.width;
        int h = grid.height;

        int t = Mathf.Clamp(perimeterThickness, 1, Mathf.Min(w, h) / 2);

        Vector3 CellCenter(int x, int y)
        {
            return grid.origin + new Vector3((x + 0.5f) * grid.tileSize, 0f, (y + 0.5f) * grid.tileSize);
        }

        float baseY = grid.origin.y + grid.surfaceY + hedgeYOffset;

        for(int yy = 0; yy < h; yy++)
        for(int xx = 0; xx < w; xx++)
        {
            if(!isWall[xx, yy]) continue;

            bool onLeft   = (xx < t);
            bool onRight  = (xx >= w - t);
            bool onBottom = (yy < t);
            bool onTop    = (yy >= h - t);

            if(onLeft && !placePerimeterLeft) continue;
            if(onRight && !placePerimeterRight) continue;
            if(onBottom && !placePerimeterBottom) continue;
            if(onTop && !placePerimeterTop) continue;

            Vector3 pos = CellCenter(xx, yy);
            pos.y = baseY;

            if(useScaleInsteadOfStack)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(hedgePrefab);
                Undo.RegisterCreatedObjectUndo(go, "Create Hedge");
                go.transform.SetParent(hedgesRoot, true);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.identity;

                var s = go.transform.localScale;
                s.y *= hedgeHeightTiles * hedgeScaleMultiplier;
                go.transform.localScale = s;

                go.name = $"Hedge_{xx}_{yy}";
            }
            else
            {
                for(int i = 0; i < hedgeHeightTiles; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(hedgePrefab);
                    Undo.RegisterCreatedObjectUndo(go, "Create Hedge");
                    go.transform.SetParent(hedgesRoot, true);

                    Vector3 p = pos;
                    p.y = baseY + (i * grid.tileSize);
                    go.transform.position = p;
                    go.transform.rotation = Quaternion.identity;

                    if(Mathf.Abs(hedgeScaleMultiplier - 1f) > 0.0001f)
                        go.transform.localScale = go.transform.localScale * hedgeScaleMultiplier;

                    go.name = $"Hedge_{xx}_{yy}_{i}";
                }
            }
        }
    }

    void SolveAndCachePath(CubehavenGridSettings grid, int w, int h)
    {
        solutionWorld.Clear();
        hasSolution = false;

        Vector2Int SnapOdd(Vector2Int c)
        {
            int ox = Mathf.Clamp(c.x | 1, 1, w - 2);
            int oy = Mathf.Clamp(c.y | 1, 1, h - 2);
            return new Vector2Int(ox, oy);
        }

        var s = SnapOdd(start);
        var g = SnapOdd(finish);

        if(isWall[s.x, s.y] || isWall[g.x, g.y])
        {
            Debug.LogWarning("Start or Finish is on a wall after snapping. Try different coords.");
            return;
        }

        Queue<Vector2Int> q = new Queue<Vector2Int>();
        bool[,] vis = new bool[w, h];
        Vector2Int[,] prev = new Vector2Int[w, h];

        for(int yy = 0; yy < h; yy++)
            for(int xx = 0; xx < w; xx++)
                prev[xx, yy] = new Vector2Int(-999, -999);

        q.Enqueue(s);
        vis[s.x, s.y] = true;

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;

        while(q.Count > 0)
        {
            var p = q.Dequeue();
            if(p == g) break;

            for(int i = 0; i < 4; i++)
            {
                int nx = p.x + dx[i];
                int ny = p.y + dy[i];
                if(!InBounds(nx, ny)) continue;
                if(vis[nx, ny]) continue;
                if(isWall[nx, ny]) continue;

                vis[nx, ny] = true;
                prev[nx, ny] = p;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        if(!vis[g.x, g.y])
        {
            Debug.LogWarning("No path found (unexpected for Prim maze).");
            return;
        }

        List<Vector2Int> path = new List<Vector2Int>();
        var cur = g;
        path.Add(cur);
        while(cur != s)
        {
            cur = prev[cur.x, cur.y];
            path.Add(cur);
        }
        path.Reverse();

        Vector3 CellCenter(int x, int y)
        {
            return grid.origin + new Vector3((x + 0.5f) * grid.tileSize, 0f, (y + 0.5f) * grid.tileSize);
        }

        float yDraw = grid.origin.y + grid.surfaceY + solutionLift;

        for(int i = 0; i < path.Count; i++)
        {
            Vector3 p = CellCenter(path[i].x, path[i].y);
            p.y = yDraw;
            solutionWorld.Add(p);
        }

        hasSolution = true;
    }
}

