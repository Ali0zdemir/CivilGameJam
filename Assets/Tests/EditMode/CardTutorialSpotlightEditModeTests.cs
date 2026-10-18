using System.Linq;
using System.Reflection;
using CivilGameJam;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class CardTutorialSpotlightEditModeTests
{
    [TestCase(0)]
    [TestCase(4)]
    [TestCase(5)]
    public void EditorResizeAndEnableCanUseHouseContoursWithAnOldMaterialCache(int legacySize)
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var tutorial = Object.FindFirstObjectByType<WorkersTutorial>(FindObjectsInactive.Include);
        var spotlight = tutorial.Spotlight;
        var canvas = tutorial.GetComponentInParent<Canvas>().rootCanvas;
        var scaler = canvas.GetComponent<CanvasScaler>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var framesField = typeof(TutorialSpotlight).GetField("frames", flags);
        var cacheField = typeof(TutorialSpotlight).GetField("contourInstances", flags);
        var baseField = typeof(TutorialSpotlight).GetField("contourMaterial", flags);
        var originalFrames = (Image[])framesField.GetValue(spotlight);
        var originalCache = cacheField.GetValue(spotlight);
        var originalBase = (Material)baseField.GetValue(spotlight);
        var originalDimmer = spotlight.material;
        var contour = new Material(originalBase) { hideFlags = HideFlags.HideAndDontSave };
        var dimmer = new Material(originalDimmer) { hideFlags = HideFlags.HideAndDontSave };
        var frames = new Image[9]; System.Array.Copy(originalFrames, frames, originalFrames.Length);
        try
        {
            canvas.transform.localScale = Vector3.one;
            baseField.SetValue(spotlight, contour); spotlight.material = dimmer;
            cacheField.SetValue(spotlight, new Material[legacySize]);
            canvas.gameObject.SetActive(true);
            tutorial.gameObject.SetActive(true); tutorial.transform.Find("Overlay").gameObject.SetActive(true);
            spotlight.gameObject.SetActive(true);
            for (int i = 5; i < frames.Length; i++)
            {
                frames[i] = new GameObject("Editor house contour " + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                frames[i].transform.SetParent(spotlight.transform.parent, false);
            }
            framesField.SetValue(spotlight, frames);
            var houses = Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(link => link.Building != null && link.House != null)
                .OrderBy(link => link.House.GetComponent<DraggableCard>().InitialGridCell)
                .Select(link => link.House).ToArray();
            Assert.That(houses.Length, Is.EqualTo(4));
            // Inspector previews can leave the information panel open; Play Mode's
            // CardHover.Awake hides it before a house silhouette is selected.
            foreach (var house in houses)
            {
                var hover = house.GetComponent<CardHover>();
                if (hover != null && hover.Details != null) hover.Details.gameObject.SetActive(false);
            }
            spotlight.SetHouseInstallationFocus(houses, null);
            scaler.enabled = false; scaler.enabled = true;
            spotlight.enabled = false; spotlight.enabled = true;
            Canvas.ForceUpdateCanvases(); spotlight.RefreshLayout();
            Assert.That(frames.Skip(5).All(frame => frame.gameObject.activeInHierarchy && frame.sprite != null), Is.True,
                string.Join("; ", houses.Select(house => house.name + ": " + TutorialSpotlight.HouseArtwork(house)?.sprite?.name)));
            Assert.That(frames.Skip(5).All(frame => frame.material == contour), Is.True,
                "Edit Mode uses the saved contour material when a retained cache slot is missing.");
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            framesField.SetValue(spotlight, originalFrames); cacheField.SetValue(spotlight, originalCache);
            baseField.SetValue(spotlight, originalBase); spotlight.material = originalDimmer;
            for (int i = 5; i < frames.Length; i++) if (frames[i] != null) Object.DestroyImmediate(frames[i].gameObject);
            Object.DestroyImmediate(contour); Object.DestroyImmediate(dimmer);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
