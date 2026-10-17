using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CardPixellariEditModeTests
{
    [Test]
    public void EverySavedGameLabelAndTheDefaultUsePixellariWithNoMissingGlyphs()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Pixellari SDF.asset");
        Assert.That(TMP_Settings.defaultFontAsset, Is.SameAs(font));
        Assert.That(font.sourceFontFile, Is.Not.Null, "Builds need the source for changing status labels.");
        var checkedLabels = 0;
        var paths = AssetDatabase.FindAssets("t:Prefab t:Scene", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !path.StartsWith("Assets/TextMesh Pro/", StringComparison.Ordinal));
        foreach (string path in paths)
        {
            if (path.EndsWith(".prefab", StringComparison.Ordinal))
                Check(AssetDatabase.LoadAssetAtPath<GameObject>(path), font, ref checkedLabels);
            else if (path.EndsWith(".unity", StringComparison.Ordinal))
                foreach (var root in EditorSceneManager.OpenScene(path).GetRootGameObjects()) Check(root, font, ref checkedLabels);
        }
        Assert.That(checkedLabels, Is.GreaterThan(100), "Include inactive popups, blueprints, and city test panels.");
    }

    private static void Check(GameObject root, TMP_FontAsset font, ref int count)
    {
        Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Text>(true), Is.Empty, root.name);
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(label.font, Is.SameAs(font), root.name + "/" + label.name);
            Assert.That(label.fontSharedMaterial.mainTexture, Is.SameAs(font.atlasTexture), label.name + " has an outdated font material.");
            string printable = new string(label.text.Where(character => !char.IsControl(character)).ToArray());
            Assert.That(font.HasCharacters(printable, out List<char> missing), Is.True,
                label.name + " is missing: " + (missing == null ? "" : new string(missing.ToArray())));
            count++;
        }
    }
}
