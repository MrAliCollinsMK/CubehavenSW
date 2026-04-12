#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AddMissingParticlesAtRuntime : EditorWindow
{
    private DefaultAsset folder;
    private Material replacementMaterial;
    private bool includeInactive = true;

    [MenuItem("Tools/Particles/Fix Missing Materials (Prefabs)")]
    public static void ShowWindow()
    {
        GetWindow<AddMissingParticlesAtRuntime>("Fix Particle Prefab Materials");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Fix Missing Materials on Particle Systems (Prefab Assets)", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        replacementMaterial = (Material)EditorGUILayout.ObjectField(
            new GUIContent("Replacement Material", "Used to fill missing (null) material slots."),
            replacementMaterial, typeof(Material), false);

        includeInactive = EditorGUILayout.Toggle(
            new GUIContent("Include Inactive", "If true, scans disabled objects inside prefabs too."),
            includeInactive);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Option A: Process a folder", EditorStyles.boldLabel);

        folder = (DefaultAsset)EditorGUILayout.ObjectField(
            new GUIContent("Folder", "Any folder under Assets/. All prefabs in it (and subfolders) will be processed."),
            folder, typeof(DefaultAsset), false);

        using(new EditorGUI.DisabledScope(replacementMaterial == null || folder == null || !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder))))
        {
            if(GUILayout.Button("Process All Prefabs In Folder"))
            {
                var folderPath = AssetDatabase.GetAssetPath(folder);
                ProcessPrefabsInFolders(new[] { folderPath }, replacementMaterial, includeInactive);
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Option B: Process selected prefabs", EditorStyles.boldLabel);

        using(new EditorGUI.DisabledScope(replacementMaterial == null))
        {
            if(GUILayout.Button("Process Selected Prefabs (Project Selection)"))
            {
                ProcessSelectedPrefabs(replacementMaterial, includeInactive);
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "This edits prefab ASSETS (not scene instances). It replaces ONLY missing (null) material slots on ParticleSystemRenderer components.",
            MessageType.Info);
    }

    private static void ProcessSelectedPrefabs(Material replacement, bool includeInactive)
    {
        var prefabPaths = new List<string>();

        foreach(var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);

            if(string.IsNullOrEmpty(path))
                continue;

            // Allow selecting folders too (we’ll expand them).
            if(AssetDatabase.IsValidFolder(path))
            {
                prefabPaths.AddRange(FindPrefabPathsInFolders(new[] { path }));
            }
            else if(path.EndsWith(".prefab"))
            {
                prefabPaths.Add(path);
            }
        }

        prefabPaths = Deduplicate(prefabPaths);

        if(prefabPaths.Count == 0)
        {
            Debug.LogWarning("[Fix Missing Particle Materials] No prefabs found in selection.");
            return;
        }

        ProcessPrefabPaths(prefabPaths, replacement, includeInactive);
    }

    private static void ProcessPrefabsInFolders(string[] folders, Material replacement, bool includeInactive)
    {
        var prefabPaths = FindPrefabPathsInFolders(folders);
        prefabPaths = Deduplicate(prefabPaths);

        if(prefabPaths.Count == 0)
        {
            Debug.LogWarning("[Fix Missing Particle Materials] No prefabs found in the specified folder(s).");
            return;
        }

        ProcessPrefabPaths(prefabPaths, replacement, includeInactive);
    }

    private static List<string> FindPrefabPathsInFolders(string[] folders)
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", folders);
        var paths = new List<string>(guids.Length);

        foreach(var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if(path.EndsWith(".prefab"))
                paths.Add(path);
        }

        return paths;
    }

    private static void ProcessPrefabPaths(List<string> prefabPaths, Material replacement, bool includeInactive)
    {
        int prefabsChanged = 0;
        int totalSlotsFixed = 0;
        int totalRenderersScanned = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach(var prefabPath in prefabPaths)
            {
                // Load prefab contents into an isolated editing scene
                GameObject root = null;

                try
                {
                    root = PrefabUtility.LoadPrefabContents(prefabPath);
                    if(root == null) continue;

                    var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(includeInactive);
                    totalRenderersScanned += renderers.Length;

                    bool prefabChanged = false;
                    int slotsFixedThisPrefab = 0;

                    foreach(var r in renderers)
                    {
                        var mats = r.sharedMaterials;
                        bool rendererChanged = false;

                        for(int i = 0; i < mats.Length; i++)
                        {
                            if(mats[i] == null)
                            {
                                mats[i] = replacement;
                                rendererChanged = true;
                                slotsFixedThisPrefab++;
                            }
                        }

                        if(rendererChanged)
                        {
                            r.sharedMaterials = mats;
                            prefabChanged = true;
                        }
                    }

                    if(prefabChanged)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        prefabsChanged++;
                        totalSlotsFixed += slotsFixedThisPrefab;
                    }
                }
                finally
                {
                    if(root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log(
            $"[Fix Missing Particle Materials] Prefabs scanned: {prefabPaths.Count} | Prefabs changed: {prefabsChanged} | " +
            $"ParticleSystemRenderers scanned: {totalRenderersScanned} | Missing slots fixed: {totalSlotsFixed}");
    }

    private static List<string> Deduplicate(List<string> paths)
    {
        var set = new HashSet<string>(paths);
        return new List<string>(set);
    }
}
#endif
