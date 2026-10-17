using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class CardAbilityPointsPlayModeTests
{
    private CardGameController game;
    private AbilityPoints bank;
    private EarthquakeRoundController round;
    private MonsterSequenceController sequence;
    private EarthquakeTestController earthquake;
    private CityCameraController navigation;
    private BuildingStation school, research, factoryStation;
    private FactoryProduction factory;
    private HouseCityUpgradeLink[] houses;
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);

    [UnitySetUp]
    public IEnumerator LoadRound()
    {
        yield return SceneManager.LoadSceneAsync("2DCardScene"); yield return null;
        game = Object.FindFirstObjectByType<CardGameController>(); bank = game.AbilityPoints;
        round = Object.FindFirstObjectByType<EarthquakeRoundController>();
        sequence = Object.FindFirstObjectByType<MonsterSequenceController>();
        earthquake = Object.FindFirstObjectByType<EarthquakeTestController>();
        navigation = Object.FindFirstObjectByType<CityCameraController>();
        school = Station(CardKind.School); research = Station(CardKind.ResearchCenter); factoryStation = Station(CardKind.Factory);
        factory = factoryStation.GetComponent<FactoryProduction>();
        foreach (var station in new[] { school, research, factoryStation }) station.enabled = false;
        factory.enabled = false;
        Object.FindFirstObjectByType<HouseUpgradePresentation>().enabled = false;
        var dock = game.GetComponent<CardDock>(); dock.enabled = false; dock.SetExpanded(false, true);
        houses = game.GetComponentsInChildren<HouseCityUpgradeLink>().OrderBy(link => link.House.Card.InitialGridCell).ToArray();
        Assert.That(bank.CurrentPoints, Is.EqualTo(10)); Assert.That(round.RoundNumber, Is.EqualTo(1));
    }

    private BuildingStation Station(CardKind kind) => game.Cards.Single(card => card.Kind == kind).Station;
    private DraggableCard Person() => game.Cards.First(card => card.Kind == CardKind.Person && !card.IsStored && !card.UsedThisTurn);
    private DraggableCard Educate()
    {
        Assert.That(school.TryAssign(Person()), Is.True); school.AdvanceWork(game.EducationSeconds + .01f);
        return game.Cards.First(card => card.Kind == CardKind.EducatedPerson && !card.IsStored && !card.UsedThisTurn);
    }
    private DraggableCard Research(DraggableCard worker, UpgradeKind kind = UpgradeKind.RailSystem)
    {
        Assert.That(research.TryAssign(worker), Is.True); research.AdvanceWork(100f);
        Assert.That(game.IsChoosingBlueprint, Is.True); game.SelectBlueprint((int)kind - 1);
        return game.Cards.Single(card => card.Kind == CardKind.Blueprint && card.UpgradeType == kind);
    }
    private DraggableCard Produce(DraggableCard plan)
    {
        Assert.That(factory.TryInsertBlueprint(plan), Is.True); factory.AdvanceWork(100f);
        return game.Cards.Last(card => card.Kind == CardKind.Upgrade && !card.IsStored);
    }
    private void PrepareSeismic(int batches)
    {
        var plan = Research(Person());
        for (int i = 0; i < batches; i++)
        {
            Assert.That(factoryStation.TryAssign(Person()), Is.True);
            Produce(plan);
        }
        var products = game.Cards.Where(card => card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.RailSystem).ToArray();
        for (int i = 0; i < products.Length; i++) Assert.That(houses[i].House.TryInstall(products[i]), Is.True);
    }

    [UnityTest]
    public IEnumerator EducationCostsTwoAPAndProducesOneEducatedPersonOnlyAfterItsTimer()
    {
        var person = Person(); var position = person.Rect.position;
        Assert.That(school.TryAssign(person), Is.True); Assert.That(bank.CurrentPoints, Is.EqualTo(8));
        school.AdvanceWork(game.EducationSeconds * .5f);
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4)); Assert.That(game.EducatedPeopleCount, Is.Zero);
        Assert.That(round.CanReady, Is.False);
        school.AdvanceWork(game.EducationSeconds * .5f + .01f);
        Assert.That(game.NormalPeopleCount, Is.EqualTo(3)); Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
        Assert.That(bank.CurrentPoints, Is.EqualTo(8));
        Assert.That(school.TryAssign(game.Cards.First(card => card.Kind == CardKind.EducatedPerson)), Is.False);
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(round.CanReady, Is.True);
    }

    [UnityTest]
    public IEnumerator UnfinishedResearchRefundsTheActualChargeAndCompletedResearchersCannotWorkAgain()
    {
        var worker = Educate(); Assert.That(bank.CurrentPoints, Is.EqualTo(8));
        Assert.That(research.TryAssign(worker), Is.True); Assert.That(bank.CurrentPoints, Is.EqualTo(6));
        research.AdvanceWork(3f); Assert.That(research.ResearchProgress, Is.GreaterThan(0f));
        Set(research, "educatedAbilityPointCost", 7);
        research.ReleaseWorker(0, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(8), "Refund the amount charged, not an Inspector price changed afterwards.");
        Assert.That(worker.UsedThisTurn, Is.False); Assert.That(research.ResearchProgress, Is.Zero);
        Set(research, "educatedAbilityPointCost", 2);
        Assert.That(research.TryAssign(worker), Is.True); Assert.That(bank.CurrentPoints, Is.EqualTo(6));
        research.AdvanceWork(100f); Assert.That(game.IsChoosingBlueprint, Is.True);
        Assert.That(worker.UsedThisTurn, Is.True); game.SelectBlueprint(0);
        Assert.That(research.TryAssign(worker), Is.False); Assert.That(factoryStation.TryAssign(worker), Is.False);
        Assert.That(bank.CurrentPoints, Is.EqualTo(6), "Completed work is not refunded.");
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(worker.GetComponent<CanvasGroup>().alpha, Is.LessThan(.5f));
    }

    [UnityTest]
    public IEnumerator FactoryChargesAtomicallyAndCompletedWorkersCannotStartAnotherOrder()
    {
        var educated = Educate(); var plan = Research(Person());
        Assert.That(bank.CurrentPoints, Is.EqualTo(4));
        Assert.That(factoryStation.TryAssign(educated), Is.True);
        var normal = Person(); Assert.That(factoryStation.TryAssign(normal), Is.True);
        Assert.That(factory.CurrentOrderCost, Is.EqualTo(8));
        Assert.That(factory.TryInsertBlueprint(plan), Is.False); Assert.That(bank.CurrentPoints, Is.EqualTo(4));
        Assert.That(educated.UsedThisTurn || normal.UsedThisTurn, Is.False);
        factoryStation.ReleaseWorker(1, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(4), "An idle worker has no AP charge to refund.");
        Assert.That(factory.TryInsertBlueprint(plan), Is.True); Assert.That(bank.CurrentPoints, Is.EqualTo(1));
        Assert.That(factoryStation.TryAssign(normal), Is.False); Assert.That(normal.UsedThisTurn, Is.False);
        factory.AdvanceWork(100f);
        Assert.That(factoryStation.WorkerCount, Is.Zero); Assert.That(educated.UsedThisTurn, Is.True);
        Assert.That(factoryStation.TryAssign(educated), Is.False); Assert.That(bank.CurrentPoints, Is.EqualTo(1));
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.EqualTo(2));
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        factory.AdvanceWork(100f);
        Assert.That(factory.Progress, Is.Zero); Assert.That(factory.WorkRate, Is.Zero);
        Assert.That(factory.ReleaseBlueprint(false), Is.SameAs(plan));
        yield return null;
    }

    [UnityTest]
    public IEnumerator RemovingAnUnfinishedFactoryWorkerRefundsAPAndCancellingThePlanCannotDoubleRefund()
    {
        var plan = Research(Person()); Assert.That(bank.CurrentPoints, Is.EqualTo(6));
        Assert.That(factory.TryInsertBlueprint(plan), Is.True); Assert.That(bank.CurrentPoints, Is.EqualTo(6));
        var worker = Person(); Assert.That(factoryStation.TryAssign(worker), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(1)); factory.AdvanceWork(1f);
        factoryStation.ReleaseWorker(0, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(6)); Assert.That(factory.Progress, Is.Zero);
        Assert.That(worker.UsedThisTurn, Is.False);
        Assert.That(factoryStation.TryAssign(worker), Is.True); Assert.That(bank.CurrentPoints, Is.EqualTo(1));
        Assert.That(factory.ReleaseBlueprint(false), Is.SameAs(plan));
        Assert.That(bank.CurrentPoints, Is.EqualTo(6)); Assert.That(factoryStation.WorkerCount, Is.Zero);
        Assert.That(factory.ReleaseBlueprint(false), Is.Null); Assert.That(bank.CurrentPoints, Is.EqualTo(6));
        Assert.That(plan.StoredInVault, Is.SameAs(game.Vault));
        yield return null;
    }

    [Test]
    public void InsufficientFundsAndFullSlotsNeverDebitOrConsumeAnotherPerson()
    {
        Assert.That(school.TryAssign(Person()), Is.True); Assert.That(school.TryAssign(Person()), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(6)); var third = Person(); var position = third.Rect.position;
        Assert.That(school.TryAssign(third), Is.False); Assert.That(bank.CurrentPoints, Is.EqualTo(6)); Assert.That(third.Rect.position, Is.EqualTo(position));
        Assert.That(bank.TrySpend(5), Is.True);
        Assert.That(research.TryAssign(third), Is.False); Assert.That(bank.CurrentPoints, Is.EqualTo(1));
        Assert.That(third.AssignedTo, Is.Null); Assert.That(third.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void InspectorPricesDriveBothTheChargeAndTheExistingCostBadges()
    {
        Set(school, "normalAbilityPointCost", 5); school.RefreshAbilityCostLabels();
        Assert.That(school.transform.Find("FolderArt/APNormalPeople").GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("5 AP"));
        Assert.That(school.TryAssign(Person()), Is.True); Assert.That(bank.CurrentPoints, Is.EqualTo(5));
        Set(research, "educatedAbilityPointCost", 7); research.RefreshAbilityCostLabels();
        Assert.That(research.transform.Find("FolderArt/APEducated").GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("7 AP"));
    }

    [UnityTest]
    public IEnumerator ACollapsedHouseStartsAFreshTurnAfterTheSavedDelayAndRetriesTheSameSequence()
    {
        PrepareSeismic(1); Assert.That(bank.CurrentPoints, Is.EqualTo(1));
        var plan = game.Cards.Single(card => card.Kind == CardKind.Blueprint);
        bank.AddPoints(4);
        Assert.That(factoryStation.TryAssign(Person()), Is.True); Produce(plan);
        var oldRails = game.Cards.Where(card => card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.RailSystem).ToArray();
        Assert.That(oldRails.Length, Is.EqualTo(4));
        Assert.That(oldRails.Count(card => !card.IsStored), Is.EqualTo(2));
        yield return new WaitForSecondsRealtime(1.6f);
        var oldPeople = game.Cards.Where(card => card.IsPerson).ToArray();
        var grounds = houses.Select(house => house.Building.Find("Apartment_Ground").position).ToArray();
        sequence.SelectSequence(3); Assert.That(round.CanReady, Is.True);
        Field<Button>(round, "readyButton").onClick.Invoke();
        Assert.That(sequence.ActiveSequence, Is.EqualTo(1)); Assert.That(bank.ActionsLocked, Is.True);
        Assert.That(school.TryAssign(Person()), Is.False); Assert.That(houses[0].House.Release(UpgradeKind.RailSystem), Is.Null);
        Assert.That(bank.TrySpend(0), Is.False); Invoke(round, "LateUpdate");
        foreach (var button in Field<Button[]>(round, "debugStartButtons")) Assert.That(button.interactable, Is.False);
        Until(() => houses[2].House.IsDestroyed);
        round.AdvanceRound(round.NextTurnAfterCollapseSeconds - .01f);
        Assert.That(round.RoundNumber, Is.EqualTo(1)); Assert.That(bank.CurrentPoints, Is.Zero);
        round.AdvanceRound(.02f);
        Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.NextRoundPreparation));
        Assert.That(round.RoundNumber, Is.EqualTo(2)); Assert.That(round.SequenceNumber, Is.EqualTo(1));
        Assert.That(round.LastRoundSurvived, Is.False); Assert.That(bank.CurrentPoints, Is.EqualTo(16));
        Assert.That(round.LastProtectedHouseCount, Is.EqualTo(2)); Assert.That(round.LastSurvivalReward, Is.EqualTo(6));
        AssertFreshPeople(oldPeople);
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False, "The next attempt rebuilds the damaged city.");
        Assert.That(oldRails.Any(card => game.Cards.Contains(card)), Is.False, "Installed and loose Rail System products are consumed on a failed earthquake turn.");
        Assert.That(game.Cards.Any(card => card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.RailSystem), Is.False);
        Assert.That(houses.All(house => house.House.GetInstalled(UpgradeKind.RailSystem) == null), Is.True);
        Assert.That(houses.All(house => !house.Building.GetComponent<ApartmentUpgradeModel>().HasSeismic), Is.True);
        Assert.That(game.Cards.Contains(plan), Is.False, "A failed earthquake also removes blueprints discovered in that turn.");
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.False);
        Assert.That(game.Vault.AvailableCount(UpgradeKind.RailSystem), Is.Zero);
        Assert.That(game.Vault.SlotFor(UpgradeKind.RailSystem).CardRect.gameObject.activeSelf, Is.False);
        Assert.That(game.CanResearchBlueprint(UpgradeKind.RailSystem), Is.True);
        for (int i = 0; i < 4; i++) Assert.That(houses[i].Building.Find("Apartment_Ground").position, Is.EqualTo(grounds[i]));
        for (int i = 0; i < 6; i++) round.AdvanceRound(100f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(16), "A transition must not pay twice.");
        Until(() => !navigation.IsEventCameraActive);
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(round.CanReady, Is.True);
    }

    [UnityTest]
    public IEnumerator SurvivingWithAllFourHousesUnlocksSequenceTwoWithTheSameInstalledSeismicCards()
    {
        // Four extra AP isolate the all-safe outcome without replaying the separate retry test.
        bank.AddPoints(4); PrepareSeismic(2); Assert.That(bank.CurrentPoints, Is.Zero);
        var installed = houses.Select(house => house.House.GetInstalled(UpgradeKind.RailSystem)).ToArray();
        yield return new WaitForSecondsRealtime(1.6f); round.Ready();
        Until(() => round.Phase == EarthquakeRoundPhase.NextRoundPreparation);
        Assert.That(round.LastRoundSurvived, Is.True); Assert.That(round.SequenceNumber, Is.EqualTo(2));
        Assert.That(sequence.SelectedSequence, Is.EqualTo(2));
        Assert.That(round.LastProtectedHouseCount, Is.EqualTo(4)); Assert.That(round.LastSurvivalReward, Is.EqualTo(12));
        Assert.That(bank.CurrentPoints, Is.EqualTo(18)); Assert.That(round.CanReady, Is.True);
        Assert.That(round.LastRoundGrant, Is.EqualTo(6), "The first Tsunami preparation already uses the smaller bonus.");
        for (int i = 0; i < houses.Length; i++)
        {
            Assert.That(houses[i].House.GetInstalled(UpgradeKind.RailSystem), Is.SameAs(installed[i]));
            Assert.That(houses[i].Building.GetComponent<ApartmentUpgradeModel>().HasSeismic, Is.True);
        }
        var plan = game.Cards.Single(card => card.Kind == CardKind.Blueprint);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.True);
        Assert.That(game.CanUseBlueprint(UpgradeKind.RailSystem), Is.False);
        Assert.That(game.CanResearchBlueprint(UpgradeKind.RailSystem), Is.False);
        Assert.That(game.Vault.Withdraw(plan), Is.Null);
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        Assert.That(factory.CanAccept(plan), Is.False);
        Assert.That(factory.TryInsertBlueprint(plan), Is.False);
        Assert.That(bank.CurrentPoints, Is.EqualTo(18), "An obsolete seismic plan must not charge production AP.");
        Assert.That(factoryStation.GetWorker(0).UsedThisTurn, Is.False);
        factoryStation.ReleaseWorker(0, false);
        var caption = Field<TextMeshProUGUI>(game.Vault.SlotFor(UpgradeKind.RailSystem), "discovery");
        Assert.That(caption.text, Is.EqualTo("COMPLETED"));
        Assert.That(game.CanUseBlueprint(UpgradeKind.Breakwater) && game.CanUseBlueprint(UpgradeKind.Fire), Is.True);
        round.Ready(); Assert.That(sequence.ActiveSequence, Is.EqualTo(2));
        Assert.That(Field<TsunamiTestController>(round, "tsunami").IsRunning, Is.True);
        sequence.StopSequence(); round.AdvanceRound(0f);
    }

    [UnityTest]
    public IEnumerator UnspentAPCarriesOverAndAFailedTurnReplenishesExactlyFourNormalPeople()
    {
        Assert.That(round.CanReady, Is.True); round.Ready();
        Until(() => round.Phase == EarthquakeRoundPhase.NextRoundPreparation);
        Assert.That(round.LastProtectedHouseCount, Is.Zero); Assert.That(bank.CurrentPoints, Is.EqualTo(20));
        Assert.That(round.LastRoundGrant, Is.EqualTo(10), "Earthquake retries keep their original bonus until the stage is cleared.");
        Assert.That(round.SequenceNumber, Is.EqualTo(1)); Assert.That(round.RoundNumber, Is.EqualTo(2));
        Assert.That(houses.Any(house => house.House.IsDestroyed || house.Building.GetComponent<EarthquakeHouseDamage>().IsDestroyed), Is.False);
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4)); Assert.That(game.EducatedPeopleCount, Is.Zero);
        Assert.That(game.Cards.Where(card => card.IsPerson).All(card => card.CanStartWork), Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator StoppingAnEarthquakeRestoresItsDamageAndNeverGrantsAP()
    {
        var plan = Research(Person());
        yield return new WaitForSecondsRealtime(.65f);
        var body = houses[0].Building.Find("Apartment_Motion/Apartments_Normal/Apartment1"); var position = body.position; var rotation = body.rotation;
        round.Ready(); Until(() => houses[0].House.IsDestroyed); Step(.5f);
        sequence.StopSequence(); Invoke(round, "Update"); Invoke(navigation, "AdvanceNavigation", 2f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(6)); Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.Preparation));
        Assert.That(game.Cards.Contains(plan) && game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.True, "STOP cancels the event without losing this turn's discovery.");
        Assert.That(body.position, Is.EqualTo(position)); Assert.That(Quaternion.Angle(body.rotation, rotation), Is.LessThan(.001f));
        Assert.That(houses.Any(house => house.House.IsDestroyed || house.Building.GetComponent<EarthquakeHouseDamage>().IsDestroyed), Is.False);
        Assert.That(bank.ActionsLocked, Is.False); Assert.That(round.CanReady, Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ElevatedApartmentsStillNeedSeismicProtectionAndTheirGroundDoesNotFall()
    {
        var house = houses[0]; var apartment = house.Building.GetComponent<ApartmentUpgradeModel>();
        house.FloodProtection.SetInstalled(true); yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(apartment.IsElevated, Is.True);
        var damage = house.Building.GetComponent<EarthquakeHouseDamage>();
        var ground = house.Building.Find("Apartment_Ground"); var position = ground.position;
        var elevatedBodies = house.Building.Find("Apartment_Motion/Apartments_Elevated").GetComponentsInChildren<MeshFilter>().Where(mesh => mesh.name.StartsWith("Apartment") && mesh.name.IndexOf("Support", StringComparison.OrdinalIgnoreCase) < 0).ToArray();
        var rotations = elevatedBodies.Select(body => body.transform.rotation).ToArray();
        Assert.That(damage.Collapse(), Is.True); Invoke(damage, "AdvanceCollapse", damage.Duration);
        Assert.That(ground.position, Is.EqualTo(position));
        Assert.That(elevatedBodies.Where((body, i) => Quaternion.Angle(body.transform.rotation, rotations[i]) > 40f).Count(), Is.GreaterThanOrEqualTo(4));
        damage.ResetDamage();
        for (int i = 0; i < rotations.Length; i++) Assert.That(Quaternion.Angle(elevatedBodies[i].transform.rotation, rotations[i]), Is.LessThan(.001f));
    }

    [UnityTest]
    public IEnumerator DebugSequencesDoNotAdvanceThePlayableRoundOrPayRewards()
    {
        sequence.SelectSequence(1); sequence.StartSelectedSequence(); Until(() => !sequence.IsRunning && !navigation.IsEventCameraActive);
        Assert.That(bank.CurrentPoints, Is.EqualTo(10)); Assert.That(round.RoundNumber, Is.EqualTo(1));
        Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.Preparation)); Assert.That(round.LastRoundGrant, Is.Zero);
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TheStartingTenAPCanReachSequenceTwoOverTwoPlanningTurns()
    {
        PrepareSeismic(1); yield return new WaitForSecondsRealtime(1.6f);
        round.Ready(); Until(() => round.RoundNumber == 2); Until(() => !navigation.IsEventCameraActive);
        Assert.That(game.Cards.Any(card => card.Kind == CardKind.Blueprint), Is.False);
        var plan = Research(Person());
        for (int batch = 0; batch < 2; batch++)
        {
            Assert.That(factoryStation.TryAssign(Person()), Is.True); Produce(plan);
        }
        var products = game.Cards.Where(card => card.Kind == CardKind.Upgrade && !card.IsStored).ToArray();
        Assert.That(products.Length, Is.EqualTo(4));
        for (int i = 0; i < 4; i++) Assert.That(houses[i].House.TryInstall(products[i]), Is.True);
        yield return new WaitForSecondsRealtime(1.6f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(3)); round.Ready();
        Until(() => round.RoundNumber == 3);
        Assert.That(round.SequenceNumber, Is.EqualTo(2)); Assert.That(round.LastRoundSurvived, Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(21));
        Assert.That(houses.All(house => house.House.GetInstalled(UpgradeKind.RailSystem) != null), Is.True);
        AssertFreshPeople(Array.Empty<DraggableCard>());
    }

    [Test]
    public void CancellingSchoolRefundsAPAndCancellingOneResearcherOnlyRemovesTheirOwnContribution()
    {
        var student = Person(); Assert.That(school.TryAssign(student), Is.True); school.AdvanceWork(5f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(8)); school.ReleaseWorker(0, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(10)); Assert.That(student.CanStartWork, Is.True);
        Assert.That(school.GetEducationProgress(0), Is.Zero);
        Assert.That(research.TryAssign(student), Is.True);
        var second = Person(); Assert.That(research.TryAssign(second), Is.True); research.AdvanceWork(game.NormalResearchSeconds * .2f);
        Assert.That(research.ResearchProgress, Is.EqualTo(.4f).Within(.0001f));
        research.ReleaseWorker(0, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(6)); Assert.That(research.ResearchProgress, Is.EqualTo(.2f).Within(.0001f));
        research.ReleaseWorker(1, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(10)); Assert.That(research.ResearchProgress, Is.Zero);
        Assert.That(student.UsedThisTurn || second.UsedThisTurn, Is.False);
    }

    [Test]
    public void WithdrawingOneFactoryWorkerRefundsOnlyTheirChargeAndCancelsOnlyTheirEffort()
    {
        var plan = Research(Person()); bank.AddPoints(4);
        var first = Person(); var second = Person();
        Assert.That(factoryStation.TryAssign(first), Is.True);
        second = Person(); Assert.That(factoryStation.TryAssign(second), Is.True);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True); Assert.That(bank.CurrentPoints, Is.Zero);
        factory.AdvanceWork(.5f); Assert.That(factory.Progress, Is.EqualTo(.2f).Within(.0001f));
        factoryStation.ReleaseWorker(0, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(5)); Assert.That(factory.Progress, Is.EqualTo(.1f).Within(.0001f));
        factory.AdvanceWork(100f);
        Assert.That(second.UsedThisTurn, Is.True); Assert.That(first.UsedThisTurn, Is.False);
        Assert.That(bank.CurrentPoints, Is.EqualTo(5)); Assert.That(factoryStation.WorkerCount, Is.Zero);
    }

    [Test]
    public void WorkStatusCaptionsStayEmptyDuringEducationResearchAndBlueprintChoice()
    {
        Assert.That(school.TryAssign(Person()), Is.True);
        Assert.That(research.TryAssign(Person()), Is.True);
        school.AdvanceWork(3f); research.AdvanceWork(3f);
        foreach (var station in new[] { school, research, factoryStation })
        {
            var caption = station.transform.Find("WorkStatus");
            Assert.That(caption.gameObject.activeSelf, Is.False);
            Assert.That(caption.GetComponent<TMP_Text>().text, Is.Empty);
        }
        research.AdvanceWork(100f);
        Assert.That(research.transform.Find("WorkStatus").GetComponent<TMP_Text>().text, Is.Empty);
        game.SelectBlueprint(0);
    }

    [UnityTest]
    public IEnumerator FreshTurnRemovesGraduatesAssignedPeopleAndFolderProxiesAndRestoresNativePositions()
    {
        var positions = game.Cards.Where(card => card.Kind == CardKind.Person).Select(card => game.Sidebar.PeopleArea.InverseTransformPoint(card.Rect.position)).ToArray();
        var educated = Educate();
        Assert.That(research.TryAssign(educated), Is.True);
        var oldPeople = game.Cards.Where(card => card.IsPerson).ToArray();
        game.ResetTurnPeople(); AssertFreshPeople(oldPeople);
        Assert.That(research.WorkerCount + school.WorkerCount + factoryStation.WorkerCount, Is.Zero);
        Assert.That(game.HasActiveCardAnimation, Is.False);
        var fresh = game.Cards.Where(card => card.IsPerson).ToArray();
        for (int i = 0; i < 4; i++)
            Assert.That(Vector3.Distance(game.Sidebar.PeopleArea.InverseTransformPoint(fresh[i].Rect.position), positions[i]), Is.LessThan(.01f));
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(game.Cards.Count(card => card.IsPerson), Is.EqualTo(4));
        Assert.That(game.GetComponentsInChildren<WorkerSlot>(true).Any(slot => slot.IsAnimating || slot.EntryImage.gameObject.activeSelf), Is.False);
    }

    [UnityTest]
    public IEnumerator TsunamiCollapseInSequenceTwoAlsoStartsANewTurnAndKeepsExistingSeismicProtection()
    {
        bank.AddPoints(4); PrepareSeismic(2);
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready(); Until(() => round.RoundNumber == 2);
        Assert.That(round.SequenceNumber, Is.EqualTo(2)); round.Ready();
        Assert.That(sequence.ActiveSequence, Is.EqualTo(2));
        Until(() => houses.Any(house => house.House.IsDestroyed));
        round.AdvanceRound(round.NextTurnAfterCollapseSeconds + .01f);
        Assert.That(round.RoundNumber, Is.EqualTo(3)); Assert.That(round.SequenceNumber, Is.EqualTo(2));
        Assert.That(round.LastRoundSurvived, Is.False);
        Assert.That(round.LastProtectedHouseCount, Is.Zero);
        Assert.That(round.LastSurvivalReward, Is.Zero, "Seismic cards alone do not earn a water survival reward.");
        Assert.That(bank.CurrentPoints, Is.EqualTo(24));
        Assert.That(houses.All(house => house.House.GetInstalled(UpgradeKind.RailSystem) != null), Is.True);
        Assert.That(houses.All(house => !house.House.IsDestroyed && !house.Building.GetComponent<ApartmentTsunamiCollapse>().HasCollapsed), Is.True);
        AssertFreshPeople(Array.Empty<DraggableCard>());
    }

    [UnityTest]
    public IEnumerator RollingBackOnlyNewDiscoveriesAlsoClearsAnIdleFactoryPlan()
    {
        var committed = Research(Person());
        game.EndBlueprintTurn(discardNewDiscoveries: false);
        var fresh = Research(Person(), UpgradeKind.Fire);
        Assert.That(factory.TryInsertBlueprint(fresh), Is.True);
        Assert.That(factory.PendingBlueprint, Is.SameAs(fresh));
        Assert.That(bank.CurrentPoints, Is.EqualTo(2));
        game.EndBlueprintTurn(discardNewDiscoveries: true);
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(game.Cards.Contains(fresh), Is.False);
        Assert.That(game.Cards.Contains(committed), Is.True);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.True);
        Assert.That(game.Vault.AvailableCount(UpgradeKind.RailSystem), Is.EqualTo(1));
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.Fire), Is.False);
        Assert.That(game.Vault.AvailableCount(UpgradeKind.Fire), Is.Zero);
        Assert.That(game.Vault.SlotFor(UpgradeKind.Fire).CardRect.gameObject.activeSelf, Is.False);
        Assert.That(game.CanResearchBlueprint(UpgradeKind.Fire), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(2));
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(game.Vault.SlotFor(UpgradeKind.Fire).IsArriving, Is.False);
    }

    [UnityTest]
    public IEnumerator RollingBackAnArrivingDiscoveryLeavesNoGhostAndItCanBeResearchedAgain()
    {
        var plan = Research(Person());
        var slot = game.Vault.SlotFor(UpgradeKind.RailSystem);
        Assert.That(slot.IsArriving, Is.True);
        game.EndBlueprintTurn(discardNewDiscoveries: true);
        Assert.That(game.Cards.Contains(plan), Is.False);
        Assert.That(slot.IsArriving, Is.False);
        Assert.That(slot.CardRect.gameObject.activeSelf, Is.False);
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(slot.CardRect.gameObject.activeSelf, Is.False);
        var replacement = Research(Person());
        Assert.That(replacement, Is.Not.SameAs(plan));
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(slot.CardRect.gameObject.activeSelf, Is.True);
        Assert.That(game.Vault.AvailableCount(UpgradeKind.RailSystem), Is.EqualTo(1));
        Assert.That(bank.CurrentPoints, Is.EqualTo(2));
    }

    private void AssertFreshPeople(DraggableCard[] oldPeople)
    {
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4)); Assert.That(game.EducatedPeopleCount, Is.Zero);
        Assert.That(game.Cards.Where(card => card.IsPerson).All(card => card.CanStartWork && !card.IsStored && card.gameObject.activeSelf), Is.True);
        Assert.That(oldPeople.Any(person => game.Cards.Contains(person)), Is.False);
    }

    private void Until(Func<bool> condition)
    {
        int steps = 0;
        while (!condition() && steps++ < 2400) Step(.05f);
        Assert.That(condition(), Is.True, "Round stalled: " + round.Phase + " / " + sequence.Stage);
    }
    private void Step(float seconds)
    {
        if (earthquake.IsRunning) Invoke(earthquake, "AdvanceShake", seconds);
        var water = Field<TsunamiTestController>(round, "tsunami");
        if (water.IsRunning) Invoke(water, "AdvanceTsunami", seconds);
        var flames = Field<FireTestController>(round, "fire");
        if (flames.IsRunning) Invoke(flames, "AdvanceFire", seconds);
        var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        if (walk.IsWalkingAfterEvent) Invoke(walk, "AdvanceAfterEventWalk", seconds);
        Invoke(sequence, "AdvanceSequence", seconds); Invoke(sequence, "LateUpdate");
        Invoke(navigation, "AdvanceNavigation", seconds);
        foreach (var house in houses) Invoke(house.Building.GetComponent<EarthquakeHouseDamage>(), "AdvanceCollapse", seconds);
        round.AdvanceRound(seconds); Invoke(round, "LateUpdate");
    }
    private void CaptureWorld(string name)
    {
        var camera = Object.FindFirstObjectByType<CityViewport>().WorldCamera;
        var original = camera.targetTexture; var active = RenderTexture.active; float aspect = camera.aspect;
        var target = new RenderTexture(1600, 900, 24); var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = 1600f / 900f; camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
            var dir = Path.GetFullPath("Library/CardGameTestResults/AbilityPoints"); Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, name), pixels.EncodeToPNG());
        }
        finally { camera.targetTexture = original; camera.aspect = aspect; RenderTexture.active = active; target.Release(); Object.Destroy(target); Object.Destroy(pixels); }
    }
}

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator APControlsUseSavedWidgetsAtBothDockPositionsAndReadyReceivesClicks()
    {
        var bank = game.AbilityPoints;
        Assert.That(bank.TrySpend(bank.CurrentPoints - 10), Is.True);
        var round = Object.FindFirstObjectByType<EarthquakeRoundController>();
        var ready = ControlField<Button>(round, "readyButton");
        var ids = SequenceUIIds();
        var dock = game.GetComponent<CardDock>();
        dock.SetExpanded(false, true);
        string directory = Path.GetFullPath("Library/CardGameTestResults/AbilityPoints"); Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "ap-planning-one-row.png"), afterRender: camera => AssertTopButton(ready, camera));
        dock.SetExpanded(true, true);
        yield return CaptureCanvas(Path.Combine(directory, "ap-planning-two-rows.png"), afterRender: camera => AssertTopButton(ready, camera));
        Assert.That(SequenceUIIds(), Is.EqualTo(ids));
        Assert.That(bank.CurrentPoints, Is.EqualTo(10));
        Assert.That(ControlField<TMP_Text>(bank, "balanceLabel").text, Is.EqualTo("10 AP"));
    }
}
