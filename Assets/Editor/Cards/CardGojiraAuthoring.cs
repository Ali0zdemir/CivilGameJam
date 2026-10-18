using System;
using System.IO;
using System.Linq;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    /// <summary>Preserves the imported Gojira mesh and adds saved controls for the existing disaster animations.</summary>
    public static class CardGojiraAuthoring
    {
        public const string SourcePath = "Assets/emreAnimation/Gojira.fbx";
        public const string MeshPath = "Assets/Art/Monsters/Gojira/GojiraRigged.asset";

        [MenuItem("Tools/Disasters/Use Emre Gojira Monster")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save Gojira in Edit Mode.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new FileNotFoundException("Gojira must be imported first.", SourcePath);
            var sourceFilter = source.GetComponentInChildren<MeshFilter>();
            if (sourceFilter == null) throw new InvalidOperationException("Gojira has no source mesh.");
            var sourceMesh = sourceFilter.sharedMesh;
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var monster = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SeaMonsterEmergence>(true)).Single();
            var oldModel = monster.Model;
            var position = oldModel.localPosition;
            var scale = oldModel.localScale;
            var rotation = oldModel.localRotation;
            var model = new GameObject("Gojira_Model").transform;
            model.SetParent(oldModel.parent, false);
            model.SetLocalPositionAndRotation(position, rotation);
            model.localScale = scale;

            const float height = 3.49f;
            var sourceBounds = sourceMesh.bounds;
            var factor = height / sourceBounds.size.y;
            var offset = new Vector3(sourceBounds.center.x, sourceBounds.min.y, -.28f);
            var conversion = Quaternion.Euler(0f, 180f, 0f);
            var mesh = Object.Instantiate(sourceMesh);
            mesh.name = "Gojira_Rigged";
            mesh.vertices = sourceMesh.vertices.Select(point => conversion * (point - offset) * factor).ToArray();
            mesh.normals = sourceMesh.normals.Select(normal => conversion * normal).ToArray();
            mesh.tangents = sourceMesh.tangents.Select(tangent => {
                var direction = conversion * new Vector3(tangent.x, tangent.y, tangent.z);
                return new Vector4(direction.x, direction.y, direction.z, tangent.w);
            }).ToArray();
            mesh.RecalculateBounds();

            Transform Control(string name, Vector3 point)
            {
                var control = new GameObject(name).transform;
                control.SetParent(model, false); control.localPosition = point;
                return control;
            }
            var body = Control("Body", new Vector3(0f, 1.35f, -.35f));
            var head = Control("Head", new Vector3(0f, 3.1f, .55f));
            var leftLeg = Control("Left_Leg", new Vector3(-.56f, .72f, 0f));
            var leftFoot = Control("Left_Foot", new Vector3(-.56f, .10f, .28f));
            var leftArm = Control("Left_Arm", new Vector3(-.72f, 1.6f, .20f));
            var rightLeg = Control("Right_Leg", new Vector3(.56f, .72f, 0f));
            var rightFoot = Control("Right_Foot", new Vector3(.56f, .10f, .28f));
            var rightArm = Control("Right_Arm", new Vector3(.72f, 1.6f, .20f));
            var neck = Control("Neck_Pivot", new Vector3(0f, 1.95f, .2f));
            var headFront = mesh.vertices.Where(point => point.y > height * .85f).Max(point => point.z);
            var muzzle = Control("Muzzle", new Vector3(0f, height * .9f, headFront + .035f));
            var mouth = new GameObject("Flame_Origin").transform;
            mouth.SetParent(muzzle, false);
            var bones = new[] { body, head, leftLeg, leftFoot, leftArm, rightLeg, rightFoot, rightArm };
            mesh.bindposes = bones.Select(bone => bone.worldToLocalMatrix * model.localToWorldMatrix).ToArray();
            mesh.boneWeights = mesh.vertices.Select(Weights).ToArray();
            EnsureFolder("Assets/Art/Monsters"); EnsureFolder("Assets/Art/Monsters/Gojira");
            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (savedMesh == null) { AssetDatabase.CreateAsset(mesh, MeshPath); savedMesh = mesh; }
            else { EditorUtility.CopySerialized(mesh, savedMesh); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(savedMesh); }
            var geometry = new GameObject("Gojira_Geometry");
            geometry.transform.SetParent(model, false);
            var skin = geometry.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = savedMesh;
            skin.sharedMaterials = sourceFilter.GetComponent<MeshRenderer>().sharedMaterials;
            skin.bones = bones; skin.rootBone = model; skin.updateWhenOffscreen = true;
            var bounds = savedMesh.bounds; bounds.Expand(1.2f); skin.localBounds = bounds;

            SetReferences(monster, ("model", model));
            SetReferences(monster.GetComponent<SeaMonsterWalk>(), ("leftLeg", leftLeg), ("leftFoot", leftFoot), ("leftArm", leftArm),
                ("rightLeg", rightLeg), ("rightFoot", rightFoot), ("rightArm", rightArm));
            var breath = monster.GetComponent<SeaMonsterFireBreath>();
            SetReferences(breath, ("model", model), ("mouth", mouth), ("neckPivot", neck));
            var data = new SerializedObject(breath);
            SetArray(data, "upperBody", body, head, muzzle, leftArm, rightArm);
            SetArray(data, "headParts", head, muzzle);
            data.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(oldModel.gameObject);
            model.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the Gojira monster.");
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the original Gojira geometry and materials with planted walking, breathing and mouth controls.");
        }

        private static float Blend(float minimum, float maximum, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum, maximum, value));
        private static BoneWeight Weights(Vector3 point)
        {
            float head = Blend(1.85f, 2.4f, point.y) * Blend(-1f, -.45f, point.z);
            float leg = Blend(.25f, .5f, Mathf.Abs(point.x)) * (1f - Blend(1.05f, 1.45f, point.y)) * Blend(-1f, -.5f, point.z);
            float arm = Blend(.45f, .8f, Mathf.Abs(point.x)) * Blend(1.05f, 1.35f, point.y) *
                (1f - Blend(1.9f, 2.35f, point.y)) * Blend(-.9f, -.5f, point.z);
            if (leg > arm)
            {
                float foot = 1f - Blend(.3f, .65f, point.y);
                int index = point.x < 0f ? 2 : 5;
                return new BoneWeight { boneIndex0 = 0, weight0 = 1f - leg, boneIndex1 = index, weight1 = leg * (1f - foot), boneIndex2 = index + 1, weight2 = leg * foot };
            }
            return new BoneWeight { boneIndex0 = 0, weight0 = (1f - head) * (1f - arm), boneIndex1 = 1, weight1 = head * (1f - arm),
                boneIndex2 = point.x < 0f ? 4 : 7, weight2 = arm };
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
        private static void SetReferences(Object target, params (string field, Object value)[] values)
        {
            var data = new SerializedObject(target);
            foreach (var value in values) data.FindProperty(value.field).objectReferenceValue = value.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetArray(SerializedObject data, string field, params Object[] values)
        {
            var array = data.FindProperty(field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
