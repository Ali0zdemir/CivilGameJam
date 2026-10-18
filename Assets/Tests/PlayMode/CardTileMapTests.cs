using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator MonsterTileGroundAndFoliageStayFixedThroughInstalledUpgradesAndTheFullSequence()
    {
        var rig = new SequencePreview();
        var ground = rig.Tsunami.GroundSurface;
        var cells = ground.Find("Tile_Grid_4x2").Cast<Transform>().ToArray();
        var positions = cells.Select(cell => cell.position).ToArray();
        var groundMatrix = ground.localToWorldMatrix;
        var groundMesh = ground.GetComponent<MeshFilter>().sharedMesh;
        var pads = rig.Tsunami.BuildingsRoot.Cast<Transform>().Where(building => building.gameObject.activeSelf).SelectMany(TravellingGrounds).ToArray();
        var meshes = pads.Select(filter => filter.sharedMesh).ToArray();
        var padPositions = pads.Select(filter => filter.transform.position).ToArray();
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TileMapWork/Previews"));
        Directory.CreateDirectory(directory);
        CaptureCityWorldFrame(Path.Combine(directory, "01-tile-map.png"));
        var house = CityHouseLinks()[0];
        Assert.That(house.House.TryInstall(ProduceCityProducts(UpgradeKind.RailSystem)[0]), Is.True);
        Assert.That(house.House.TryInstall(ProduceCityProducts(UpgradeKind.Breakwater)[0]), Is.True);
        Assert.That(house.House.TryInstall(ProduceCityProducts(UpgradeKind.Fire)[0]), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        CaptureCityWorldFrame(Path.Combine(directory, "02-upgraded-tile-map.png"));
        rig.Sequence.SelectSequence(3); rig.Sequence.StartSelectedSequence();
        rig.For(rig.Monster.RiseDuration * .6f);
        CaptureCityWorldFrame(Path.Combine(directory, "03-tile-map-tsunami.png"));
        rig.Until(() => rig.Sequence.Stage == MonsterSequenceStage.Fire);
        rig.For(rig.Breath.FlameStartTime + .8f);
        CaptureCityWorldFrame(Path.Combine(directory, "04-tile-map-fire.png"));
        rig.Until(() => !rig.Sequence.IsRunning);
        Assert.That(ground.localToWorldMatrix, Is.EqualTo(groundMatrix));
        Assert.That(ground.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(groundMesh));
        Assert.That(cells.Select(cell => cell.position), Is.EqualTo(positions));
        Assert.That(pads.Select(filter => filter.sharedMesh), Is.EqualTo(meshes));
        Assert.That(pads.Select(filter => filter.transform.position), Is.EqualTo(padPositions), "Building grounds remain seated while the apartment bodies shake, collapse or change upgrade variants.");
        Assert.That(pads.All(filter => filter.gameObject.activeInHierarchy && filter.GetComponent<MeshRenderer>().enabled), Is.True);
        Assert.That(house.RailSystem.IsInstalled && house.FloodProtection.IsInstalled && house.FireProtection.IsInstalled, Is.True);
        Assert.That(Object.FindFirstObjectByType<MonsterSequenceResults>().IsVisible, Is.True);
        rig.Sequence.StopSequence();
        Assert.That(ground.localToWorldMatrix, Is.EqualTo(groundMatrix));
    }
}
