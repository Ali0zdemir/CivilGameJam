using System;
using System.Collections.Generic;
using System.Linq;
using CivilGameJam.Upgrades;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardApartmentModelAuthoring
    {
        private const string Art = "Assets/EfeArts/EmreArts/";
        private const string Definitions = "Assets/Art/Buildings/ImportedGeometry/";

        [MenuItem("Tools/Card Game/Save Authored Apartment Upgrade Models")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save apartment models in Edit Mode.");
            foreach (string name in new[] { "House", "Apartment_02", "Apartment_03", "Apartment_04" }) SavePrefab(name);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved normal and elevated apartments, seismic isolators and surface data on all four residential prefabs.");
        }

        [MenuItem("Tools/Card Game/Save Authored Apartment Sprinklers")]
        public static void SaveSprinklersForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save apartment sprinklers in Edit Mode.");
            foreach (string name in new[] { "House", "Apartment_02", "Apartment_03", "Apartment_04" })
            {
                string path = "Assets/Art/Buildings/Prefabs/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var motion = root.transform.Find("Apartment_Motion");
                    ConfigureSprinklers(root.GetComponent<ApartmentUpgradeModel>(), motion.Find("Apartments_Normal"), motion.Find("Apartments_Elevated"));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the apartment's original sprinkler heads and bases on both model variants.");
        }

        [MenuItem("Tools/Card Game/Save Magnetic Elevator Supports")]
        public static void SaveMagneticSupportsForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save magnetic supports in Edit Mode.");
            foreach (string name in new[] { "House", "Apartment_02", "Apartment_03", "Apartment_04" })
            {
                string path = "Assets/Art/Buildings/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigureElevatorSupports(root.GetComponent<ApartmentUpgradeModel>(), root.transform.Find("Apartment_Motion/Apartments_Elevated"));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            CardUpgradePresentationAuthoring.SavePointerCoverageForBatch();
            Debug.Log("Saved magnetic elevator supports and complete worker portrait pointer coverage.");
        }

        private static void SavePrefab(string name)
        {
            string path = "Assets/Art/Buildings/Prefabs/" + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var motion = new GameObject("Apartment_Motion").transform;
                motion.SetParent(root.transform, false);
                var normal = Model("Apartments (1).prefab", "Apartments_Normal", motion);
                var elevated = Model("Apartments(Elevated).prefab", "Apartments_Elevated", motion);
                var normalGround = Ground(normal);
                var ground = Object.Instantiate(normalGround.gameObject, root.transform);
                ground.name = "Apartment_Ground";
                ground.transform.SetPositionAndRotation(normalGround.position, normalGround.rotation);
                ground.transform.localScale = normalGround.lossyScale;
                normalGround.gameObject.SetActive(false);
                Ground(elevated).gameObject.SetActive(false);
                foreach (var t in normal.GetComponentsInChildren<Transform>(true).Concat(elevated.GetComponentsInChildren<Transform>(true)))
                    if (t.name.StartsWith("Sprinkler", StringComparison.Ordinal)) t.gameObject.SetActive(false);
                Transform normalIsolator = Isolator(normal), elevatedIsolator = Isolator(elevated);
                normalIsolator.gameObject.SetActive(false);
                elevatedIsolator.gameObject.SetActive(false);
                var apartment = root.GetComponent<ApartmentUpgradeModel>();
                if (apartment == null) apartment = root.AddComponent<ApartmentUpgradeModel>();
                Set(apartment, ("motionRoot", motion), ("normalModel", normal.gameObject), ("elevatedModel", elevated.gameObject),
                    ("normalIsolators", normalIsolator), ("elevatedIsolators", elevatedIsolator));
                var surfaces = new List<BuildingModelGeometry>();
                var supports = new List<Bounds>();
                for (int index = 1; index <= 6; index++)
                {
                    var node = new GameObject("Apartment_Surface_" + index).transform;
                    node.SetParent(motion, false);
                    var geometry = node.gameObject.AddComponent<BuildingModelGeometry>();
                    var normalMesh = normal.Find("Apartment" + index).GetComponent<MeshFilter>();
                    var elevatedMesh = elevated.Find("Apartment" + index + "(Elevated)").GetComponent<MeshFilter>();
                    var normalDefinition = Bake(normalMesh, node, "EmreApartment" + index + "_Normal");
                    var elevatedDefinition = Bake(elevatedMesh, node, "EmreApartment" + index + "_Elevated");
                    Set(geometry, ("definition", normalDefinition), ("modelMesh", normalMesh), ("apartmentModel", apartment),
                        ("elevatedDefinition", elevatedDefinition), ("elevatedMesh", elevatedMesh));
                    surfaces.Add(geometry);
                    Bounds foot = elevatedDefinition.FoundationBounds;
                    float height = elevatedDefinition.GroundFloorHeight;
                    float width = Mathf.Min(.065f, foot.size.x * .16f), depth = Mathf.Min(.065f, foot.size.z * .16f);
                    for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        supports.Add(new Bounds(new Vector3(foot.center.x + x * (foot.extents.x - width * .5f), foot.min.y + height * .5f,
                            foot.center.z + z * (foot.extents.z - depth * .5f)), new Vector3(width, height, depth)));
                }
                var apartmentData = new SerializedObject(apartment);
                var supportProperty = apartmentData.FindProperty("elevatedSupports");
                supportProperty.arraySize = supports.Count;
                for (int i = 0; i < supports.Count; i++) supportProperty.GetArrayElementAtIndex(i).boundsValue = supports[i];
                apartmentData.ApplyModifiedPropertiesWithoutUndo();
                var rootGeometry = root.GetComponent<BuildingModelGeometry>();
                var data = new SerializedObject(rootGeometry);
                var parts = data.FindProperty("parts"); parts.arraySize = surfaces.Count;
                for (int i = 0; i < surfaces.Count; i++) parts.GetArrayElementAtIndex(i).objectReferenceValue = surfaces[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                var collider = root.GetComponent<BoxCollider>();
                collider.center = rootGeometry.BodyBounds.center;
                collider.size = rootGeometry.BodyBounds.size;
                ConfigureSprinklers(apartment, normal, elevated);
                ConfigureElevatorSupports(apartment, elevated);
                elevated.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform Model(string asset, string name, Transform parent)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + asset), parent);
            model.name = name;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localPosition = Vector3.up * .9593861f;
            Bounds ground = BoundsIn(Ground(model.transform).GetComponent<MeshFilter>(), parent);
            model.transform.localPosition -= new Vector3(ground.center.x, 0f, ground.center.z);
            return model.transform;
        }

        private static Transform Ground(Transform model) => model.Cast<Transform>().Single(t => t.name.Contains("(Ground)"));
        private static Transform Isolator(Transform model) => model.Cast<Transform>().Single(t => t.name.Contains("SeismicIsolators"));

        private static void ConfigureSprinklers(ApartmentUpgradeModel apartment, Transform normal, Transform elevated)
        {
            var data = new SerializedObject(apartment);
            var pairs = data.FindProperty("sprinklers"); pairs.arraySize = 6;
            for (int index = 0; index < pairs.arraySize; index++)
            {
                string name = "Sprinkler" + (index + 1);
                var normalBody = normal.Find("Apartment" + (index + 1));
                var elevatedBody = elevated.Find("Apartment" + (index + 1) + "(Elevated)");
                var head = normal.Find(name);
                var foot = normal.Find(name + "(Base)");
                if (head == null || foot == null) throw new InvalidOperationException("Missing authored sprinkler " + name);
                var elevatedHead = CopySprinkler(head, normalBody, elevatedBody, elevated);
                var elevatedFoot = CopySprinkler(foot, normalBody, elevatedBody, elevated);
                var pair = pairs.GetArrayElementAtIndex(index);
                pair.FindPropertyRelative("normalHead").objectReferenceValue = head;
                pair.FindPropertyRelative("normalBase").objectReferenceValue = foot;
                pair.FindPropertyRelative("elevatedHead").objectReferenceValue = elevatedHead;
                pair.FindPropertyRelative("elevatedBase").objectReferenceValue = elevatedFoot;
                foreach (Transform part in new[] { head, foot, elevatedHead, elevatedFoot }) part.gameObject.SetActive(false);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform CopySprinkler(Transform source, Transform normalBody, Transform elevatedBody, Transform elevated)
        {
            var existing = elevated.Find(source.name);
            if (existing != null) return existing;
            var copy = Object.Instantiate(source.gameObject, elevated).transform;
            copy.name = source.name;
            copy.SetPositionAndRotation(elevatedBody.TransformPoint(normalBody.InverseTransformPoint(source.position)),
                elevatedBody.rotation * Quaternion.Inverse(normalBody.rotation) * source.rotation);
            copy.localScale = source.localScale;
            return copy;
        }

        private static void ConfigureElevatorSupports(ApartmentUpgradeModel apartment, Transform elevated)
        {
            var group = elevated.Find("Elevator_Supports");
            if (group == null) { group = new GameObject("Elevator_Supports").transform; group.SetParent(elevated, false); }
            var sourceMeshes = AssetDatabase.LoadAllAssetsAtPath(Art + "Apartments(Elevated).fbx").OfType<Mesh>().ToArray();
            for (int index = 1; index <= 6; index++)
            {
                var body = elevated.Find("Apartment" + index + "(Elevated)");
                var original = sourceMeshes.Single(mesh => mesh.name == body.name);
                float floor = ElevatorFloor(original);
                var upper = Object.Instantiate(original); upper.name = "Apartment" + index + "_ElevatorBody";
                var lower = Object.Instantiate(original); lower.name = "Apartment" + index + "_ElevatorSupports";
                var vertices = original.vertices;
                for (int submesh = 0; submesh < original.subMeshCount; submesh++)
                {
                    var topTriangles = new List<int>(); var footTriangles = new List<int>();
                    int[] triangles = original.GetTriangles(submesh);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        float highest = Mathf.Max(vertices[triangles[i]].y, Mathf.Max(vertices[triangles[i + 1]].y, vertices[triangles[i + 2]].y));
                        var target = highest <= floor + .0001f ? footTriangles : topTriangles;
                        target.Add(triangles[i]); target.Add(triangles[i + 1]); target.Add(triangles[i + 2]);
                    }
                    upper.SetTriangles(topTriangles, submesh); lower.SetTriangles(footTriangles, submesh);
                }
                if (upper.triangles.Length == 0 || lower.triangles.Length == 0) throw new InvalidOperationException("Cannot split elevator supports on " + body.name);
                var upperAsset = SaveMesh(upper, Definitions + "EmreApartment" + index + "_ElevatorBody.asset");
                var lowerAsset = SaveMesh(lower, Definitions + "EmreApartment" + index + "_ElevatorSupports.asset");
                body.GetComponent<MeshFilter>().sharedMesh = upperAsset;
                var child = group.Find("Apartment" + index + "_Supports");
                if (child == null) { child = new GameObject("Apartment" + index + "_Supports", typeof(MeshFilter), typeof(MeshRenderer)).transform; child.SetParent(group, false); }
                child.localPosition = body.localPosition; child.localRotation = body.localRotation; child.localScale = body.localScale;
                child.GetComponent<MeshFilter>().sharedMesh = lowerAsset;
                child.GetComponent<MeshRenderer>().sharedMaterials = body.GetComponent<MeshRenderer>().sharedMaterials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(body.GetComponent<MeshFilter>());
            }
            Set(apartment, ("elevatorSupports", group));
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); return existing;
        }

        private static float ElevatorFloor(Mesh mesh)
        {
            var levels = new Dictionary<int, float>();
            var vertices = mesh.vertices; int[] triangles = mesh.triangles;
            float bottom = mesh.bounds.min.y;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                if (Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > .0001f) continue;
                float height = (a.y + b.y + c.y) / 3f;
                if (height < bottom + .04f || height > bottom + mesh.bounds.size.y * .45f) continue;
                int key = Mathf.RoundToInt(height * 10000f);
                levels.TryGetValue(key, out float area);
                levels[key] = area + Vector3.Cross(b - a, c - a).magnitude * .5f;
            }
            float minimumArea = mesh.bounds.size.x * mesh.bounds.size.z * .35f;
            foreach (var level in levels.OrderBy(pair => pair.Key)) if (level.Value >= minimumArea) return level.Key / 10000f;
            throw new InvalidOperationException("Cannot locate the first raised floor of " + mesh.name);
        }

        private static BuildingModelDefinition Bake(MeshFilter filter, Transform relativeTo, string name)
        {
            string path = Definitions + name + "_Surfaces.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BuildingModelDefinition>(path);
            if (definition == null) { definition = ScriptableObject.CreateInstance<BuildingModelDefinition>(); AssetDatabase.CreateAsset(definition, path); }
            Mesh mesh = filter.sharedMesh;
            Matrix4x4 matrix = relativeTo.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 v in vertices) bounds.Encapsulate(v);
            var roofTriangles = new List<Vector3>();
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                if (Vector3.Cross(b - a, c - a).normalized.y < .5f || (a.y + b.y + c.y) / 3f < bounds.min.y + bounds.size.y * .6f) continue;
                roofTriangles.AddRange(new[] { a, b, c });
            }
            if (roofTriangles.Count == 0) throw new InvalidOperationException("No roof surface on " + filter.name);
            var roof = new Bounds(roofTriangles[0], Vector3.zero);
            foreach (Vector3 v in roofTriangles) roof.Encapsulate(v);
            float groundFloor = Mathf.Clamp(bounds.size.y * .18f, .18f, .32f);
            var foundation = new Bounds(new Vector3(bounds.center.x, bounds.min.y + .02f, bounds.center.z), new Vector3(bounds.size.x, .04f, bounds.size.z));
            var data = new SerializedObject(definition);
            data.FindProperty("residential").boolValue = true;
            data.FindProperty("closedMesh").objectReferenceValue = mesh;
            data.FindProperty("bodyBounds").boundsValue = bounds;
            data.FindProperty("foundationBounds").boundsValue = foundation;
            data.FindProperty("roofBounds").boundsValue = roof;
            data.FindProperty("groundFloorHeight").floatValue = groundFloor;
            data.FindProperty("sprinklerAnchor").vector3Value = RoofAnchor(roof, roofTriangles);
            var points = data.FindProperty("roofTriangles"); points.arraySize = roofTriangles.Count;
            for (int i = 0; i < roofTriangles.Count; i++) points.GetArrayElementAtIndex(i).vector3Value = roofTriangles[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static Bounds BoundsIn(MeshFilter filter, Transform relativeTo)
        {
            Matrix4x4 matrix = relativeTo.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(filter.sharedMesh.bounds.center), Vector3.zero);
            foreach (Vector3 v in filter.sharedMesh.vertices) bounds.Encapsulate(matrix.MultiplyPoint3x4(v));
            return bounds;
        }

        private static Vector3 RoofAnchor(Bounds roof, List<Vector3> triangles)
        {
            Vector3 point = roof.center;
            float height = float.NegativeInfinity;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) < .0000001f) continue;
                float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.z - c.z)) / denominator;
                float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.z - c.z)) / denominator;
                if (u < 0f || v < 0f || u + v > 1f) continue;
                height = Mathf.Max(height, a.y * u + b.y * v + c.y * (1f - u - v));
            }
            if (!float.IsNegativeInfinity(height)) return new Vector3(point.x, height, point.z);
            int highest = 0;
            for (int i = 3; i < triangles.Count; i += 3)
                if (triangles[i].y + triangles[i + 1].y + triangles[i + 2].y > triangles[highest].y + triangles[highest + 1].y + triangles[highest + 2].y) highest = i;
            return (triangles[highest] + triangles[highest + 1] + triangles[highest + 2]) / 3f;
        }

        private static void Set(Object target, params (string name, Object value)[] values)
        {
            var data = new SerializedObject(target);
            foreach (var value in values) data.FindProperty(value.name).objectReferenceValue = value.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
