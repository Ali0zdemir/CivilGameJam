using System.Linq;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CardApartmentTsunamiEditModeTests
{
    [TestCase("House")]
    [TestCase("Apartment_02")]
    [TestCase("Apartment_03")]
    [TestCase("Apartment_04")]
    public void TsunamiMotionIsSavedOnTheExistingApartmentsWithoutAnimatingTheirGround(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Buildings/Prefabs/" + name + ".prefab");
        var saved = new SerializedObject(prefab.GetComponent<ApartmentTsunamiCollapse>());
        Assert.That(saved.FindProperty("apartment").objectReferenceValue, Is.Not.Null);
        Assert.That(saved.FindProperty("animationRoot").objectReferenceValue, Is.Not.Null);
        var clip = (AnimationClip)saved.FindProperty("collapseAnimation").objectReferenceValue;
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.legacy, Is.True, "Directly sampled flood clips must also animate in standalone builds without an Animator.");
        Assert.That(clip.length, Is.EqualTo(4.125f).Within(.001f));
        Assert.That(clip.isLooping, Is.False);
        var parts = saved.FindProperty("parts");
        Assert.That(parts.arraySize, Is.EqualTo(6));
        for (int i = 0; i < parts.arraySize; i++)
        {
            var part = parts.GetArrayElementAtIndex(i);
            Assert.That(part.FindPropertyRelative("body").objectReferenceValue, Is.Not.Null);
            var attachments = part.FindPropertyRelative("attachments");
            Assert.That(attachments.arraySize, Is.EqualTo(3));
            for (int j = 0; j < attachments.arraySize; j++) Assert.That(attachments.GetArrayElementAtIndex(j).objectReferenceValue, Is.Not.Null);
        }
        var bindings = AnimationUtility.GetCurveBindings(clip);
        Assert.That(bindings.Length, Is.EqualTo(60));
        Assert.That(bindings.Select(binding => binding.path).Distinct(), Is.EquivalentTo(Enumerable.Range(1, 6).Select(i => "Apartment" + i)));
        var instance = Object.Instantiate(prefab);
        try
        {
            var normal = instance.transform.Find("Apartment_Motion/Apartments_Normal");
            var bodies = Enumerable.Range(1, 6).Select(i => normal.Find("Apartment" + i)).ToArray();
            var positions = bodies.Select(body => body.localPosition).ToArray();
            var rotations = bodies.Select(body => body.localRotation).ToArray();
            var ground = instance.transform.Find("Apartment_Ground");
            Vector3 groundPosition = ground.position;
            var meshes = instance.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).ToArray();
            clip.SampleAnimation(normal.gameObject, 0f);
            for (int i = 0; i < 6; i++)
            {
                Assert.That(Vector3.Distance(bodies[i].localPosition, positions[i]), Is.LessThan(.0001f), "No initial teleport.");
                Assert.That(Quaternion.Angle(bodies[i].localRotation, rotations[i]), Is.LessThan(.01f), "No initial rotation jump.");
            }
            clip.SampleAnimation(normal.gameObject, clip.length);
            Assert.That(bodies.Count(body => Quaternion.Angle(body.localRotation, Quaternion.identity) > 45f), Is.GreaterThanOrEqualTo(4));
            Assert.That(ground.position, Is.EqualTo(groundPosition));
            Assert.That(instance.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh), Is.EqualTo(meshes));
        }
        finally { Object.DestroyImmediate(instance); }
    }
}
