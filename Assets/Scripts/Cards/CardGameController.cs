using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    public sealed class CardGameController : MonoBehaviour
    {
        [Serializable]
        public sealed class BlueprintOption
        {
            public string title;
            public GameObject prefab;
            public Button button;
            public GameObject productPrefab;
            [Min(1)] public int productCount = 2;
        }

        [Header("Scene")]
        [SerializeField] private RectTransform table;
        [SerializeField] private RectTransform gridPanel;
        [SerializeField] private RectTransform cardLayer;
        [SerializeField] private RectTransform factoryOutputArea;
        [SerializeField] private CardGrid grid;
        [SerializeField] private TextMeshProUGUI population;
        [SerializeField] private TextMeshProUGUI hint;
        [SerializeField] private RectTransform choicePanel;
        [SerializeField] private BlueprintVault vault;
        [SerializeField] private CardSidebar sidebar;

        [Header("Gameplay rewards")]
        [SerializeField] private GameObject educatedPersonPrefab;
        [SerializeField] private BlueprintOption[] blueprints;

        [Header("Time in seconds (one worker)")]
        [Min(0.1f)] [SerializeField] private float educationSeconds = 25f;
        [Min(0.1f)] [SerializeField] private float normalResearchSeconds = 25f;
        [Min(0.1f)] [SerializeField] private float educatedResearchSeconds = 15f;

        private readonly List<DraggableCard> cards = new List<DraggableCard>();
        private Canvas canvas;
        private BuildingStation choosingStation;
        private float messageUntil;
        private string defaultHint;
        private DraggableCard draggedCard;
        private CardHover inspectedCard;

        public IReadOnlyList<DraggableCard> Cards => cards;
        public RectTransform CardLayer => cardLayer;
        public RectTransform FactoryOutputArea => factoryOutputArea;
        public CardGrid Grid => grid;
        public BlueprintVault Vault => vault;
        public CardSidebar Sidebar => sidebar;
        public bool IsChoosingBlueprint => choosingStation != null;
        public bool IsDraggingAnyCard => draggedCard != null;
        public DraggableCard DraggedCard => draggedCard;
        public bool HasActiveWorkerEntry
        {
            get
            {
                foreach (var card in cards) if (card != null && card.Station != null && card.Station.IsReceivingWorker) return true;
                return false;
            }
        }
        public float EducationSeconds => Mathf.Max(0.1f, educationSeconds);
        public float NormalResearchSeconds => Mathf.Max(0.1f, normalResearchSeconds);
        public float EducatedResearchSeconds => Mathf.Max(0.1f, educatedResearchSeconds);
        public float CanvasScale => canvas.scaleFactor;
        public int NormalPeopleCount => CountPeople(CardKind.Person);
        public int EducatedPeopleCount => CountPeople(CardKind.EducatedPerson);

        private void Start()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            canvas = table.GetComponentInParent<Canvas>();
            defaultHint = hint != null ? hint.text : string.Empty;
            choicePanel.gameObject.SetActive(false);
            sidebar.RefreshLayout();
            Canvas.ForceUpdateCanvases();
            cards.Clear();
            vault.Initialize(this);
            // The entire starting board already exists in the saved scene.
            foreach (var card in table.GetComponentsInChildren<DraggableCard>())
            {
                card.Initialize(this);
                cards.Add(card);
                if (!card.IsBuilding) continue;
                int cell = card.InitialGridCell >= 0 ? card.InitialGridCell : Grid.FindNearestAvailable(card, card.Rect.position);
                if (!Grid.TryPlace(card, cell)) Grid.SnapToNearest(card);
            }
            RefreshPopulation();
        }

        private bool ValidateSetup()
        {
            if (table == null || gridPanel == null || CardLayer == null || Grid == null ||
                choicePanel == null || vault == null || sidebar == null ||
                table.GetComponentInParent<Canvas>() == null || educatedPersonPrefab == null ||
                blueprints == null || blueprints.Length == 0)
            {
                Debug.LogError("CardGameController: scene or card prefab references are missing.", this);
                return false;
            }
            foreach (var option in blueprints)
                if (option == null || option.prefab == null || option.button == null || option.productPrefab == null)
                {
                    Debug.LogError("CardGameController: a blueprint prefab is missing.", this);
                    return false;
                }
            return true;
        }

        public void ShowInspection(CardHover inspection)
        {
            if (IsDraggingAnyCard || IsChoosingBlueprint) return;
            if (inspectedCard == inspection) return;
            CloseInspection();
            inspectedCard = inspection;
            inspection.OpenInspection();
        }

        public void CloseInspection(CardHover inspection)
        {
            if (inspectedCard == inspection) CloseInspection();
            else inspection.CancelHover();
        }

        public void CloseInspection()
        {
            if (inspectedCard != null) inspectedCard.CancelHover();
            inspectedCard = null;
        }

        public void BeginCardDrag(DraggableCard card) { CloseInspection(); draggedCard = card; }
        public void EndCardDrag(DraggableCard card) { if (draggedCard == card) draggedCard = null; }

        public BlueprintOption RecipeFor(UpgradeKind kind)
        {
            foreach (var recipe in blueprints)
                if (recipe.prefab.GetComponent<DraggableCard>().UpgradeType == kind) return recipe;
            return null;
        }

        public void ProduceBlueprint(DraggableCard blueprint, Vector3 origin)
        {
            var recipe = RecipeFor(blueprint.UpgradeType);
            if (recipe == null) return;
            cards.Remove(blueprint);
            Destroy(blueprint.gameObject);
            for (int i = 0; i < Mathf.Max(1, recipe.productCount); i++)
            {
                var product = Spawn(recipe.productPrefab);
                product.MarkAsNewProduct();
                ReturnToTable(product, origin);
            }
            ShowMessage($"Production complete: {recipe.productCount} {recipe.title} items. Install them on houses.");
        }

        private DraggableCard Spawn(GameObject prefab)
        {
            var instance = Instantiate(prefab, CardLayer);
            instance.name = prefab.name;
            var card = instance.GetComponent<DraggableCard>();
            card.Initialize(this);
            cards.Add(card);
            return card;
        }

        private void SizeCard(DraggableCard card)
        {
            // Keep prefab artwork proportions; building and person prefabs have different native sizes.
            if (card.IsBuilding)
            {
                var size = card.Rect.sizeDelta;
                float scale = Mathf.Min((Grid.CellSize.x - 36f) / size.x, (Grid.CellSize.y - 34f) / size.y, 1f);
                card.SetLayoutScale(Vector3.one * Mathf.Max(0.1f, scale));
            }
            else
            {
                float scale = 150f / card.Rect.sizeDelta.y;
                card.SetLayoutScale(Vector3.one * scale);
            }
        }

        public void FinishDrop(DraggableCard card, Vector2 screenPosition, Camera eventCamera, Vector3 dragStart)
        {
            ClearPreview();
            if (IsChoosingBlueprint)
            {
                if (card.IsBuilding) Grid.SnapToNearest(card);
                else RestoreRejectedDrop(card, dragStart);
                return;
            }
            if (card.IsBuilding)
            {
                Grid.SnapToNearest(card);
                return;
            }

            if (card.IsPerson)
            {
                var target = BuildingAt(screenPosition, eventCamera);
                if (target != null)
                {
                    if (target.Station != null && target.Station.TryAssign(card)) return;
                    RestoreRejectedDrop(card, dragStart);
                    ShowMessage(target.Station == null ? "This building does not accept workers." :
                        target.Kind == CardKind.School && card.Kind == CardKind.EducatedPerson
                            ? "This person is already educated." : "This building holds up to 2 workers.");
                    return;
                }
            }
            else if (card.Kind == CardKind.Blueprint || card.Kind == CardKind.Upgrade)
            {
                var target = BuildingAt(screenPosition, eventCamera);
                if (target != null)
                {
                    var factory = target.GetComponent<FactoryProduction>();
                    var house = target.GetComponent<HouseUpgrades>();
                    if (card.Kind == CardKind.Blueprint && factory != null && factory.TryInsertBlueprint(card)) return;
                    if (card.Kind == CardKind.Upgrade && house != null && house.TryInstall(card)) return;
                    if (card.Kind == CardKind.Blueprint)
                    {
                        vault.Store(card, card.Rect.position);
                        ShowMessage("Blueprint returned to the Vault. Drag it to the factory to produce items.");
                        return;
                    }
                    RestoreRejectedDrop(card, dragStart);
                    ShowMessage("Install this item in the matching empty house slot.");
                    return;
                }
            }
            if (card.Kind == CardKind.Blueprint && vault.Store(card, card.Rect.position)) return;
            if (sidebar.TrySnap(card, card.Rect.position)) return;
            ClampToTable(card);
        }

        private void RestoreRejectedDrop(DraggableCard card, Vector3 destination)
        {
            Vector3 droppedPosition = card.Rect.position;
            card.Rect.position = destination;
            if (!sidebar.TrySnap(card, destination) && !(card.IsPerson && sidebar.TryPark(card))) ClampToTable(card);
            card.FlyFrom(droppedPosition, card.Rect.position);
        }

        public void PreviewDrop(DraggableCard card, Vector2 screenPosition, Camera eventCamera)
        {
            ClearPreview();
            if (card.IsBuilding) Grid.SetPreview(Grid.FindNearestAvailable(card, card.Rect.position));
            else if (card.IsPerson) BuildingAt(screenPosition, eventCamera)?.Station?.PreviewAcceptance(card);
            else if (card.Kind == CardKind.Blueprint) BuildingAt(screenPosition, eventCamera)?.GetComponent<FactoryProduction>()?.Preview(card);
            else if (card.Kind == CardKind.Upgrade) BuildingAt(screenPosition, eventCamera)?.GetComponent<HouseUpgrades>()?.Preview(card);
        }

        private DraggableCard BuildingAt(Vector2 screenPosition, Camera eventCamera)
        {
            DraggableCard best = null;
            float nearest = float.PositiveInfinity;
            foreach (var card in cards)
            {
                if (!card.IsBuilding || !card.gameObject.activeSelf || card.IsDragging ||
                    !RectTransformUtility.RectangleContainsScreenPoint(card.Rect, screenPosition, eventCamera)) continue;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(eventCamera, card.Rect.TransformPoint(card.Rect.rect.center));
                float distance = (screenPosition - center).sqrMagnitude;
                if (distance < nearest) { best = card; nearest = distance; }
            }
            return best;
        }

        private void ClearPreview()
        {
            Grid.SetPreview(-1);
            foreach (var card in cards) card.Station?.PreviewAcceptance(null);
            foreach (var card in cards)
            {
                card.GetComponent<FactoryProduction>()?.Preview(null);
                card.GetComponent<HouseUpgrades>()?.Preview(null);
            }
        }

        public void ClampToTable(DraggableCard card)
        {
            if (sidebar.Owns(card) && !card.IsDragging) return;
            Vector2 half = Vector2.Scale(card.Rect.rect.size, card.LayoutScale) * 0.5f;
            Rect area = CardLayer.rect;
            if (card.IsParkedInFactoryOutput && !card.IsDragging && factoryOutputArea != null)
                area = BoundsInCardLayer(factoryOutputArea);
            if (card.IsDragging)
            {
                var root = (RectTransform)canvas.rootCanvas.transform;
                Vector2 min = CardLayer.InverseTransformPoint(root.TransformPoint(root.rect.min));
                Vector2 max = CardLayer.InverseTransformPoint(root.TransformPoint(root.rect.max));
                area = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            Vector2 point = card.TablePosition;
            point.x = Mathf.Clamp(point.x, area.xMin + half.x + 5f, area.xMax - half.x - 5f);
            point.y = Mathf.Clamp(point.y, area.yMin + half.y + 5f, area.yMax - half.y - 5f);
            card.SetTablePosition(point);
            if (!card.IsDragging && card.Kind != CardKind.Blueprint && vault.OverlapsTableArea(point, half * 2f))
                card.SetTablePosition(FindFreePosition(card));
        }

        public void ReturnToTable(DraggableCard worker, Vector3 origin, bool animate = true)
        {
            if (worker.Kind == CardKind.Blueprint && vault.Store(worker, animate ? origin : (Vector3?)null)) return;
            worker.StopFlight();
            worker.gameObject.SetActive(true);
            worker.Rect.SetParent(CardLayer, true);
            worker.IsParkedInFactoryOutput = worker.Kind == CardKind.Upgrade && factoryOutputArea != null;
            if (!sidebar.TryPark(worker)) worker.Rect.anchoredPosition = FindFreePosition(worker);
            worker.Rect.SetAsLastSibling();
            Vector3 destination = worker.Rect.position;
            if (animate) worker.FlyFrom(origin, destination);
            RefreshPopulation();
        }

        public void Graduate(DraggableCard student, Vector3 schoolPosition)
        {
            cards.Remove(student);
            Destroy(student.gameObject);
            var educated = Spawn(educatedPersonPrefab);
            sidebar.TransferRememberedPosition(student, educated);
            ReturnToTable(educated, schoolPosition);
            ShowMessage("Education complete: 1 person became educated.");
        }

        private Vector2 FindFreePosition(DraggableCard incoming)
        {
            bool output = incoming.Kind == CardKind.Upgrade && factoryOutputArea != null;
            Rect area = output ? BoundsInCardLayer(factoryOutputArea) : GetLooseCardArea();
            float topInset = output ? 18f : 115f;
            Vector2 incomingSize = Vector2.Scale(incoming.Rect.rect.size, incoming.LayoutScale);
            float width = incomingSize.x;
            float height = incomingSize.y;
            int columns = Mathf.Max(1, Mathf.FloorToInt((area.width - 36f + 12f) / (width + 12f)));
            int rows = Mathf.Max(1, Mathf.FloorToInt((area.height - topInset - 18f + 14f) / (height + 14f)));
            Vector2 fallback = Vector2.zero;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    var candidate = new Vector2(area.xMin + 18f + width / 2f + col * (width + 12f),
                        area.yMax - topInset - height / 2f - row * (height + 14f));
                    fallback = candidate;
                    if (HasRoom(incoming, incomingSize, candidate)) return candidate;
                }
            }
            // Also use gaps between authored rows when their spacing differs from loose-card spacing.
            foreach (var edge in cards)
            {
                if (edge == incoming || edge.IsBuilding || edge.IsStored || !edge.gameObject.activeSelf) continue;
                float y = edge.TablePosition.y - (edge.Rect.rect.height * edge.LayoutScale.y + height) * 0.5f - 12f;
                if (y - height / 2f < area.yMin + 18f || y + height / 2f > area.yMax - topInset) continue;
                for (int col = 0; col < columns; col++)
                {
                    var candidate = new Vector2(area.xMin + 18f + width / 2f + col * (width + 12f), y);
                    if (HasRoom(incoming, incomingSize, candidate)) return candidate;
                }
            }
            // A full tray becomes a small fan; cards can still be dragged out individually.
            var fan = fallback + new Vector2((cards.Count % 4) * 10f, (cards.Count % 4) * 8f);
            if (vault.OverlapsTableArea(fan, incomingSize))
                fan = new Vector2(area.xMin + 18f + width / 2f + (cards.Count % 4) * 10f, area.yMax - topInset - height / 2f);
            fan.x = Mathf.Clamp(fan.x, area.xMin + width / 2f, area.xMax - width / 2f);
            fan.y = Mathf.Clamp(fan.y, area.yMin + height / 2f, area.yMax - height / 2f);
            return fan;
        }

        private Rect BoundsInCardLayer(RectTransform area)
        {
            var corners = new Vector3[4];
            area.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners)
            {
                Vector2 point = CardLayer.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private bool HasRoom(DraggableCard incoming, Vector2 size, Vector2 position)
        {
            if (vault.OverlapsTableArea(position, size)) return false;
            foreach (var other in cards)
            {
                if (other == incoming || other.IsBuilding || other.IsStored || !other.gameObject.activeSelf) continue;
                Vector2 delta = other.TablePosition - position;
                Vector2 separation = (size + Vector2.Scale(other.Rect.rect.size, other.LayoutScale)) * 0.5f;
                if (Mathf.Abs(delta.x) < separation.x + 6f && Mathf.Abs(delta.y) < separation.y + 6f) return false;
            }
            return true;
        }

        public void RefreshPopulation()
        {
            if (population != null) population.text = $"People: {NormalPeopleCount}    Educated: {EducatedPeopleCount}";
        }

        private int CountPeople(CardKind kind)
        {
            int count = 0;
            foreach (var card in cards) if (card != null && card.Kind == kind) count++;
            return count;
        }

        public void ShowBlueprintChoice(BuildingStation station)
        {
            if (IsChoosingBlueprint) return;
            CloseInspection();
            choosingStation = station;
            ClearPreview();
            choicePanel.SetAsLastSibling();
            choicePanel.gameObject.SetActive(true);
        }

        public void SelectBlueprint(int index)
        {
            if (!IsChoosingBlueprint || index < 0 || index >= blueprints.Length) return;
            var station = choosingStation;
            choosingStation = null;
            choicePanel.gameObject.SetActive(false);
            station.FinishResearch();
            var blueprint = Spawn(blueprints[index].prefab);
            vault.Store(blueprint, station.Card.Rect.position);
            ShowMessage(blueprints[index].title + " discovered. Blueprint added to the Vault.");
        }

        private void ShowMessage(string message)
        {
            if (hint == null) return;
            hint.text = message;
            messageUntil = Time.unscaledTime + 4f;
        }

        private void LateUpdate()
        {
            if (hint == null) return;
            if (messageUntil > 0f && Time.unscaledTime > messageUntil)
            {
                messageUntil = 0f;
                hint.text = defaultHint;
            }
        }

        [ContextMenu("Fit scene cards to grid")]
        public void FitSceneCardsToGrid()
        {
            if (table == null || Grid == null || gridPanel == null) return;
            foreach (var card in table.GetComponentsInChildren<DraggableCard>()) SizeCard(card);
        }

        private Rect GetLooseCardArea()
        {
            var corners = new Vector3[4];
            gridPanel.GetWorldCorners(corners);
            float left = CardLayer.InverseTransformPoint(corners[0]).x;
            float right = CardLayer.InverseTransformPoint(corners[3]).x;
            Rect area = CardLayer.rect;
            return area.xMax - right >= left - area.xMin
                ? Rect.MinMaxRect(right, area.yMin, area.xMax, area.yMax)
                : Rect.MinMaxRect(area.xMin, area.yMin, left, area.yMax);
        }
    }
}
