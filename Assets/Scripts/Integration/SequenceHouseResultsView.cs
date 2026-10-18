using System;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using TMPro;
using UnityEngine;

namespace CivilGameJam
{
    /// <summary>Copies each real house's completed-turn state into the authored result cards.</summary>
    [DisallowMultipleComponent]
    public sealed class SequenceHouseResultsView : MonoBehaviour
    {
        [Serializable]
        private sealed class HouseCard
        {
            public HouseCityUpgradeLink source;
            public UpgradeIcon[] icons;
            public TMP_Text title;
            public TMP_Text report;
            public GameObject destroyedMarker;
        }

        [SerializeField] private MonsterSequenceResults results;
        [SerializeField] private MonsterSequenceController sequence;
        [SerializeField] private HouseCard[] cards;

        private void OnEnable() { if (results != null) results.Showing += Capture; }
        private void OnDisable() { if (results != null) results.Showing -= Capture; }

        private void Capture()
        {
            if (cards == null || sequence == null) return;
            int stage = sequence.ActiveSequence > 0 ? sequence.ActiveSequence : sequence.SelectedSequence;
            for (int i = 0; i < cards.Length; i++)
            {
                var view = cards[i];
                if (view.source == null || view.source.House == null) continue;
                var house = view.source.House;
                bool seismic = house.GetInstalled(UpgradeKind.RailSystem) != null;
                bool elevator = house.GetInstalled(UpgradeKind.Breakwater) != null;
                bool fire = house.GetInstalled(UpgradeKind.Fire) != null;
                if (view.icons != null)
                    foreach (var icon in view.icons)
                        if (icon != null) icon.gameObject.SetActive(house.GetInstalled(icon.Kind) != null);
                var building = view.source.Building;
                bool destroyed = house.IsDestroyed || results.IsHouseDestroyed(building != null ? building.GetComponent<BuildingModelGeometry>() : null);
                if (view.destroyedMarker != null) view.destroyedMarker.SetActive(destroyed);
                if (view.title != null) view.title.text = "HOUSE " + (i + 1);
                if (view.report == null) continue;
                string hazard = stage == 3 ? "FIRE" : stage == 2 ? "TSUNAMI" : "EARTHQUAKE";
                string protection = stage == 3 ? "Sprinklers" : stage == 2 ? "Elevated Floor" : "Seismic isolator";
                bool active = stage == 3 ? fire : stage == 2 ? elevator : seismic;
                string outcome = active ? "House protected." : "Survived; protection missing.";
                if (destroyed)
                {
                    bool quake = building != null && building.TryGetComponent<ApartmentEarthquakeAnimation>(out var earthquake) && earthquake.HasCollapsed;
                    bool flood = building != null && building.TryGetComponent<ApartmentTsunamiCollapse>(out var tsunami) && tsunami.HasCollapsed;
                    bool burnt = building != null && sequence.WasHouseBurnt(building);
                    outcome = quake ? seismic ? "Destroyed by the earthquake." : "Collapsed: no seismic isolator." :
                        flood ? elevator ? "Destroyed by the tsunami." : "Flooded: no elevated floor." :
                        burnt ? fire ? "Destroyed by fire." : "Burned: no sprinklers." :
                        !active ? stage == 3 ? "Burned: no sprinklers." : stage == 2 ? "Flooded: no elevated floor." : "Collapsed: no seismic isolator." :
                        "Destroyed during the sequence.";
                }
                view.report.text = hazard + "\n" + protection + ": " + (active ? "ACTIVE" : "MISSING") + "\n" + outcome;
            }
        }
    }
}
