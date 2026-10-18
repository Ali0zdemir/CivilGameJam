using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardAvailabilityGuideEditModeTests
{
    [Test]
    public void PersonBadgesAndBlueprintMasksAreSavedInTheirArtworkWithoutChangingCardGeometry()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var game = Object.FindFirstObjectByType<CardGameController>();
        foreach (var person in game.GetComponentsInChildren<DraggableCard>(true).Where(card => card.IsPerson)) CheckPerson(person);
        foreach (var path in new[] { "Assets/Prefabs/PeopleCard.prefab", "Assets/Prefabs/EducatedPeopleCard.prefab" })
            CheckPerson(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<DraggableCard>());

        var options = new SerializedObject(game).FindProperty("blueprints");
        Assert.That(options.arraySize, Is.EqualTo(3));
        for (int i = 0; i < options.arraySize; i++)
        {
            var entry = options.GetArrayElementAtIndex(i);
            var button = (Button)entry.FindPropertyRelative("button").objectReferenceValue;
            var overlay = (Image)entry.FindPropertyRelative("unavailableOverlay").objectReferenceValue;
            var art = button.GetComponentInChildren<UpgradeIcon>(true);
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.transform.parent, Is.SameAs(art.transform));
            Assert.That(overlay.sprite, Is.SameAs(art.Artwork));
            Assert.That(overlay.raycastTarget, Is.False);
            Assert.That(overlay.preserveAspect, Is.True);
            Assert.That(overlay.color, Is.EqualTo(new Color(0f, 0f, 0f, .94f)));
            Assert.That(overlay.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(overlay.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(overlay.rectTransform.sizeDelta, Is.EqualTo(Vector2.zero));
            Assert.That(overlay.gameObject.activeSelf, Is.False, "Availability is decided when the saved selection screen opens.");
        }
    }

    private static void CheckPerson(DraggableCard person)
    {
        var badge = (Image)new SerializedObject(person).FindProperty("unavailableOverlay").objectReferenceValue;
        Assert.That(badge, Is.Not.Null, person.name);
        Assert.That(badge.sprite.name, Is.EqualTo("Unavailable UI"));
        Assert.That(badge.transform.parent.name, Is.EqualTo("Artwork"));
        Assert.That(badge.color, Is.EqualTo(Color.white));
        Assert.That(badge.raycastTarget, Is.False);
        Assert.That(badge.preserveAspect, Is.True);
        Assert.That(badge.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(badge.rectTransform.rect.width, Is.EqualTo(badge.rectTransform.rect.height));
        Assert.That(badge.rectTransform.rect.width, Is.LessThan(((RectTransform)badge.transform.parent).rect.width));
        Assert.That(badge.GetComponent<CanvasGroup>().ignoreParentGroups, Is.True);
        Assert.That(badge.gameObject.activeSelf, Is.False, "Fresh people have no unavailable marker.");
    }
}
