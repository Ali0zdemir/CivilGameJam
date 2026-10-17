using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardFactoryFolderEditModeTests
{
    [Test]
    public void FactoryPresentationIsAuthoredWithoutInstructionsAndInheritedByTheSavedScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FactoryCard.prefab");
        Check(prefab.GetComponent<FactoryFolderPresentation>());
        var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FactoryFolderPresentation>(true))
            .Single(item => item.gameObject.activeInHierarchy);
        Check(view);
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(view), Is.SameAs(prefab.GetComponent<FactoryFolderPresentation>()));
    }

    private static void Check(FactoryFolderPresentation view)
    {
        Assert.That(view, Is.Not.Null);
        var data = new SerializedObject(view);
        foreach (var name in new[] { "factory", "station", "pocket", "storedBlueprint", "entryImage", "entryCanvas", "entryMouth", "workerHint", "personHint", "arrowHint" })
            Assert.That(data.FindProperty(name).objectReferenceValue, Is.Not.Null, name + " must be saved, not constructed in Play Mode.");
        var factory = view.GetComponent<FactoryProduction>();
        Assert.That(new SerializedObject(factory).FindProperty("folderPresentation").objectReferenceValue, Is.SameAs(view));
        var hint = (CanvasGroup)data.FindProperty("workerHint").objectReferenceValue;
        Assert.That(hint.GetComponentsInChildren<TMPro.TMP_Text>(true), Is.Empty, "The one-person prompt contains no words or numbers.");
        Assert.That(hint.blocksRaycasts, Is.False);
        Assert.That(hint.gameObject.activeSelf, Is.True, "Keep the guide visible for editing; Initialize hides it until a usable order needs a worker.");
        Assert.That(hint.alpha, Is.EqualTo(1f));
        var hintRect = (RectTransform)hint.transform;
        Assert.That(hintRect.anchoredPosition, Is.EqualTo(new Vector2(-21.3f, -9f)));
        Assert.That(hintRect.sizeDelta, Is.EqualTo(new Vector2(80f, 112f)));
        Assert.That(hintRect.localScale, Is.EqualTo(Vector3.one * 1.6f));
        Assert.That(view.StoredBlueprint.gameObject.activeSelf, Is.False);
        Assert.That(view.EntryImage.gameObject.activeSelf, Is.False);
        Assert.That(view.StoredBlueprint.GetComponentInParent<FactoryBlueprintSlot>(true), Is.Not.Null);
        var pocket = view.StoredBlueprint.transform.parent;
        var handler = view.StoredBlueprint.GetComponentInParent<FactoryBlueprintSlot>(true);
        Assert.That(handler.gameObject, Is.SameAs(pocket.gameObject), "The drag receiver must stay active when the stored blueprint image is hidden.");
        Assert.That(pocket.gameObject.activeSelf, Is.True);
        Assert.That(view.StoredBlueprint.GetComponent<FactoryBlueprintSlot>(), Is.Null);
        Assert.That(hint.transform.localScale.x, Is.EqualTo(hint.transform.localScale.y), "The enlarged prompt must keep its proportions.");
        Assert.That(pocket.GetComponent<RectMask2D>(), Is.Not.Null);
        Assert.That(pocket.GetSiblingIndex(), Is.LessThan(pocket.parent.Find("Klasör").GetSiblingIndex()));
        var mouth = (RectTransform)data.FindProperty("entryMouth").objectReferenceValue;
        var portrait = view.StoredBlueprint.rectTransform;
        Vector2 topAtLip = pocket.InverseTransformPoint(mouth.position + portrait.up * (portrait.rect.height * portrait.lossyScale.y * .5f));
        Assert.That(((RectTransform)pocket).rect.Contains(topAtLip), Is.True, "Switching behind the flap must not clip the blueprint's upper half.");
        var worker = view.GetComponentsInChildren<WorkerSlot>(true).First();
        Assert.That(data.FindProperty("entrySeconds").floatValue, Is.EqualTo(new SerializedObject(worker).FindProperty("entrySeconds").floatValue));
    }
}
