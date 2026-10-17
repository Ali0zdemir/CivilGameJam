using System.Collections;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator SchoolResearchAndFactoryGiveOneSubtleSettlingMotionWhenTheyDeliverTheirCards()
    {
        var schoolFeedback = school.GetComponent<CardFeedback>();
        Assert.That(school.TryAssign(People()[0]), Is.True);
        school.AdvanceWork(game.EducationSeconds * .5f);
        Assert.That(schoolFeedback.IsWorkCompleting, Is.False);
        school.AdvanceWork(game.EducationSeconds);
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
        yield return AssertSubtleWorkCompletion(school.Card);

        var researchFeedback = research.GetComponent<CardFeedback>();
        Assert.That(research.TryAssign(Educated()), Is.True);
        research.AdvanceWork(game.EducatedResearchSeconds * .5f);
        Assert.That(researchFeedback.IsWorkCompleting, Is.False);
        research.AdvanceWork(game.EducatedResearchSeconds);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        Assert.That(researchFeedback.IsWorkCompleting, Is.False, "The reveal motion belongs to the actual blueprint handoff.");
        game.SelectBlueprint(0);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.True);
        yield return AssertSubtleWorkCompletion(research.Card);

        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>();
        factory.enabled = false;
        var plan = ResearchPlan(0);
        Assert.That(station.TryAssign(People().First(person => person.CanStartWork && !person.IsStored)), Is.True);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        factory.AdvanceWork(factory.ProductionSeconds * .5f);
        Assert.That(station.GetComponent<CardFeedback>().IsWorkCompleting, Is.False);
        factory.AdvanceWork(factory.ProductionSeconds);
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.EqualTo(2));
        yield return AssertSubtleWorkCompletion(station.Card);
        factory.AdvanceWork(100f);
        Assert.That(station.GetComponent<CardFeedback>().IsWorkCompleting, Is.False, "An idle completed order must not keep shaking.");
    }

    private IEnumerator AssertSubtleWorkCompletion(DraggableCard card)
    {
        var feedback = card.GetComponent<CardFeedback>();
        var position = card.Rect.position;
        var size = card.Rect.sizeDelta;
        var scale = card.LayoutScale;
        var rotation = card.Rect.localRotation;
        var children = card.GetComponentsInChildren<RectTransform>(true).Select(child => child.GetInstanceID()).OrderBy(id => id).ToArray();
        Assert.That(feedback.IsWorkCompleting, Is.True, card.name);
        bool moved = false;
        float deadline = Time.realtimeSinceStartup + 1f;
        while (feedback.IsWorkCompleting && Time.realtimeSinceStartup < deadline)
        {
            float angle = Quaternion.Angle(card.Rect.localRotation, rotation);
            if (angle > .025f) moved = true;
            Assert.That(angle, Is.LessThan(.7f), "Completion feedback must remain subtle.");
            Assert.That(Vector3.Distance(card.Rect.position, position), Is.LessThan(.01f), "The card must stay centered in its slot.");
            Assert.That(card.Rect.sizeDelta, Is.EqualTo(size));
            Assert.That(Vector3.Distance(card.Rect.localScale, scale), Is.LessThan(.001f));
            yield return null;
        }
        Assert.That(moved, Is.True, card.name + " must show the completion motion.");
        Assert.That(feedback.IsWorkCompleting, Is.False);
        Assert.That(Quaternion.Angle(card.Rect.localRotation, rotation), Is.LessThan(.01f));
        Assert.That(card.GetComponentsInChildren<RectTransform>(true).Select(child => child.GetInstanceID()).OrderBy(id => id), Is.EqualTo(children));
    }

    [UnityTest]
    public IEnumerator CompletionMotionUsesUnscaledTimeAndYieldsCleanlyToDragOrDisable()
    {
        var card = research.Card;
        var feedback = card.GetComponent<CardFeedback>();
        var rotation = card.Rect.localRotation;
        float originalTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            feedback.PlayWorkComplete();
            yield return AssertSubtleWorkCompletion(card);
            feedback.PlayWorkComplete();
            yield return new WaitForSecondsRealtime(.04f);
            var pointer = BeginGridDrag(card);
            Assert.That(feedback.IsWorkCompleting, Is.False);
            feedback.PlayWorkComplete();
            Assert.That(feedback.IsWorkCompleting, Is.False, "A delivery must not add a shake while the player holds the card.");
            EndGridDrag(card, pointer);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Quaternion.Angle(card.Rect.localRotation, rotation), Is.LessThan(.01f));
            feedback.PlayWorkComplete();
            yield return new WaitForSecondsRealtime(.04f);
            feedback.enabled = false;
            Assert.That(feedback.IsWorkCompleting, Is.False);
            Assert.That(Quaternion.Angle(card.Rect.localRotation, rotation), Is.LessThan(.01f));
            feedback.enabled = true;
        }
        finally { Time.timeScale = originalTimeScale; }
    }
}
