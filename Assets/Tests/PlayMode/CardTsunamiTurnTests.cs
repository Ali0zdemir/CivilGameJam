using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class CardAbilityPointsPlayModeTests
{
    [UnityTest]
    public IEnumerator BossTsunamiReportsOneSecondAfterTheLastHouseFalls()
    {
        yield return ReachTsunami();
        var water = Field<TsunamiTestController>(round, "tsunami");
        var collapses = houses.Select(house => house.Building.GetComponent<ApartmentTsunamiCollapse>()).ToArray();
        round.Ready();
        var clock = 0f;
        var lastFall = -1f;
        var state = string.Empty;
        for (var frame = 0; frame < 3000 && !sequence.Results.IsVisible; frame++)
        {
            Step(.05f); clock += .05f;
            if (lastFall < 0f && collapses.All(collapse => collapse.HasCollapsed && !collapse.IsPlaying))
            {
                lastFall = clock;
                state = "cityEnd=" + water.HasReachedCityEnd + ", quakeRunning=" + earthquake.IsRunning +
                    ", waterRunning=" + water.IsRunning + ", waterProgress=" + water.WaterRecessionProgress +
                    ", stage=" + sequence.Stage;
                Assert.That(sequence.Results.IsVisible, Is.False);
            }
        }
        var directory = Path.GetFullPath("Library/CardGameTestResults/BossTsunamiTiming");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "native-timeline.txt"),
            "lastFall=" + lastFall + ", report=" + clock + ", difference=" + (clock - lastFall) + "; " + state);
        Assert.That(lastFall, Is.GreaterThan(0f));
        Assert.That(sequence.Results.IsVisible, Is.True);
        Assert.That(clock - lastFall, Is.EqualTo(1f).Within(.06f), "Start the result delay at the final authored collapse, without an extra dust/water/walk wait.");
        AssertVisibleTsunamiRuins();
        Assert.That(bank.CurrentPoints, Is.EqualTo(24));
    }

    [UnityTest]
    public IEnumerator ReadyTsunamiKeepsRuinsVisibleAndReportsAfterOneSecondOfSmoothRecession()
    {
        yield return ReachTsunami();
        Set(navigation, "targetPan", new Vector2(.7f, -.3f));
        Set(navigation, "targetZoom", 5.6f);
        Invoke(navigation, "AdvanceNavigation", 2f);
        var homePan = navigation.PanOffset;
        var homeZoom = Field<float>(navigation, "currentZoom");
        var water = Field<TsunamiTestController>(round, "tsunami");
        var wave = Field<AuthoredTsunamiWave>(water, "authoredWave");
        var fullScale = wave.transform.localScale;
        Assert.That(sequence.TsunamiAftermathSeconds, Is.EqualTo(1f));
        Set(water, "floodRunoffTime", 25f);
        Set(round, "bossDepartureSeconds", 50f);
        round.Ready();
        Until(() => Field<bool>(sequence, "tsunamiAftermathStarted"));
        Assert.That(water.HasFinishedHouseCollapses, Is.True);
        Assert.That(round.RoundNumber, Is.EqualTo(2));
        Assert.That(sequence.Results.IsVisible, Is.False);
        var cityPan = navigation.PanOffset;
        var cityZoom = Field<float>(navigation, "currentZoom");
        Assert.That(Field<Object>(navigation, "eventFocusOwner"), Is.SameAs(sequence));
        Step(.7f);
        AssertVisibleTsunamiRuins();
        Assert.That(water.WaterRecessionProgress, Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(navigation.PanOffset, Is.EqualTo(cityPan));
        Assert.That(Field<float>(navigation, "currentZoom"), Is.EqualTo(cityZoom));
        Assert.That(Field<bool>(navigation, "eventReturning"), Is.False);
        Assert.That(bank.ActionsLocked && navigation.InputBlocked, Is.True);
        Assert.That(round.RoundNumber, Is.EqualTo(2));
        sequence.StopSequence(); round.AdvanceRound(0f);
        Invoke(navigation, "AdvanceNavigation", 2f);
        Assert.That(navigation.PanOffset, Is.EqualTo(homePan));
        Assert.That(Field<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
        Assert.That(bank.CurrentPoints, Is.EqualTo(18), "Cancelling the watch cannot pay a retry bonus.");
        Assert.That(round.RoundNumber, Is.EqualTo(2));
        Assert.That(wave.transform.localScale, Is.EqualTo(fullScale));
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False);
        Assert.That(round.CanReady, Is.True);

        round.Ready();
        Until(() => Field<bool>(sequence, "tsunamiAftermathStarted"));
        cityPan = navigation.PanOffset;
        cityZoom = Field<float>(navigation, "currentZoom");
        var untilLastFrame = sequence.TsunamiAftermathSeconds - .05f;
        while (Field<float>(sequence, "tsunamiAftermathElapsed") < untilLastFrame - .00001f)
        {
            Step(Mathf.Min(.05f, untilLastFrame - Field<float>(sequence, "tsunamiAftermathElapsed")));
            Assert.That(navigation.PanOffset, Is.EqualTo(cityPan), "Hold the city for the brief aftermath.");
            Assert.That(Field<float>(navigation, "currentZoom"), Is.EqualTo(cityZoom));
            Assert.That(Field<bool>(navigation, "eventReturning"), Is.False);
            Assert.That(Field<Object>(navigation, "eventFocusOwner"), Is.SameAs(sequence));
            AssertVisibleTsunamiRuins();
        }
        Assert.That(round.RoundNumber, Is.EqualTo(2), "Finish the one-second recession after the last house collapse.");
        Assert.That(sequence.Results.IsVisible, Is.False);
        Assert.That(bank.CurrentPoints, Is.EqualTo(18));
        Assert.That(sequence.IsRunning && navigation.InputBlocked, Is.True);
        CaptureWorld("tsunami-one-second-visible-ruins.png");
        Step(.06f);
        Assert.That(Field<float>(sequence, "tsunamiAftermathElapsed"), Is.EqualTo(1.01f).Within(.001f));
        Assert.That(sequence.PresentationFinished, Is.True, "The remaining boss route and original 25-second runoff cannot extend the report delay.");
        Assert.That(water.IsRunning, Is.False);
        Assert.That(water.WaterRecessionProgress, Is.GreaterThan(.999f), "Finish the gradual recession before clearing the water.");
        Assert.That(round.RoundNumber, Is.EqualTo(3));
        Assert.That(sequence.Results.IsVisible, Is.True);
        Assert.That(sequence.Results.DestroyedHouseCount, Is.EqualTo(4));
        Assert.That(bank.CurrentPoints, Is.EqualTo(24));
        Assert.That(houses.All(house => house.House.IsDestroyed), Is.True, "The watch must keep damage behind the eventual report.");
        AssertVisibleTsunamiRuins();
        sequence.Results.Retry();
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False);
        Assert.That(bank.CurrentPoints, Is.EqualTo(24));
    }

    private void AssertVisibleTsunamiRuins()
    {
        var ruins = houses.Select(house => house.Building.GetComponent<ApartmentTsunamiCollapse>())
            .Where(collapse => collapse.HasCollapsed).ToArray();
        Assert.That(ruins, Is.Not.Empty);
        foreach (var collapse in ruins)
        {
            Assert.That(collapse.IsPlaying, Is.False);
            var mesh = Field<Transform>(collapse, "animationRoot").GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(mesh.Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy), Is.True);
            Assert.That(mesh.Any(renderer => renderer.forceRenderingOff), Is.False,
                "Tsunami ruins must remain visible before and behind the result panel.");
        }
    }

    private IEnumerator ReachTsunami()
    {
        bank.AddPoints(4);
        PrepareSeismic(2);
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready();
        Until(() => round.RoundNumber == 2);
        // Stop at the real earthquake-to-water handoff to isolate planning/pricing tests.
        // The uninterrupted cinematic and its water loss have their own integration test.
        sequence.StopSequence(); round.AdvanceRound(0f); Invoke(navigation, "AdvanceNavigation", 2f);
        Assert.That(round.SequenceNumber, Is.EqualTo(2));
        Assert.That(bank.CurrentPoints, Is.EqualTo(18));
        AssertFreshPeople(System.Array.Empty<DraggableCard>());
    }

    [UnityTest]
    public IEnumerator TsunamiEducatedPricesUseTheExistingBadgesAndRefundTheActualOperationCharge()
    {
        yield return ReachTsunami();
        Assert.That(school.AbilityPointCost(CardKind.Person), Is.EqualTo(2));
        Assert.That(research.AbilityPointCost(CardKind.EducatedPerson), Is.EqualTo(3));
        Assert.That(factoryStation.AbilityPointCost(CardKind.EducatedPerson), Is.EqualTo(5));
        Assert.That(research.AbilityPointCost(CardKind.Person), Is.EqualTo(6));
        Assert.That(factoryStation.AbilityPointCost(CardKind.Person), Is.EqualTo(7));
        Assert.That(research.transform.Find("FolderArt/APEducated").GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("3 AP"));
        Assert.That(factoryStation.transform.Find("FolderArt/APEducated").GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("5 AP"));

        var graduate = Educate();
        Assert.That(bank.CurrentPoints, Is.EqualTo(16));
        Assert.That(research.TryAssign(graduate), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(13));
        research.AdvanceWork(game.EducatedResearchSeconds * .2f);
        Set(research, "tsunamiEducatedAbilityPointCost", 7);
        research.RefreshAbilityCostLabels();
        research.ReleaseWorker(0, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(16), "A price edit must not increase an already reserved refund.");
        Assert.That(research.ResearchProgress, Is.Zero);
        Set(research, "tsunamiEducatedAbilityPointCost", 3);
        research.RefreshAbilityCostLabels();
        var plan = Research(graduate, UpgradeKind.Breakwater);
        Assert.That(bank.CurrentPoints, Is.EqualTo(13));
        var maker = Educate();
        Assert.That(bank.CurrentPoints, Is.EqualTo(11));
        Assert.That(factoryStation.TryAssign(maker), Is.True);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(6));
        factory.AdvanceWork(.2f);
        factoryStation.ReleaseWorker(0, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(11));
        Assert.That(factory.Progress, Is.Zero);
        Assert.That(factoryStation.TryAssign(maker), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(6));
        factory.AdvanceWork(100f);
        Assert.That(factory.Progress, Is.Zero, "One educated worker still waits for a second person.");
        bank.AddPoints(1);
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        Assert.That(bank.CurrentPoints, Is.Zero);
        factory.AdvanceWork(100f);
        Assert.That(maker.UsedThisTurn, Is.True);
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.Breakwater), Is.EqualTo(2));
    }

    private void PrepareElevators(int batches)
    {
        var plan = Research(Person(), UpgradeKind.Breakwater);
        for (int i = 0; i < batches; i++)
        {
            // Multi-order fixtures span worker turns while preserving their AP accounting.
            if (game.Cards.Count(card => card.Kind == CardKind.Person && card.CanStartWork && !card.IsStored) < 2)
                game.ResetTurnPeople();
            Assert.That(factoryStation.TryAssign(Person()), Is.True);
            Produce(plan);
        }
        var elevators = game.Cards.Where(card => card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.Breakwater).ToArray();
        for (int i = 0; i < elevators.Length; i++) Assert.That(houses[i].House.TryInstall(elevators[i]), Is.True);
    }

    [UnityTest]
    public IEnumerator FourWaterProtectedHousesPayTwentyFourAPAndUnlockFireKeepingBothSystems()
    {
        yield return ReachTsunami();
        // Carried AP cover one researcher and two complete factory crews.
        bank.AddPoints(16);
        PrepareElevators(2);
        Assert.That(bank.CurrentPoints, Is.Zero);
        var rails = houses.Select(house => house.House.GetInstalled(UpgradeKind.RailSystem)).ToArray();
        var elevators = houses.Select(house => house.House.GetInstalled(UpgradeKind.Breakwater)).ToArray();
        var previousPeople = game.Cards.Where(card => card.IsPerson).ToArray();
        yield return new WaitForSecondsRealtime(1.6f);
        foreach (var house in houses)
            Assert.That(house.Building.GetComponent<ApartmentUpgradeModel>().IsElevatorProtected, Is.True);
        round.Ready();
        Assert.That(sequence.ActiveSequence, Is.EqualTo(2));
        Until(() => round.RoundNumber == 3);
        Assert.That(round.SequenceNumber, Is.EqualTo(3));
        Assert.That(round.LastRoundSurvived, Is.True);
        Assert.That(round.LastProtectedHouseCount, Is.EqualTo(4));
        Assert.That(round.LastSurvivalReward, Is.EqualTo(24));
        Assert.That(Field<float>(sequence, "tsunamiAftermathElapsed"), Is.GreaterThanOrEqualTo(sequence.TsunamiAftermathSeconds - .0001f));
        Assert.That(round.LastRoundGrant, Is.EqualTo(6));
        Assert.That(bank.CurrentPoints, Is.EqualTo(30), "0 unspent + 24 water survival + 6 turn bonus.");
        AssertFreshPeople(previousPeople);
        for (int i = 0; i < houses.Length; i++)
        {
            Assert.That(houses[i].House.GetInstalled(UpgradeKind.RailSystem), Is.SameAs(rails[i]));
            Assert.That(houses[i].House.GetInstalled(UpgradeKind.Breakwater), Is.SameAs(elevators[i]));
            var model = houses[i].Building.GetComponent<ApartmentUpgradeModel>();
            Assert.That(model.HasSeismic && model.IsElevatorProtected, Is.True);
        }
        Assert.That(game.CanUseBlueprint(UpgradeKind.RailSystem) || game.CanUseBlueprint(UpgradeKind.Breakwater), Is.False);
        Assert.That(game.CanUseBlueprint(UpgradeKind.Fire), Is.True);
        Assert.That(sequence.SelectedSequence, Is.EqualTo(3));
        Assert.That(round.CanReady, Is.False);
        sequence.Results.Retry();
        round.Ready();
        Assert.That(sequence.IsRunning && bank.ActionsLocked, Is.True);
        Assert.That(sequence.ActiveSequence, Is.EqualTo(3));
        sequence.StopSequence(); round.AdvanceRound(0f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(30));
    }

    [UnityTest]
    public IEnumerator TwoElevatedHousesPayTwelveAPAndRetryWaterWithAllSeismicProtectionIntact()
    {
        yield return ReachTsunami();
        bank.AddPoints(7); // Carried AP for the second factory worker.
        PrepareElevators(1);
        Assert.That(bank.CurrentPoints, Is.EqualTo(5));
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready();
        Until(() => round.RoundNumber == 3);
        Assert.That(round.SequenceNumber, Is.EqualTo(2));
        Assert.That(round.LastRoundSurvived, Is.False);
        Assert.That(round.LastProtectedHouseCount, Is.EqualTo(2));
        Assert.That(round.LastSurvivalReward, Is.EqualTo(12));
        Assert.That(round.LastRoundGrant, Is.EqualTo(6));
        Assert.That(bank.CurrentPoints, Is.EqualTo(23));
        Assert.That(houses.All(house => house.House.GetInstalled(UpgradeKind.RailSystem) != null &&
            house.Building.GetComponent<ApartmentUpgradeModel>().HasSeismic), Is.True);
        Assert.That(houses.Count(house => house.House.GetInstalled(UpgradeKind.Breakwater) != null), Is.EqualTo(2));
        Assert.That(houses.Count(house => house.House.IsDestroyed), Is.EqualTo(2));
        sequence.Results.Retry();
        Assert.That(houses.All(house => !house.House.IsDestroyed), Is.True);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.Breakwater) && game.CanUseBlueprint(UpgradeKind.Breakwater), Is.True);
        AssertFreshPeople(System.Array.Empty<DraggableCard>());
    }

    [UnityTest]
    public IEnumerator ReadyBossHidesAfterTenSecondsOfDepartureEvenWhenTheDestinationIsFarAway()
    {
        bank.AddPoints(4); PrepareSeismic(2);
        var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        walk.AfterEventPoint.position = walk.WalkPoint.position + Vector3.forward * 200f;
        // Keep water from ending the departure timer test at the first building contact.
        foreach (var house in houses) house.FloodProtection.SetInstalled(true);
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready(); Until(() => sequence.Stage == MonsterSequenceStage.WalkingToSecondPoint);
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        Assert.That(walk.AfterEventWalkDuration, Is.GreaterThan(100f));
        Step(9.9f);
        Assert.That(monster.Model.gameObject.activeSelf && walk.IsWalkingAfterEvent, Is.True);
        Step(.11f);
        Assert.That(monster.Model.gameObject.activeSelf || walk.IsWalkingAfterEvent, Is.False);
        Assert.That(Field<float>(sequence, "departureElapsed"), Is.InRange(10f, 10.05f));
        Until(() => round.RoundNumber == 3);
        Assert.That(round.SequenceNumber, Is.EqualTo(2));
        Assert.That(navigation.InputBlocked, Is.False);
    }

    private static PointerEventData DragPointer(DraggableCard card) => new PointerEventData(EventSystem.current)
    {
        position = RectTransformUtility.WorldToScreenPoint(null, card.Rect.position),
        pointerId = -1,
        button = PointerEventData.InputButton.Left
    };

    [UnityTest]
    public IEnumerator ADraggedPersonLightsAllValidBuildingsAndGuidesUpdateForCapacityAndAP()
    {
        var dock = game.GetComponent<CardDock>(); dock.SetExpanded(true, true);
        Canvas.ForceUpdateCanvases();
        var uiIds = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(rect => rect.GetInstanceID()).OrderBy(id => id).ToArray();
        var worker = Person(); var pointer = DragPointer(worker);
        worker.OnBeginDrag(pointer);
        Assert.That(worker.IsDragging, Is.True);
        foreach (var station in new[] { school, research, factoryStation })
            Assert.That(station.Card.DropTargetFeedback.IsAvailable, Is.True);
        Assert.That(houses.Any(house => house.House.Card.DropTargetFeedback.IsAvailable), Is.False);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(research.Card.DropTargetFeedback.Visibility, Is.GreaterThan(.2f));
        Assert.That(school.TryAssign(game.Cards.First(card => card.Kind == CardKind.Person && card != worker && !card.IsStored)), Is.True);
        Assert.That(school.TryAssign(game.Cards.First(card => card.Kind == CardKind.Person && card != worker && !card.IsStored)), Is.True);
        game.PreviewDrop(worker, RectTransformUtility.WorldToScreenPoint(null, school.Card.Rect.position), null);
        Assert.That(school.Card.DropTargetFeedback.IsAvailable, Is.False);
        Assert.That(research.Card.DropTargetFeedback.IsAvailable, Is.True);
        Assert.That(bank.TrySpend(bank.CurrentPoints), Is.True);
        Invoke(game, "LateUpdate");
        Assert.That(research.Card.DropTargetFeedback.IsAvailable, Is.False);
        Assert.That(factoryStation.Card.DropTargetFeedback.IsAvailable, Is.False, "Idle factory workers must still be affordable.");
        Assert.That(Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(rect => rect.GetInstanceID()).OrderBy(id => id).ToArray(), Is.EqualTo(uiIds), "Drag guides use the saved artwork, never new UI.");
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, game.Sidebar.PeopleArea.position);
        worker.OnEndDrag(pointer);
        Assert.That(game.IsDraggingAnyCard, Is.False);
        Assert.That(game.Cards.Any(card => card.DropTargetFeedback != null && card.DropTargetFeedback.IsAvailable), Is.False);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(factoryStation.Card.DropTargetFeedback.Visibility, Is.LessThan(.01f));
    }

    [UnityTest]
    public IEnumerator EducatedPersonHighlightsResearchAndFactoryButNeverSchool()
    {
        var graduate = Educate();
        yield return new WaitForSecondsRealtime(.8f);
        var pointer = DragPointer(graduate); graduate.OnBeginDrag(pointer);
        Assert.That(graduate.IsDragging, Is.True);
        Assert.That(research.Card.DropTargetFeedback.IsAvailable && factoryStation.Card.DropTargetFeedback.IsAvailable, Is.True);
        Assert.That(school.Card.DropTargetFeedback.IsAvailable, Is.False);
        graduate.OnEndDrag(pointer);
        Assert.That(Field<CardDropTargetFeedback>(game, "peopleDropTarget").IsAvailable, Is.False);
    }

    [UnityTest]
    public IEnumerator ReloadingTheSceneDuringADragClearsSavedOutlinesWithoutMissingReferences()
    {
        var worker = Person(); var pointer = DragPointer(worker);
        worker.OnBeginDrag(pointer);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(research.Card.DropTargetFeedback.Visibility, Is.GreaterThan(.1f));
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("2DCardScene");
        yield return null;
        var loaded = Object.FindFirstObjectByType<CardGameController>();
        Assert.That(loaded.IsDraggingAnyCard, Is.False);
        Assert.That(loaded.Cards.Where(card => card.IsBuilding).All(card => !card.DropTargetFeedback.IsAvailable), Is.True);
        Assert.That(loaded.AbilityPoints.CurrentPoints, Is.EqualTo(10));
    }

    [UnityTest]
    public IEnumerator BlueprintAndItemGuidesRejectBusyFactoriesAndOccupiedHouseSockets()
    {
        bank.AddPoints(20);
        var plan = Research(Person());
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        var firePlan = Research(Person(), UpgradeKind.Fire);
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(game.Vault.Withdraw(firePlan), Is.SameAs(firePlan));
        var pointer = DragPointer(firePlan); firePlan.OnBeginDrag(pointer);
        Assert.That(factoryStation.Card.DropTargetFeedback.IsAvailable, Is.False);
        Assert.That(Field<CardDropTargetFeedback>(game, "vaultDropTarget").IsAvailable, Is.True);
        Assert.That(houses.Any(house => house.House.Card.DropTargetFeedback.IsAvailable), Is.False);
        firePlan.OnEndDrag(pointer);
        Assert.That(factory.ReleaseBlueprint(false), Is.SameAs(plan));
        Assert.That(game.Vault.Withdraw(firePlan), Is.SameAs(firePlan));
        pointer = DragPointer(firePlan); firePlan.OnBeginDrag(pointer);
        Assert.That(factoryStation.Card.DropTargetFeedback.IsAvailable, Is.True);
        firePlan.OnEndDrag(pointer);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        factory.AdvanceWork(100f);
        yield return new WaitForSecondsRealtime(.8f);
        var products = game.Cards.Where(card => card.Kind == CardKind.Upgrade && !card.IsStored).ToArray();
        pointer = DragPointer(products[0]); products[0].OnBeginDrag(pointer);
        Assert.That(houses.All(house => house.House.Card.DropTargetFeedback.IsAvailable), Is.True);
        Assert.That(Field<CardDropTargetFeedback>(game, "vaultDropTarget").IsAvailable, Is.False);
        Assert.That(houses[0].House.TryInstall(products[1]), Is.True);
        game.PreviewDrop(products[0], RectTransformUtility.WorldToScreenPoint(null, houses[1].House.Card.Rect.position), null);
        Assert.That(houses[0].House.Card.DropTargetFeedback.IsAvailable, Is.False);
        Assert.That(houses.Skip(1).All(house => house.House.Card.DropTargetFeedback.IsAvailable), Is.True);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, houses[1].House.Card.Rect.position);
        products[0].OnEndDrag(pointer);
        Assert.That(houses[1].House.GetInstalled(UpgradeKind.RailSystem), Is.SameAs(products[0]));
        Assert.That(game.Cards.Any(card => card.DropTargetFeedback != null && card.DropTargetFeedback.IsAvailable), Is.False);
    }
}

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator SavedDestinationOutlinesShowOnNativeArtworkDuringPeopleBlueprintAndItemDrags()
    {
        Assert.That(game.AbilityPoints.TrySpend(game.AbilityPoints.CurrentPoints - 10), Is.True);
        var directory = Path.GetFullPath("Library/CardGameTestResults/TsunamiTurns");
        Directory.CreateDirectory(directory);
        var worker = People()[0];
        var pointer = new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(null, worker.Rect.position),
            pointerId = -1, button = PointerEventData.InputButton.Left
        };
        worker.OnBeginDrag(pointer);
        yield return CaptureCanvas(Path.Combine(directory, "people-targets.png"), settleSeconds: .3f);
        Assert.That(school.Card.DropTargetFeedback.Visibility, Is.GreaterThan(.3f));
        worker.OnEndDrag(pointer);
        Assert.That(research.TryAssign(worker), Is.True);
        research.AdvanceWork(100f); game.SelectBlueprint(0);
        yield return new WaitForSecondsRealtime(.7f);
        var plan = game.Vault.Withdraw(UpgradeKind.RailSystem);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, plan.Rect.position);
        plan.OnBeginDrag(pointer);
        yield return CaptureCanvas(Path.Combine(directory, "blueprint-targets.png"), settleSeconds: .3f);
        plan.OnEndDrag(pointer);
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>();
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        Assert.That(factory.GetComponent<BuildingStation>().TryAssign(People().First(person => !person.UsedThisTurn)), Is.True);
        factory.AdvanceWork(100f);
        yield return new WaitForSecondsRealtime(.7f);
        var item = game.Cards.First(card => card.Kind == CardKind.Upgrade && !card.IsStored);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, item.Rect.position);
        item.OnBeginDrag(pointer);
        yield return CaptureCanvas(Path.Combine(directory, "item-targets.png"), settleSeconds: .3f);
        item.OnEndDrag(pointer);
    }
}
