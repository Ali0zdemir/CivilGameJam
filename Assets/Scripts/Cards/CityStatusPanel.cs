using TMPro;
using UnityEngine;

namespace CivilGameJam.Cards
{
    public sealed class CityStatusPanel : MonoBehaviour
    {
        [SerializeField] private CardGameController game;
        [SerializeField] private TextMeshProUGUI normalCount;
        [SerializeField] private TextMeshProUGUI educatedCount;
        [SerializeField] private TextMeshProUGUI populationCount;
        [SerializeField] private TextMeshProUGUI housesCount;
        [SerializeField] private TextMeshProUGUI researchCount;
        [SerializeField] private TextMeshProUGUI schoolCount;
        [SerializeField] private TextMeshProUGUI factoryCount;
        [SerializeField] private TextMeshProUGUI schoolStatus;
        [SerializeField] private TextMeshProUGUI researchStatus;
        [SerializeField] private TextMeshProUGUI factoryStatus;
        [SerializeField] private BuildingStation school;
        [SerializeField] private BuildingStation research;
        [SerializeField] private BuildingStation factory;
        [SerializeField] private FactoryProduction production;

        private void Update() => Refresh();

        public void Refresh()
        {
            if (game == null) return;
            Set(normalCount, game.NormalPeopleCount.ToString());
            Set(educatedCount, game.EducatedPeopleCount.ToString());
            Set(populationCount, (game.NormalPeopleCount + game.EducatedPeopleCount).ToString());
            int houses = 0, schools = 0, factories = 0, centers = 0;
            foreach (var card in game.Cards)
                switch (card.Kind)
                {
                    case CardKind.House: houses++; break;
                    case CardKind.School: schools++; break;
                    case CardKind.Factory: factories++; break;
                    case CardKind.ResearchCenter: centers++; break;
                }
            Set(housesCount, houses.ToString()); Set(schoolCount, schools.ToString());
            Set(factoryCount, factories.ToString()); Set(researchCount, centers.ToString());
            if (school != null) Set(schoolStatus, school.WorkerCount == 0 ? "Waiting for students" : $"{school.WorkerCount} / 2 students learning");
            if (research != null) Set(researchStatus, research.AwaitingBlueprint ? "Choose a blueprint" :
                research.WorkerCount == 0 ? "Waiting for researchers" : "Researching");
            if (production != null && factory != null) Set(factoryStatus, production.PendingBlueprint == null ? "Waiting for a blueprint" :
                factory.WorkerCount == 0 ? "A worker is required" : "Producing");
        }

        private static void Set(TextMeshProUGUI label, string value)
        { if (label != null && label.text != value) label.text = value; }
    }
}
