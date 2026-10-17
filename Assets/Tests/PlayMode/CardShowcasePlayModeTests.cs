using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator StandaloneShowcaseRetainsItsAuthoredDisasterButtons()
    {
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("BuildingShowcase");
        yield return null;
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        Assert.That(navigation, Is.Not.Null);
        Assert.That(navigation.InputViewport, Is.Null, "The standalone scene keeps its original full-screen camera.");
        Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        foreach (var controller in new MonoBehaviour[] {
            Object.FindFirstObjectByType<EarthquakeTestController>(),
            Object.FindFirstObjectByType<FireTestController>(),
            Object.FindFirstObjectByType<TsunamiTestController>() })
        {
            var button = (UnityEngine.UI.Button)controller.GetType().GetField("testButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            Assert.That(button.onClick.GetPersistentEventCount(), Is.GreaterThan(0));
            button.onClick.Invoke();
            Assert.That((bool)controller.GetType().GetProperty("IsRunning").GetValue(controller), Is.True, controller.name);
        }
        Object.FindFirstObjectByType<EarthquakeTestController>().StopEarthquake();
        Object.FindFirstObjectByType<FireTestController>().StopFire();
        Object.FindFirstObjectByType<TsunamiTestController>().StopTsunami();
        yield return null;
    }

    private static void InvokeCity(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);

    [UnityTest]
    public IEnumerator ShowcaseCameraPansAndZoomsOnlyInsideTheUncoveredCity()
    {
        var city = Object.FindFirstObjectByType<CityViewport>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        navigation.enabled = false;
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/showcase-navigation.png"));
        yield return CaptureCanvas(path, afterRender: uiCamera =>
        {
            Vector2 cityPoint = RectTransformUtility.WorldToScreenPoint(uiCamera,
                city.ViewRect.TransformPoint(new Vector3(city.ViewRect.rect.center.x, city.ViewRect.rect.yMax - 260f)));
            float initialZoom = city.WorldCamera.orthographicSize;
            InvokeCity(navigation, "ReadPointer", cityPoint, 0f, true, true);
            InvokeCity(navigation, "ReadPointer", cityPoint + Vector2.right * 45f, 1f, false, true);
            InvokeCity(navigation, "AdvanceNavigation", 1f);
            Assert.That(navigation.PanOffset.x, Is.LessThan(-0.01f));
            Assert.That(city.WorldCamera.orthographicSize, Is.LessThan(initialZoom));
            Vector2 peoplePoint = RectTransformUtility.WorldToScreenPoint(uiCamera, game.Sidebar.PeopleArea.position);
            Vector2 expectedPan = navigation.PanOffset;
            float expectedZoom = city.WorldCamera.orthographicSize;
            InvokeCity(navigation, "ReadPointer", peoplePoint, 10f, true, true);
            InvokeCity(navigation, "ReadPointer", peoplePoint + Vector2.right * 45f, 10f, false, true);
            InvokeCity(navigation, "AdvanceNavigation", 1f);
            Assert.That(Vector2.Distance(navigation.PanOffset, expectedPan), Is.LessThan(0.001f));
            Assert.That(city.WorldCamera.orthographicSize, Is.EqualTo(expectedZoom).Within(0.001f));
            navigation.ResetView();
        });
    }

    [UnityTest]
    public IEnumerator DraggingCardsAndChoosingBlueprintsLockCityInput()
    {
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var guard = Object.FindFirstObjectByType<CardCityNavigation>();
        var person = People()[0];
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left, pointerId = -1,
            position = RectTransformUtility.WorldToScreenPoint(null, person.Rect.position)
        };
        person.OnBeginDrag(pointer);
        InvokeCity(guard, "Update");
        Assert.That(navigation.InputBlocked, Is.True);
        person.OnEndDrag(pointer);
        yield return new WaitForSecondsRealtime(0.55f);
        InvokeCity(guard, "Update");
        Assert.That(navigation.InputBlocked, Is.False);
        research.TryAssign(person); research.AdvanceWork(25.01f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        InvokeCity(guard, "Update");
        Assert.That(navigation.InputBlocked, Is.True);
        game.SelectBlueprint(0);
        InvokeCity(guard, "Update");
        Assert.That(navigation.InputBlocked, Is.False);
    }

    [UnityTest]
    public IEnumerator ShowcaseEarthquakeShakesOnlyTheCityAndRestoresTheCamera()
    {
        var city = Object.FindFirstObjectByType<CityViewport>();
        var earthquake = Object.FindFirstObjectByType<EarthquakeTestController>();
        var camera = city.WorldCamera.transform;
        var position = camera.localPosition; var rotation = camera.localRotation;
        var cardPositions = game.Cards.ToDictionary(card => card, card => card.Rect.position);
        earthquake.StartEarthquake();
        Assert.That(earthquake.IsRunning, Is.True);
        InvokeCity(earthquake, "AdvanceShake", 2f);
        float movement = Vector3.Distance(camera.localPosition, position) + Quaternion.Angle(camera.localRotation, rotation);
        Assert.That(movement, Is.GreaterThan(0.00001f));
        var shakingPose = camera.localPosition;
        city.RefreshViewport();
        Assert.That(camera.localPosition, Is.EqualTo(shakingPose), "Viewport resizing must not erase earthquake shake.");
        foreach (var pair in cardPositions) Assert.That(pair.Key.Rect.position, Is.EqualTo(pair.Value));
        earthquake.StopEarthquake();
        Assert.That(camera.localPosition, Is.EqualTo(position));
        Assert.That(camera.localRotation, Is.EqualTo(rotation));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ImportedUpgradesFireAndTsunamiWorkAlongsideCardProduction()
    {
        var world = GameObject.Find("World3D");
        var rails = world.GetComponentsInChildren<BuildingRailUpgrade>();
        var columns = world.GetComponentsInChildren<HouseColumnUpgrade>();
        var sprinklers = world.GetComponentsInChildren<HouseSprinklerUpgrade>();
        rails[0].SetInstalled(true); columns[1].SetInstalled(true); sprinklers[2].SetInstalled(true);
        Assert.That(rails[0].IsInstalled && columns[1].IsInstalled && sprinklers[2].IsInstalled, Is.True);
        var fire = world.GetComponentInChildren<FireTestController>();
        fire.StartFire();
        Assert.That(fire.IsRunning, Is.True);
        InvokeCity(fire, "AdvanceFire", 1f);
        fire.StopFire();
        var tsunami = world.GetComponentInChildren<TsunamiTestController>();
        tsunami.StartTsunami();
        Assert.That(tsunami.IsRunning, Is.True);
        InvokeCity(tsunami, "AdvanceTsunami", 1f);
        Assert.That(tsunami.NormalizedProgress, Is.GreaterThan(0f));
        tsunami.StopTsunami();
        rails[0].ResetUpgrade(); columns[1].ResetUpgrade(); sprinklers[2].ResetUpgrade();
        var production = game.Cards.Single(card => card.Kind == CardKind.Factory).GetComponent<FactoryProduction>();
        production.enabled = false;
        var plan = ResearchPlan(0);
        Assert.That(production.GetComponent<BuildingStation>().TryAssign(People().First(person => person.AssignedTo == null)), Is.True);
        Assert.That(production.TryInsertBlueprint(plan), Is.True);
        production.AdvanceWork(production.ProductionSeconds + 0.01f);
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.EqualTo(2));
        Assert.That(game.Vault.AvailableCount(UpgradeKind.RailSystem), Is.EqualTo(1));
        yield return null;
    }
}
