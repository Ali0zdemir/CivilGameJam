using System.Collections;
using CivilGameJam.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class MainMenuEarthquakeTests
{
    [UnityTest, Timeout(30000)]
    public IEnumerator EveryBuildingMovesDuringBothEarthquakeLoops()
    {
        yield return SceneManager.LoadSceneAsync("MainMenuBackup", LoadSceneMode.Single);
        yield return null;
        var anchor = Object.FindFirstObjectByType<MenuModelViewportAnchor>();
        Assert.That(anchor, Is.Not.Null);
        var model = anchor.transform.Find("Main_Menu");
        var animator = model.GetComponent<Animator>();
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip.isLooping, Is.True);

        var buildings = new Transform[6];
        var initial = new Vector3[6];
        var movement = new float[6, 2];
        for (int i = 0; i < buildings.Length; i++)
        {
            buildings[i] = model.Find($"Apartment{i + 1}(Default)(Seismic)");
            Assert.That(buildings[i], Is.Not.Null);
            initial[i] = buildings[i].localPosition;
        }
        Vector3 anchorPosition = anchor.transform.position;
        Vector3 modelScale = model.localScale;
        float started = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        float elapsed = 0f;
        float deadline = Time.realtimeSinceStartup + 15f;
        while (elapsed < 2.1f && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            elapsed = animator.GetCurrentAnimatorStateInfo(0).normalizedTime - started;
            int loop = Mathf.Min(Mathf.FloorToInt(elapsed), 1);
            for (int i = 0; i < buildings.Length; i++)
                movement[i, loop] = Mathf.Max(movement[i, loop], Vector3.Distance(initial[i], buildings[i].localPosition));
        }
        Assert.That(elapsed, Is.GreaterThanOrEqualTo(2.1f), "The animation stopped before completing two loops.");
        for (int i = 0; i < buildings.Length; i++)
            for (int loop = 0; loop < 2; loop++)
                Assert.That(movement[i, loop], Is.GreaterThan(0.001f), $"Building {i + 1} did not move in loop {loop + 1}.");
        Assert.That(anchor.transform.position, Is.EqualTo(anchorPosition));
        Assert.That(model.localScale, Is.EqualTo(modelScale));
    }
}
