using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ParticlePrefabMissingMaterialFixer : EditorWindow
{
    private DefaultAsset folder;
    private Material replacementMaterial;
    private bool includeInactive = true;

    [MenuItem("Tools/Particles/Fix Missing Materials (Prefabs)")]
    public static void ShowWindow()
    {
        GetWindow<ParticlePrefabMissingMaterialFixer>("Fix Particle Prefab Materials");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Fix Missing Materials on Particle Systems (Prefab Assets)", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        replacementMaterial = (Material)EditorGUILayout.ObjectField(
            new GUIContent("Replacement Material", "Used to fill missing (null or broken) material slots."),
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
            "Edits prefab ASSETS (not scene instances). Replaces missing material slots on ParticleSystemRenderer components, including broken 'Missing (Material)' references.",
            MessageType.Info);
    }

    private static void ProcessSelectedPrefabs(Material replacement, bool includeInactive)
    {
        var prefabPaths = new List<string>();

        foreach(var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if(string.IsNullOrEmpty(path)) continue;

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
        var prefabPaths = Deduplicate(FindPrefabPathsInFolders(folders));

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
                        // Fix BOTH:
                        // - null materials
                        // - broken "Missing (Material)" references stored as non-zero instance IDs in serialized data
                        if(FixMissingMaterialsOnRenderer(r, replacement, out int fixedSlots))
                        {
                            prefabChanged = true;
                            slotsFixedThisPrefab += fixedSlots;
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

    private static bool FixMissingMaterialsOnRenderer(ParticleSystemRenderer renderer, Material replacement, out int slotsFixed)
    {
        slotsFixed = 0;

        // Renderer stores materials in serialized arrays (Unity internal names).
        // We'll try m_SharedMaterials first; fallback to m_Materials if needed.
        var so = new SerializedObject(renderer);

        SerializedProperty matsProp = so.FindProperty("m_SharedMaterials");
        if(matsProp == null || !matsProp.isArray)
            matsProp = so.FindProperty("m_Materials");

        if(matsProp == null || !matsProp.isArray)
        {
            // Fallback to sharedMaterials if Unity changes internals.
            var mats = renderer.sharedMaterials;
            bool changedFallback = false;

            for(int i = 0; i < mats.Length; i++)
            {
                if(mats[i] == null)
                {
                    mats[i] = replacement;
                    slotsFixed++;
                    changedFallback = true;
                }
            }

            if(changedFallback)
                renderer.sharedMaterials = mats;

            return changedFallback;
        }

        bool changed = false;

        for(int i = 0; i < matsProp.arraySize; i++)
        {
            var el = matsProp.GetArrayElementAtIndex(i);

            // Key trick:
            // Missing reference can be:
            //   objectReferenceValue == null
            // AND objectReferenceInstanceIDValue != 0 (broken ref shown as "Missing (Material)")
            bool isMissingOrBroken =
                el.objectReferenceValue == null &&
                (el.objectReferenceInstanceIDValue != 0 || true); // also catches plain null slots

            if(isMissingOrBroken)
            {
                el.objectReferenceValue = replacement;
                slotsFixed++;
                changed = true;
            }
        }

        if(changed)
        {
            so.ApplyModifiedPropertiesWithoutUndo(); // we're editing prefab contents, not scene objects
            EditorUtility.SetDirty(renderer);
        }

        return changed;
    }

    private static List<string> Deduplicate(List<string> paths)
    {
        return new List<string>(new HashSet<string>(paths));
    }
}
