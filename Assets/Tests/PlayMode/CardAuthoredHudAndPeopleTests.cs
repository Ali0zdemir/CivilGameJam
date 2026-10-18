using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class CardAbilityPointsPlayModeTests
{
    [UnityTest]
    public IEnumerator APCounterUpdatesOnlyItsVisibleNumberAndPreservesAuthoredWordingAndStyle()
    {
        var root = new GameObject("Authored AP format");
        root.SetActive(false);
        try
        {
            var labelObject = new GameObject("Balance", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(root.transform, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = Field<TMP_Text>(APHud, "balanceLabel").font;
            label.text = "Action points: <color=#38aa62>10</color> remaining";
            label.fontSize = 27.6f;
            label.enableAutoSizing = false;
            label.color = new Color(.2f, .5f, .1f);
            label.alignment = TextAlignmentOptions.Right;
            label.rectTransform.anchoredPosition = new Vector2(54f, -17f);
            label.rectTransform.sizeDelta = new Vector2(500f, 90f);
            var authoredPosition = label.rectTransform.anchoredPosition;
            var authoredSize = label.rectTransform.sizeDelta;
            var authoredColor = label.color;
            var customBank = root.AddComponent<AbilityPoints>();
            Set(customBank, "balanceLabel", label);
            root.SetActive(true);
            Assert.That(label.text, Is.EqualTo("Action points: <color=#38aa62>10</color> remaining"));
            Assert.That(customBank.TrySpend(4), Is.True);
            Assert.That(label.text, Is.EqualTo("Action points: <color=#38aa62>6</color> remaining"));
            customBank.AddPoints(2);
            yield return null;
            Assert.That(label.text, Is.EqualTo("Action points: <color=#38aa62>8</color> remaining"));
            Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(authoredPosition));
            Assert.That(label.rectTransform.sizeDelta, Is.EqualTo(authoredSize));
            Assert.That(label.color, Is.EqualTo(authoredColor));
            Assert.That(label.fontSize, Is.EqualTo(27.6f));
            Assert.That(label.enableAutoSizing, Is.False);
            Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Right));
        }
        finally { Object.Destroy(root); }
    }

    [UnityTest]
    public IEnumerator SavedAPAndReadyTypographyArtworkAndGeometrySurviveSpendingAndRefunds()
    {
        var counter = Field<RectTransform>(APHud, "counter");
        var balance = Field<TMP_Text>(APHud, "balanceLabel");
        var ready = Field<Button>(round, "readyButton");
        var readyLabel = ready.GetComponentInChildren<TMP_Text>(true);
        var rects = new[] { counter, balance.rectTransform, (RectTransform)ready.transform, readyLabel.rectTransform };
        var positions = rects.Select(rect => rect.anchoredPosition).ToArray();
        var sizes = rects.Select(rect => rect.sizeDelta).ToArray();
        var scales = rects.Select(rect => rect.localScale).ToArray();
        var labels = new[] { balance, readyLabel };
        var fonts = labels.Select(label => label.font).ToArray();
        var fontSizes = labels.Select(label => label.fontSize).ToArray();
        var autoSizing = labels.Select(label => label.enableAutoSizing).ToArray();
        var alignments = labels.Select(label => label.alignment).ToArray();
        var readyText = readyLabel.text;
        var apSprite = counter.GetComponent<Image>().sprite;
        var readySprite = ready.GetComponent<Image>().sprite;
        Assert.That(apSprite.name, Is.EqualTo("AP Ui"));
        Assert.That(readySprite.name, Is.EqualTo("Ready Button Ui"));
        Assert.That(bank.TrySpend(4), Is.True);
        bank.AddPoints(4);
        yield return new WaitForSecondsRealtime(1.6f);
        for (int i = 0; i < rects.Length; i++)
        {
            Assert.That(Vector2.Distance(rects[i].anchoredPosition, positions[i]), Is.LessThan(.01f));
            Assert.That(rects[i].sizeDelta, Is.EqualTo(sizes[i]));
            Assert.That(rects[i].localScale, Is.EqualTo(scales[i]));
        }
        for (int i = 0; i < labels.Length; i++)
        {
            Assert.That(labels[i].font, Is.SameAs(fonts[i]));
            Assert.That(labels[i].fontSize, Is.EqualTo(fontSizes[i]));
            Assert.That(labels[i].enableAutoSizing, Is.EqualTo(autoSizing[i]));
            Assert.That(labels[i].alignment, Is.EqualTo(alignments[i]));
        }
        Assert.That(readyLabel.text, Is.EqualTo(readyText));
        Assert.That(counter.GetComponent<Image>().sprite, Is.SameAs(apSprite));
        Assert.That(ready.GetComponent<Image>().sprite, Is.SameAs(readySprite));
    }
}

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator RevisedNormalArtworkFollowsPeopleThroughFoldersAndFreshTurns()
    {
        var revised = People()[0].transform.Find("Artwork").GetComponent<Image>().sprite;
        Assert.That(revised.name, Is.EqualTo("People Card Small Revised"));
        Assert.That(People().All(person => person.transform.Find("Artwork").GetComponent<Image>().sprite == revised), Is.True);
        foreach (var station in new[] { school, research, Station(CardKind.Factory) })
        {
            var worker = People().First(person => !person.IsStored);
            var slot = station.GetComponentsInChildren<WorkerSlot>(true).OrderBy(item => item.Index).First();
            Assert.That(station.TryAssign(worker), Is.True);
            Assert.That(slot.EntryImage.sprite, Is.SameAs(revised));
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(slot.Portrait.sprite, Is.SameAs(revised));
            Assert.That(station.ReleaseWorker(0), Is.SameAs(worker));
            Assert.That(slot.EntryImage.sprite, Is.SameAs(revised));
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(worker.IsStored, Is.False);
        }
        game.ResetTurnPeople();
        yield return null;
        Assert.That(People().Length, Is.EqualTo(4));
        Assert.That(People().All(person => person.transform.Find("Artwork").GetComponent<Image>().sprite == revised), Is.True);
        var directory = Path.Combine(Application.dataPath, "../Library/CardGameTestResults/RevisedPeopleHud");
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "authored-hud-revised-people.png"));
    }
}
