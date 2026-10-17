using CivilGameJam.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardProgressCompletionEditModeTests
{
    [TestCase("Assets/2DCardScene.unity", 9)] // Includes the three saved, inactive reference cards.
    [TestCase("Assets/Scenes/BuildingShowcase.unity", 3)]
    public void EverySceneBarHasItsSavedFeedbackAndControllerReference(string path, int expected)
    {
        var scene = EditorSceneManager.OpenScene(path);
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var effect in root.GetComponentsInChildren<ProgressBarFeedback>(true)) { Check(effect); count++; }
            foreach (var owner in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (owner == null) continue;
                var data = new SerializedObject(owner);
                var property = data.FindProperty("progressFeedback");
                if (property != null) Assert.That(property.objectReferenceValue, Is.Not.Null, owner.name);
            }
        }
        Assert.That(count, Is.EqualTo(expected));
    }

    [TestCase("ResearchCenterCard")]
    [TestCase("SchoolCard")]
    [TestCase("FactoryCard")]
    public void CardFeedbackIsEditableInThePrefabBeforePlay(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
        Check(prefab.transform.Find("ProgressBar").GetComponent<ProgressBarFeedback>());
    }

    private static void Check(ProgressBarFeedback effect)
    {
        Assert.That(effect, Is.Not.Null);
        var data = new SerializedObject(effect);
        var fill = data.FindProperty("fill").objectReferenceValue as Image;
        var group = data.FindProperty("visibility").objectReferenceValue as CanvasGroup;
        var glow = data.FindProperty("glow").objectReferenceValue as Outline;
        Assert.That(fill, Is.Not.Null);
        Assert.That(group, Is.SameAs(effect.GetComponent<CanvasGroup>()));
        Assert.That(group.blocksRaycasts, Is.False);
        Assert.That(glow, Is.SameAs(fill.GetComponent<Outline>()));
        Assert.That(glow.enabled, Is.False);
    }
}
