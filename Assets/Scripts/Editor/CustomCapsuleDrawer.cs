using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[CustomEditor(typeof(CustomCapsule))]
public class CustomCapsuleDrawer : Editor
{
    private const int SEGMENT_COUNT = 10;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        CustomCapsule capsule = (CustomCapsule)target;
        float height = capsule.height;
        float radius = capsule.radius;
        if (GUILayout.Button("生成"))
        {
            BuildCapsuleMesh(height, radius, capsule);
        }
    }
    private void BuildCapsuleMesh(float lineLength, float radius, CustomCapsule capsule)
    {
        if(lineLength <= 0 || radius <=0)
        {
            Debug.LogError("Invaild Value");
            return;
        }
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();

        float halfLine = lineLength / 2;
        float angleStep = Mathf.PI / SEGMENT_COUNT;

        Vector3 center = new Vector3(0, 0, 0);
        vertices.Add(center);
        uvs.Add(new Vector2(0.5f, 0.5f));

        int centerIndex = 0;

        for (int i = 0; i <= SEGMENT_COUNT; i++)
        {
            float angle = i * angleStep;
            float x = Mathf.Cos(angle) * radius;
            float y = halfLine + Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, y, 0));
            uvs.Add(new Vector2(1.0f - (float)i / SEGMENT_COUNT, 1.0f));
        }

        for (int i = 0; i <= SEGMENT_COUNT; i++)
        {
            float angle = Mathf.PI + i * angleStep;
            float x = Mathf.Cos(angle) * radius;
            float y = -halfLine + Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, y, 0));
            uvs.Add(new Vector2((float)i / SEGMENT_COUNT, 0.0f));
        }

        var triangles = new List<int>();
        for (int i = 1; i < vertices.Count - 1; i++)
        {
            int next = i + 1;

            triangles.Add(centerIndex);
            triangles.Add(next);
            triangles.Add(i);
        }
        triangles.Add(centerIndex);
        triangles.Add(1);
        triangles.Add(vertices.Count - 1);

        Mesh mesh = new Mesh();

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mesh = SaveMesh(mesh, capsule.gameObject.name);

        if (mesh == null)
            return;
        capsule.meshFilter.sharedMesh = mesh;

        EditorUtility.SetDirty(capsule.meshFilter);

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
    }
    public Mesh SaveMesh(Mesh mesh, string name)
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();

        if (stage == null)
        {
            Debug.LogWarning("当前物体不是Prefab，相应的mesh不会被保存");
            return mesh;
        }

        string prefabPath = stage.assetPath;
        string directory = Path.GetDirectoryName(prefabPath);

        string meshPath = Path.Combine(directory, name + "_Mesh.asset").Replace("\\", "/");

        Mesh oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        if (oldMesh != null)
        {
            oldMesh.Clear();

            oldMesh.vertices = mesh.vertices;
            oldMesh.uv = mesh.uv;
            oldMesh.triangles = mesh.triangles;

            oldMesh.RecalculateNormals();
            oldMesh.RecalculateBounds();

            oldMesh.name = name + "_Mesh";

            EditorUtility.SetDirty(oldMesh);
            AssetDatabase.SaveAssets();

            DestroyImmediate(mesh);

            return oldMesh;
        }

        AssetDatabase.CreateAsset(mesh, meshPath);
        AssetDatabase.SaveAssets();

        return AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
    }
}
