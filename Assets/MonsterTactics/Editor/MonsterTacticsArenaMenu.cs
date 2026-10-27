using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MonsterTactics;

namespace MonsterTactics.Editor
{
    public static class MonsterTacticsArenaMenu
    {
        private const int Columns = 8;
        private const int Rows = 8;
        private const float Radius = 0.68f;
        private const float Gap = 0.075f;
        private const float TileHeight = 0.18f;

        private const string RootName = "MonsterTactics Arena";
        private const string GeneratedFolder = "Assets/MonsterTactics/Generated";

        [MenuItem("MonsterTactics/Create Starter Arena", priority = 20)]
        private static void CreateStarterArena()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Exit Play mode before rebuilding the arena.", "OK");
                return;
            }

            GameObject previous = GameObject.Find(RootName);
            if (previous != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Rebuild MonsterTactics Arena?",
                    "This will replace the existing 'MonsterTactics Arena' object. Other scene objects won't be deleted.",
                    "Replace", "Cancel");
                if (!replace) return;
                Undo.DestroyObjectImmediate(previous);
            }

            EnsureFolders();
            Mesh tileMesh = GetOrCreateMesh();

            Material playerA = GetOrCreateMaterial("Player Grass A", new Color(0.27f, 0.64f, 0.43f));
            Material playerB = GetOrCreateMaterial("Player Grass B", new Color(0.31f, 0.72f, 0.47f));
            Material enemyA = GetOrCreateMaterial("Enemy Stone A", new Color(0.38f, 0.49f, 0.62f));
            Material enemyB = GetOrCreateMaterial("Enemy Stone B", new Color(0.43f, 0.56f, 0.66f));
            Material groundMaterial = GetOrCreateMaterial("Arena Ground", new Color(0.16f, 0.31f, 0.29f));

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create MonsterTactics Arena");

            Transform tileParent = new GameObject("Hex Tiles").transform;
            tileParent.SetParent(root.transform, false);

            float horizontalStep = Mathf.Sqrt(3f) * (Radius + Gap);
            float verticalStep = 1.5f * (Radius + Gap);
            List<Vector3> positions = new List<Vector3>(Columns * Rows);

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    positions.Add(new Vector3(
                        horizontalStep * (col + (row % 2) * 0.5f),
                        0f,
                        verticalStep * row));
                }
            }

            Vector3 boardCenter = Vector3.zero;
            foreach (Vector3 position in positions) boardCenter += position;
            boardCenter /= positions.Count;

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    bool playerSide = row < Rows / 2;
                    Material material = playerSide
                        ? (((row + col) % 2 == 0) ? playerA : playerB)
                        : (((row + col) % 2 == 0) ? enemyA : enemyB);

                    GameObject tile = new GameObject($"Tile {col}-{row}");
                    tile.transform.SetParent(tileParent, false);
                    tile.transform.localPosition = positions[row * Columns + col] - boardCenter;

                    MeshFilter filter = tile.AddComponent<MeshFilter>();
                    filter.sharedMesh = tileMesh;
                    MeshRenderer renderer = tile.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    MeshCollider collider = tile.AddComponent<MeshCollider>();
                    collider.sharedMesh = tileMesh;
                    HexTile hex = tile.AddComponent<HexTile>();
                    hex.Configure(col, row, playerSide, material.color);
                }
            }

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(1.48f, 1f, 1.26f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            Object.DestroyImmediate(ground.GetComponent<MeshCollider>());

            GameObject controller = new GameObject("Arena Controller");
            controller.transform.SetParent(root.transform, false);
            TileSelection selection = controller.AddComponent<TileSelection>();

            Camera camera = SetupCamera();
            SerializedObject selectionSerialized = new SerializedObject(selection);
            selectionSerialized.FindProperty("targetCamera").objectReferenceValue = camera;
            selectionSerialized.ApplyModifiedPropertiesWithoutUndo();

            EnsureLighting();
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("MonsterTactics: built 64 hex tiles. Save the scene, press Play, then left-click a tile to select it.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/MonsterTactics"))
                AssetDatabase.CreateFolder("Assets", "MonsterTactics");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/MonsterTactics", "Generated");
        }

        private static Mesh GetOrCreateMesh()
        {
            string path = GeneratedFolder + "/Hex Prism.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            Mesh mesh = new Mesh { name = "MonsterTactics Hex Prism" };
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            vertices.Add(new Vector3(0f, TileHeight, 0f));

            // Separate top/sides create crisp, flat-shaded tile edges.
            for (int i = 0; i < 6; i++)
                vertices.Add(Corner(i, TileHeight));

            for (int i = 0; i < 6; i++)
            {
                triangles.Add(0);
                triangles.Add(1 + (i + 1) % 6);
                triangles.Add(1 + i);
            }

            for (int i = 0; i < 6; i++)
            {
                int baseIndex = vertices.Count;
                Vector3 a = Corner(i, TileHeight);
                Vector3 b = Corner((i + 1) % 6, TileHeight);
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(Corner(i, 0f));
                vertices.Add(Corner((i + 1) % 6, 0f));

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 2);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Vector3 Corner(int index, float height)
        {
            float angle = (60f * index + 30f) * Mathf.Deg2Rad;
            return new Vector3(Radius * Mathf.Cos(angle), height, Radius * Mathf.Sin(angle));
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = GeneratedFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
                throw new System.InvalidOperationException("No suitable shader found. Create a Universal 3D (URP) project.");

            Material material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Camera SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Camera[] cameras = Object.FindObjectsByType<Camera>();
                if (cameras.Length > 0) camera = cameras[0];
            }

            if (camera == null)
            {
                GameObject cameraObject = new GameObject("MonsterTactics Camera");
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create MonsterTactics camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            Undo.RecordObject(camera, "Configure MonsterTactics camera");
            Undo.RecordObject(camera.transform, "Position MonsterTactics camera");
            camera.gameObject.name = "MonsterTactics Camera";
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 8.2f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.15f, 0.22f);
            camera.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            camera.transform.position = -camera.transform.forward * 18f;
            return camera;
        }

        private static void EnsureLighting()
        {
            Light[] lights = Object.FindObjectsByType<Light>();
            foreach (Light existing in lights)
            {
                if (existing.type == LightType.Directional && existing.enabled) return;
            }

            GameObject sunlight = new GameObject("MonsterTactics Sun");
            Undo.RegisterCreatedObjectUndo(sunlight, "Create MonsterTactics sun");
            Light light = sunlight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;
            light.color = new Color(1f, 0.94f, 0.82f);
            sunlight.transform.rotation = Quaternion.Euler(55f, -40f, 0f);
        }
    }
}
