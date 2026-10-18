using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CivilGameJam.Menu.Editor
{
    /// <summary>Authors an editable tabletop blockout behind the existing screen-space menu.</summary>
    public static class MainMenuBlueprintAuthoring
    {
        private const string MaterialsPath = "Assets/Materials/MainMenu";

        [MenuItem("Tools/Civil Game Jam/Add Blueprint Menu Backdrop")]
        public static void ApplyToSavedMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author the menu backdrop outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(MainMenuSceneAuthoring.ScenePath);
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            if (menu == null) throw new InvalidOperationException("The saved main menu is missing.");
            Author(Camera.main);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the blueprint backdrop.");
            AssetDatabase.SaveAssets();
            Debug.Log("Blueprint menu backdrop saved: a flat ground, one thin blueprint rectangle, and two square figures. Menu UI layout preserved.");
        }

        [MenuItem("Tools/Civil Game Jam/Match Blueprint Reference Framing")]
        public static void MatchReferenceFraming()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Frame the menu outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(MainMenuSceneAuthoring.ScenePath);
            var camera = Camera.main;
            Author(camera);
            ComposeReference(camera, GameObject.Find("BlueprintBackdrop").transform.Find("Blueprint"));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the reference framing.");
            AssetDatabase.SaveAssets();
            Debug.Log("Reference framing saved: close oblique blueprint surface, an open lower-right corner, and the existing menu UI positions.");
        }

        internal static void Author(Camera camera)
        {
            if (camera == null) throw new InvalidOperationException("The menu needs its saved main camera.");
            // Reopening the tool preserves any subsequent edits to the authored placeholders.
            if (GameObject.Find("BlueprintBackdrop") != null) return;

            camera.transform.position = new Vector3(0f, 8f, -12f);
            camera.transform.LookAt(new Vector3(0f, 0f, 1f));
            camera.orthographic = false;
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(101, 104, 107, 255);

            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.66f, 0.68f, 0.72f);

            EnsureMaterialsFolder();
            var ground = Material("Ground", new Color32(109, 112, 115, 255));
            var blueprint = Material("Blueprint", new Color32(55, 96, 132, 255));
            var figureOne = Material("Figure01", new Color32(220, 165, 91, 255));
            var figureTwo = Material("Figure02", new Color32(148, 128, 183, 255));

            var backdrop = new GameObject("BlueprintBackdrop").transform;
            Box("Ground", backdrop, new Vector3(0f, -0.08f, 0f), new Vector3(200f, 0.16f, 200f), ground);

            var sheet = new GameObject("Blueprint").transform;
            sheet.SetParent(backdrop, false);
            sheet.localPosition = new Vector3(0f, 0f, 1f);
            sheet.localRotation = Quaternion.Euler(0f, -7f, 0f);
            Box("ThinRectangle", sheet, new Vector3(0f, 0.025f, 0f), new Vector3(14f, 0.05f, 9f), blueprint);

            // Each figure has an unscaled root on the paper so a sprite can replace its square.
            Figure("Figure01", sheet, new Vector3(-4.3f, 0.05f, -0.5f), figureOne, camera);
            Figure("Figure02", sheet, new Vector3(4.3f, 0.05f, -0.5f), figureTwo, camera);

            var light = new GameObject("KeyLight", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(backdrop, false);
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.45f;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.1f;
            RenderSettings.sun = light;
            ComposeReference(camera, sheet);
        }

        private static void ComposeReference(Camera camera, Transform sheet)
        {
            const float paperTop = 0.05f;
            const float width = 48f;
            const float depth = 36f;
            camera.transform.rotation = Quaternion.Euler(40f, -28f, 0f);
            camera.transform.position = new Vector3(0f, paperTop, 0f) - camera.transform.forward * 14f;
            camera.orthographic = false;
            camera.fieldOfView = 38f;
            camera.backgroundColor = new Color32(22, 24, 27, 255);

            // The near edge crosses only the lower-right corner. All other paper edges
            // extend beyond the frame, like the combined desk and parchment in the reference.
            camera.aspect = 1600f / 900f;
            try
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, paperTop, 0f));
                Vector3 OnPaper(float x, float y)
                {
                    var ray = camera.ViewportPointToRay(new Vector3(x, y, 0f));
                    if (!plane.Raycast(ray, out float distance))
                        throw new InvalidOperationException("The camera must look down at the blueprint.");
                    return ray.GetPoint(distance);
                }

                var nearLeft = OnPaper(0.64f, 0f);
                var nearRight = OnPaper(1f, 0.46f);
                var edgeRight = (nearRight - nearLeft).normalized;
                var intoPaper = Vector3.Cross(edgeRight, Vector3.up).normalized;
                var center = (nearLeft + nearRight) * 0.5f + intoPaper * (depth * 0.5f);
                center.y = 0f;
                sheet.position = center;
                sheet.rotation = Quaternion.LookRotation(intoPaper, Vector3.up);
                sheet.localScale = Vector3.one;
                var rectangle = sheet.Find("ThinRectangle");
                rectangle.localPosition = new Vector3(0f, paperTop * 0.5f, 0f);
                rectangle.localRotation = Quaternion.identity;
                rectangle.localScale = new Vector3(width, paperTop, depth);

                PlaceFigure(sheet.Find("Figure01"), OnPaper(0.24f, 0.44f), camera);
                PlaceFigure(sheet.Find("Figure02"), OnPaper(0.78f, 0.52f), camera);
            }
            finally { camera.ResetAspect(); }

            var ground = sheet.parent.Find("Ground");
            ground.localPosition = new Vector3(0f, -0.08f, 0f);
            ground.localRotation = Quaternion.identity;
            ground.localScale = new Vector3(200f, 0.16f, 200f);
            var groundMaterial = ground.GetComponent<MeshRenderer>().sharedMaterial;
            groundMaterial.SetColor("_BaseColor", new Color32(22, 24, 27, 255));
            groundMaterial.SetColor("_Color", new Color32(22, 24, 27, 255));
            EditorUtility.SetDirty(groundMaterial);
        }

        private static void PlaceFigure(Transform figure, Vector3 anchor, Camera camera)
        {
            figure.position = anchor;
            var square = figure.Find("SquarePlaceholder");
            square.rotation = camera.transform.rotation;
            square.position = anchor + camera.transform.up * (square.localScale.y * 0.5f);
        }

        private static void Figure(string name, Transform sheet, Vector3 position, Material material, Camera camera)
        {
            const float side = 2f;
            var root = new GameObject(name).transform;
            root.SetParent(sheet, false);
            root.localPosition = position;
            var square = Box("SquarePlaceholder", root, Vector3.zero, new Vector3(side, side, 0.04f), material);
            square.rotation = camera.transform.rotation;
            square.position = root.position + Vector3.up * (side * 0.5f * camera.transform.up.y);
        }

        private static Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            return box.transform;
        }

        private static void EnsureMaterialsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialsPath)) AssetDatabase.CreateFolder("Assets/Materials", "MainMenu");
        }

        private static Material Material(string name, Color color)
        {
            string path = MaterialsPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The project's URP Lit shader is missing.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0f);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
