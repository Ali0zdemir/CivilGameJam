using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class CardAbilityPointsPlayModeTests
{
    private AbilityPointFeedback APHud => Object.FindFirstObjectByType<AbilityPointFeedback>();
    private static int[] APWidgetIds() => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Select(rect => rect.GetInstanceID()).OrderBy(id => id).ToArray();

    [UnityTest]
    public IEnumerator SpendingAPShowsItsActualAmountUsingSavedWidgetsAndRestoresTheAuthoredCounter()
    {
        var hud = APHud;
        var counter = Field<RectTransform>(hud, "counter");
        var label = Field<TMP_Text>(hud, "balanceLabel");
        var popups = Field<TMP_Text[]>(hud, "spendLabels");
        var position = counter.anchoredPosition;
        var scale = counter.localScale;
        var color = label.color;
        var popupPosition = popups[0].rectTransform.anchoredPosition;
        int[] ids = APWidgetIds();
        Assert.That(bank.TrySpend(4), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(6));
        Assert.That(label.text, Is.EqualTo("6 AP"));
        Assert.That(popups[0].text, Is.EqualTo("-4 AP"));
        Assert.That(hud.IsFlashing && !hud.IsWarning, Is.True);
        yield return new WaitForSecondsRealtime(.13f);
        Assert.That(popups[0].gameObject.activeSelf, Is.True);
        Assert.That(popups[0].rectTransform.anchoredPosition.y, Is.GreaterThan(popupPosition.y));
        Assert.That(popups[0].color.a, Is.GreaterThan(.7f));
        Assert.That(label.color.g, Is.LessThan(color.g));
        Assert.That(counter.localScale.x, Is.GreaterThan(scale.x));
        Assert.That(APWidgetIds(), Is.EqualTo(ids));
        Assert.That(popups[0].fontSize, Is.EqualTo(32f).Within(.01f));
        Assert.That(Mathf.Abs(counter.anchoredPosition.x - position.x), Is.GreaterThan(.01f));
        yield return new WaitForSecondsRealtime(.62f);
        Assert.That(hud.IsFlashing, Is.True, "Spending must shake the counter for a full second.");
        Assert.That(label.color.r, Is.GreaterThan(label.color.g * 2f));
        Assert.That(popups[0].color.r, Is.GreaterThan(popups[0].color.g * 2f));
        Assert.That(popups[0].color.a, Is.GreaterThan(.9f));
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(hud.IsFlashing, Is.False);
        Assert.That(Vector2.Distance(counter.anchoredPosition, position), Is.LessThan(.01f), "Allow subpixel Canvas rounding while detecting counter drift.");
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(popups.All(popup => !popup.gameObject.activeSelf), Is.True);
        Assert.That(hud.IsFlashing, Is.False);
        Assert.That(Vector2.Distance(counter.anchoredPosition, position), Is.LessThan(.01f));
        Assert.That(counter.localScale, Is.EqualTo(scale));
        Assert.That(Vector4.Distance(label.color, color), Is.LessThan(.00001f), "Allow float rounding while verifying the authored color is restored.");
        Assert.That(bank.TrySpend(0), Is.True);
        bank.AddPoints(4);
        Assert.That(hud.IsFlashing, Is.False, "Zero-cost assignments and refunds must not display a spending notification.");
        Assert.That(popups.All(popup => !popup.gameObject.activeSelf), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(10));
    }

    [UnityTest]
    public IEnumerator AffordabilityPreviewsStaySilentAndRejectedWorkersFlashOnlyTheirOwnCostBadge()
    {
        var graduate = Educate();
        yield return new WaitForSecondsRealtime(1.5f);
        bank.TrySpend(bank.CurrentPoints);
        yield return new WaitForSecondsRealtime(1.5f);
        var hud = APHud;
        var normal = Field<AbilityPointCostFeedback>(research, "normalCostFeedback");
        var educated = Field<AbilityPointCostFeedback>(research, "educatedCostFeedback");
        var normalLabel = Field<TMP_Text>(normal, "label");
        var badge = Field<RectTransform>(normal, "badge");
        var color = normalLabel.color;
        var position = badge.anchoredPosition;
        for (int i = 0; i < 20; i++)
        {
            Assert.That(research.CanAccept(Person()), Is.False);
            Assert.That(research.CanAccept(graduate), Is.False);
        }
        Assert.That(hud.IsWarning || normal.IsPlaying || educated.IsPlaying, Is.False);
        Assert.That(school.TryAssign(graduate), Is.False);
        Assert.That(hud.IsWarning, Is.False, "An invalid worker type is not an insufficient-AP error.");
        Assert.That(research.TryAssign(Person()), Is.False);
        Assert.That(hud.IsWarning && normal.IsPlaying, Is.True);
        Assert.That(educated.IsPlaying, Is.False);
        yield return new WaitForSecondsRealtime(.13f);
        Assert.That(normalLabel.color.g, Is.LessThan(color.g));
        Assert.That(bank.CurrentPoints, Is.Zero);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(normalLabel.color, Is.EqualTo(color));
        Assert.That(badge.anchoredPosition, Is.EqualTo(position));
        Assert.That(research.TryAssign(graduate), Is.False);
        Assert.That(educated.IsPlaying && !normal.IsPlaying, Is.True);
        yield return new WaitForSecondsRealtime(.7f);
        bank.AddPoints(4);
        Assert.That(school.TryAssign(Person()), Is.True);
        Assert.That(school.TryAssign(Person()), Is.True);
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(bank.CurrentPoints, Is.Zero);
        Assert.That(school.TryAssign(Person()), Is.False);
        Assert.That(hud.IsWarning, Is.False, "A full building must not falsely report an AP failure.");
    }

    [UnityTest]
    public IEnumerator UnaffordableFactoryOrdersWarnForBothWorkerKindsWithoutChargingOrConsumingTheBlueprint()
    {
        var graduate = Educate();
        var plan = Research(Person());
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        Assert.That(factoryStation.TryAssign(graduate), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(4));
        var normal = Field<AbilityPointCostFeedback>(factoryStation, "normalCostFeedback");
        var educated = Field<AbilityPointCostFeedback>(factoryStation, "educatedCostFeedback");
        for (int i = 0; i < 20; i++) Assert.That(factory.CanAccept(plan), Is.False);
        Assert.That(APHud.IsWarning, Is.False);
        Assert.That(factory.TryInsertBlueprint(plan), Is.False);
        Assert.That(APHud.IsWarning && normal.IsPlaying && educated.IsPlaying, Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(4));
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(plan.StoredInVault, Is.SameAs(game.Vault));
        Assert.That(factoryStation.WorkerCount, Is.EqualTo(2));
        Assert.That(Field<TMP_Text[]>(APHud, "spendLabels").All(label => !label.gameObject.activeSelf), Is.True);
        yield return new WaitForSecondsRealtime(.7f);
        bank.AddPoints(4);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        Assert.That(bank.CurrentPoints, Is.Zero);
        Assert.That(Field<TMP_Text[]>(APHud, "spendLabels").Single(label => label.gameObject.activeSelf).text, Is.EqualTo("-8 AP"));
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(factory.ReleaseBlueprint(false), Is.SameAs(plan));
        Assert.That(bank.CurrentPoints, Is.EqualTo(8));
        Assert.That(APHud.IsFlashing || APHud.IsReadyPromptActive, Is.False);
    }

    [UnityTest]
    public IEnumerator ReadyAttentionWaitsForWorkAndReturnAnimationsAndStopsWhenAPReturnsOrActionsLock()
    {
        var hud = APHud;
        Assert.That(school.TryAssign(Person()), Is.True);
        Assert.That(bank.TrySpend(bank.CurrentPoints), Is.True);
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(bank.CurrentPoints, Is.Zero);
        Assert.That(round.CanReady || hud.IsReadyPromptActive, Is.False);
        school.AdvanceWork(game.EducationSeconds + .01f);
        round.AdvanceRound(0f);
        Assert.That(round.CanReady || hud.IsReadyPromptActive, Is.False, "The graduating person is still returning to the people area.");
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(round.CanReady && hud.IsReadyPromptActive, Is.True);
        Assert.That(Field<Image>(hud, "readyGlow").gameObject.activeSelf, Is.True);
        Assert.That(round.RoundNumber, Is.EqualTo(1), "Attention must not press READY automatically.");
        bank.AddPoints(1);
        Assert.That(hud.IsReadyPromptActive, Is.False);
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(Field<Image>(hud, "readyGlow").gameObject.activeSelf, Is.False);
        Assert.That(bank.TrySpend(1), Is.True);
        yield return null;
        round.AdvanceRound(0f);
        Assert.That(hud.IsReadyPromptActive, Is.True);
        bank.SetActionsLocked(true);
        Assert.That(hud.IsReadyPromptActive, Is.False);
        bank.SetActionsLocked(false);
    }

    [UnityTest]
    public IEnumerator ReadyAttentionWaitsForBlueprintSelectionAndItsVaultArrival()
    {
        Assert.That(research.TryAssign(Person()), Is.True);
        bank.TrySpend(bank.CurrentPoints);
        research.AdvanceWork(100f);
        round.AdvanceRound(0f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        Assert.That(APHud.IsReadyPromptActive, Is.False);
        game.SelectBlueprint(0);
        round.AdvanceRound(0f);
        Assert.That(APHud.IsReadyPromptActive, Is.False);
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(round.CanReady && APHud.IsReadyPromptActive, Is.True);
    }

    [UnityTest]
    public IEnumerator APFeedbackSurvivesRapidActionsDisablingAndReloadWithoutAccumulatingOffsetsOrWidgets()
    {
        var hud = APHud;
        var counter = Field<RectTransform>(hud, "counter");
        var label = Field<TMP_Text>(hud, "balanceLabel");
        var position = counter.anchoredPosition;
        var scale = counter.localScale;
        var color = label.color;
        var ids = APWidgetIds();
        bank.AddPoints(20);
        for (int i = 0; i < 10; i++) Assert.That(bank.TrySpend(1), Is.True);
        Assert.That(Field<TMP_Text[]>(hud, "spendLabels").Count(popup => popup.gameObject.activeSelf), Is.EqualTo(4));
        for (int i = 0; i < 3; i++)
        {
            Assert.That(bank.TrySpend(100), Is.False);
            yield return new WaitForSecondsRealtime(.06f);
            Assert.That(Vector2.Distance(counter.anchoredPosition, position), Is.LessThanOrEqualTo(5.1f));
        }
        Assert.That(APWidgetIds(), Is.EqualTo(ids));
        hud.enabled = false;
        Assert.That(counter.anchoredPosition, Is.EqualTo(position));
        Assert.That(counter.localScale, Is.EqualTo(scale));
        Assert.That(label.color, Is.EqualTo(color));
        Assert.That(Field<TMP_Text[]>(hud, "spendLabels").All(popup => !popup.gameObject.activeSelf), Is.True);
        bank.TrySpend(1);
        Assert.That(hud.IsFlashing, Is.False);
        hud.enabled = true;
        bank.TrySpend(1);
        Assert.That(hud.IsFlashing, Is.True);
        yield return SceneManager.LoadSceneAsync("2DCardScene");
        yield return null;
        var loaded = Object.FindFirstObjectByType<AbilityPointFeedback>();
        Assert.That(loaded.IsFlashing || loaded.IsReadyPromptActive, Is.False);
        Assert.That(Object.FindFirstObjectByType<AbilityPoints>().CurrentPoints, Is.EqualTo(10));
        LogAssert.NoUnexpectedReceived();
    }
}

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator APFeedbackPresentationKeepsTheHUDReadableAndReadyClickableInBothDockPositions()
    {
        var bank = game.AbilityPoints;
        bank.TrySpend(bank.CurrentPoints - 10);
        yield return new WaitForSecondsRealtime(1.5f);
        var hud = Object.FindFirstObjectByType<AbilityPointFeedback>();
        var ready = ControlField<Button>(Object.FindFirstObjectByType<EarthquakeRoundController>(), "readyButton");
        var savedUI = UIIds(game);
        var dock = game.GetComponent<CardDock>();
        dock.SetExpanded(true, true);
        string directory = Path.Combine(Application.dataPath, "../Library/CardGameTestResults/APFeedback");
        Directory.CreateDirectory(directory);
        bank.TrySpend(4);
        yield return CaptureCanvas(Path.Combine(directory, "spend.png"), settleSeconds: .12f);
        yield return new WaitForSecondsRealtime(1.5f);
        bank.TrySpend(6);
        yield return new WaitForSecondsRealtime(1.5f);
        Drag(People()[0], research.Card.Rect.position);
        Assert.That(hud.IsWarning, Is.True);
        yield return CaptureCanvas(Path.Combine(directory, "insufficient.png"), settleSeconds: .13f);
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(hud.IsReadyPromptActive, Is.True);
        dock.SetExpanded(false, true);
        yield return CaptureCanvas(Path.Combine(directory, "ready-one-row.png"), settleSeconds: .2f,
            afterRender: camera => AssertTopButton(ready, camera));
        dock.SetExpanded(true, true);
        yield return CaptureCanvas(Path.Combine(directory, "ready-two-rows.png"), settleSeconds: .2f,
            afterRender: camera => AssertTopButton(ready, camera));
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
    }
}
