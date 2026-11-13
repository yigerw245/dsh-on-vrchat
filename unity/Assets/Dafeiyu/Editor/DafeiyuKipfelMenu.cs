using System;
using System.Linq;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DafeiyuKipfelMenu
{
    private const string Source = "Assets/DafeiyuKipfel/deep.unity";
    private const string World = "Assets/Dafeiyu/Dafeiyu Test World.unity";
    private const string Prefab = "Assets/DafeiyuKipfel/Dafeiyu Kipfel.prefab";

    [MenuItem("Tools/Dafeiyu/Install Kipfel Model in Test World")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
            throw new InvalidOperationException("UdonSharp compilation failed.");

        Scene world = EditorSceneManager.OpenScene(World);
        var npc = world.GetRootGameObjects().Select(go => go.GetComponent<DafeiyuNpc>()).Single(n => n != null);
        Scene source = EditorSceneManager.OpenScene(Source, OpenSceneMode.Additive);
        try
        {
            GameObject model = source.GetRootGameObjects().Single(go => go.name == "Kipfel");
            foreach (Transform node in model.GetComponentsInChildren<Transform>(true))
            {
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(node.gameObject))
                    PrefabUtility.UnpackPrefabInstance(node.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            foreach (Transform node in model.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
            }
            RelaxArms(model.transform);
            Bounds bounds = ValidateAndMeasure(model);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(model, Prefab);
            if (prefab == null) throw new InvalidOperationException("Could not save the Kipfel prefab.");

            Transform visual = npc.whale;
            for (int i = visual.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(visual.GetChild(i).gameObject);
            visual.name = "Kipfel model";
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, world);
            instance.transform.SetParent(visual, false);
            instance.transform.localPosition = Vector3.down * bounds.min.y;
            instance.transform.localRotation = Quaternion.identity;
            npc.tail = null;
            npc.subtitleAnchor.localPosition = Vector3.up * (bounds.size.y + 0.18f);
            npc.subtitleAnchor.localRotation = Quaternion.identity;
            npc.subtitles.fontSize = 0.9f;
            npc.subtitles.rectTransform.sizeDelta = new Vector2(2.25f, 0.55f);
            FitKipfelColliders(npc.gameObject, bounds.size.y);
            UdonSharpEditorUtility.CopyProxyToUdon(npc);
            EditorSceneManager.MarkSceneDirty(world);
            if (!EditorSceneManager.SaveScene(world))
                throw new InvalidOperationException("Could not save the Kipfel NPC test world.");
            Debug.Log("[Dafeiyu] Installed Kipfel and retained the NPC audio and dialogue.");
        }
        finally
        {
            EditorSceneManager.CloseScene(source, true);
        }
    }

    private static void FitKipfelColliders(GameObject npc, float height)
    {
        var torso = npc.GetComponents<BoxCollider>().First(collider => !collider.isTrigger);
        torso.center = Vector3.up * (height * 0.36f);
        torso.size = new Vector3(0.32f, height * 0.72f, 0.27f);

        var head = npc.GetComponent<SphereCollider>();
        if (head == null) head = npc.AddComponent<SphereCollider>();
        head.center = Vector3.up * (height * 0.83f);
        head.radius = height * 0.17f;

        var bump = npc.GetComponents<BoxCollider>().FirstOrDefault(collider => collider.isTrigger);
        if (bump == null) bump = npc.AddComponent<BoxCollider>();
        bump.isTrigger = true;
        bump.center = Vector3.up * (height * 0.49f);
        bump.size = new Vector3(0.54f, height * 0.98f, 0.50f);
    }

    private static void RelaxArms(Transform model)
    {
        foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "UpperArm.L")
                bone.rotation = Quaternion.AngleAxis(68f, model.forward) * bone.rotation;
            else if (bone.name == "UpperArm.R")
                bone.rotation = Quaternion.AngleAxis(-68f, model.forward) * bone.rotation;
        }
    }

    private static Bounds ValidateAndMeasure(GameObject model)
    {
        Renderer[] visible = model.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
        if (visible.Length == 0) throw new InvalidOperationException("The Kipfel model has no visible meshes.");
        Bounds bounds = default;
        bool hasVertex = false;
        var baked = new Mesh();
        foreach (Renderer renderer in visible)
        {
            if (renderer.sharedMaterials.Any(material => material == null || material.shader == null ||
                                                       material.shader.name == "Hidden/InternalErrorShader"))
                throw new InvalidOperationException("Missing material or shader: " + renderer.name);
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                if (skinned.sharedMesh == null) throw new InvalidOperationException("Missing mesh: " + renderer.name);
                skinned.BakeMesh(baked);
                mesh = baked;
            }
            else if (renderer.TryGetComponent<MeshFilter>(out var filter))
            {
                mesh = filter.sharedMesh;
            }
            if (mesh == null) continue;
            if (renderer.name != "Body" && renderer.name != "Shoes" && renderer.name != "Headband") continue;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 point = renderer.transform.TransformPoint(vertex);
                if (Mathf.Abs(point.x) > 0.75f || Mathf.Abs(point.z) > 0.75f || point.y < -0.15f || point.y > 1.65f)
                    continue;
                if (hasVertex) bounds.Encapsulate(point);
                else bounds = new Bounds(point, Vector3.zero);
                hasVertex = true;
            }
        }
        Object.DestroyImmediate(baked);
        if (!hasVertex) throw new InvalidOperationException("The Kipfel model has no visible geometry.");
        Debug.Log("[Dafeiyu] Kipfel bounds: " + bounds);
        return bounds;
    }
}
