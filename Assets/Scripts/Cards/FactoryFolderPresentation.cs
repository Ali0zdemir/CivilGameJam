using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    // Every graphic is authored in FactoryCard. Gameplay only animates those saved objects.
    public sealed class FactoryFolderPresentation : MonoBehaviour
    {
        [SerializeField] private FactoryProduction factory;
        [SerializeField] private BuildingStation station;
        [Header("Saved blueprint pocket")]
        [SerializeField] private RectTransform pocket;
        [SerializeField] private Image storedBlueprint;
        [SerializeField] private Image entryImage;
        [SerializeField] private Canvas entryCanvas;
        [SerializeField] private RectTransform entryMouth;
        [Min(.1f)] [SerializeField] private float entrySeconds = .5f;
        [Header("Workers needed: no text")]
        [SerializeField] private CanvasGroup workerHint;
        [SerializeField] private RectTransform personHint;
        [SerializeField] private RectTransform arrowHint;
        [Min(.01f)] [SerializeField] private float hintFadeSeconds = .18f;
        [Min(0f)] [SerializeField] private float hintTravel = 5f;
        [Min(.1f)] [SerializeField] private float hintCycleSeconds = 1.4f;
        private CardGameController game;
        private DraggableCard card;
        private Coroutine entry;
        private Coroutine exit;
        private DraggableCard returningBlueprint;
        private float entryProgress;
        private Vector2 arrowRest;
        private Vector3 personScale;
        private bool hintPoseSaved;
        private float hintTime;
        private DraggableCard shownBlueprint;

        public bool IsEntering => entry != null;
        public bool IsReturning => exit != null;
        public bool IsAnimating => IsEntering || IsReturning;
        internal bool CanReturn => isActiveAndEnabled && card != null && entryImage != null && storedBlueprint != null &&
            pocket != null && shownBlueprint != null && !IsReturning;
        public bool IsWorkerHintVisible => workerHint != null && workerHint.gameObject.activeSelf && workerHint.alpha > .05f;
        public Image EntryImage => entryImage;
        public Image StoredBlueprint => storedBlueprint;

        public void Initialize(CardGameController owner, DraggableCard building)
        {
            game = owner;
            card = building;
            SaveHintPose();
            ClearBlueprint();
        }

        public void Insert(DraggableCard blueprint)
        {
            ClearBlueprint();
            if (entryImage == null || storedBlueprint == null || pocket == null || card == null) return;
            var artwork = blueprint.transform.Find("CardSymbol")?.GetComponent<UpgradeIcon>();
            if (artwork == null || artwork.Artwork == null) return;
            shownBlueprint = blueprint;
            entryProgress = 0f;
            storedBlueprint.sprite = entryImage.sprite = artwork.Artwork;
            var source = artwork.rectTransform;
            var rect = entryImage.rectTransform;
            var space = card.Rect;
            Vector3 origin = source.position;
            Quaternion rotation = source.rotation;
            Vector3 scale = source.lossyScale;
            scale.x *= source.rect.width / rect.rect.width;
            scale.y *= source.rect.height / rect.rect.height;
            rect.SetParent(space, false);
            rect.position = origin;
            rect.rotation = rotation;
            FolderEntryMotion.SetWorldScale(rect, scale);
            entryImage.color = Color.white;
            entryImage.gameObject.SetActive(true);
            if (entryCanvas != null) entryCanvas.overrideSorting = true;
            Vector3 localScale = new Vector3(scale.x / space.lossyScale.x,
                scale.y / space.lossyScale.y, scale.z / space.lossyScale.z);
            entry = StartCoroutine(Enter(space, space.InverseTransformPoint(origin), localScale,
                Quaternion.Inverse(space.rotation) * rotation));
        }

        private IEnumerator Enter(RectTransform space, Vector3 origin, Vector3 scale, Quaternion rotation)
        {
            yield return FolderEntryMotion.Play(entryImage, entryCanvas, space, pocket,
                storedBlueprint.rectTransform, entryMouth, Mathf.Max(.1f, entrySeconds), origin, scale, rotation,
                () => game == null || game.IsChoosingBlueprint, t => entryProgress = t);
            entry = null;
            ResetEntryImage();
            storedBlueprint.gameObject.SetActive(shownBlueprint != null);
        }

        public void ClearBlueprint()
        {
            CancelReturn(returningBlueprint);
            if (entry != null) StopCoroutine(entry);
            entry = null;
            shownBlueprint = null;
            ResetEntryImage();
            if (storedBlueprint != null) storedBlueprint.gameObject.SetActive(false);
            HideHint();
        }

        internal struct ExitPose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public float slideSeconds;
        }

        internal ExitPose CaptureExitPose()
        {
            var source = IsEntering ? entryImage.rectTransform : storedBlueprint.rectTransform;
            Vector3 scale = source.lossyScale;
            scale.x *= source.rect.width / entryImage.rectTransform.rect.width;
            scale.y *= source.rect.height / entryImage.rectTransform.rect.height;
            return new ExitPose {
                position = source.position, scale = scale, rotation = source.rotation,
                slideSeconds = (IsEntering ? Mathf.Max(0f, entryProgress - .62f) : .38f) * Mathf.Max(.1f, entrySeconds)
            };
        }

        internal void AnimateReturn(DraggableCard blueprint, RectTransform destination, ExitPose pose)
        {
            Sprite sprite = storedBlueprint.sprite;
            ClearBlueprint();
            returningBlueprint = blueprint;
            var space = card.Rect;
            var rect = entryImage.rectTransform;
            rect.SetParent(pose.slideSeconds > 0f ? pocket : space, false);
            rect.position = pose.position;
            rect.rotation = pose.rotation;
            FolderEntryMotion.SetWorldScale(rect, pose.scale);
            entryImage.sprite = sprite;
            entryImage.color = Color.white;
            entryImage.gameObject.SetActive(true);
            if (entryCanvas != null) entryCanvas.overrideSorting = pose.slideSeconds <= 0f;
            blueprint.BeginBlueprintReturn(this);
            Vector3 localScale = new Vector3(pose.scale.x / space.lossyScale.x,
                pose.scale.y / space.lossyScale.y, pose.scale.z / space.lossyScale.z);
            exit = StartCoroutine(ReturnToVault(space, destination, space.InverseTransformPoint(pose.position),
                localScale, Quaternion.Inverse(space.rotation) * pose.rotation, pose.slideSeconds));
        }

        private IEnumerator ReturnToVault(RectTransform space, RectTransform destination, Vector3 localOrigin,
            Vector3 localScale, Quaternion localRotation, float slideSeconds)
        {
            var rect = entryImage.rectTransform;
            // Reverse the entry slide while still clipped behind the folder's front.
            for (float elapsed = 0f; elapsed < slideSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Smooth(Mathf.Clamp01(elapsed / slideSeconds));
                rect.position = Vector3.Lerp(space.TransformPoint(localOrigin), MouthPosition(), t);
                rect.rotation = Quaternion.Slerp(space.rotation * localRotation, storedBlueprint.rectTransform.rotation, t);
                FolderEntryMotion.SetWorldScale(rect, Vector3.Scale(localScale, space.lossyScale));
                entryImage.color = game.IsChoosingBlueprint ? Color.clear : Color.white;
                yield return null;
            }
            if (slideSeconds > 0f)
            {
                localOrigin = space.InverseTransformPoint(MouthPosition());
                localRotation = Quaternion.Inverse(space.rotation) * storedBlueprint.rectTransform.rotation;
            }
            rect.SetParent(space, true);
            if (entryCanvas != null) entryCanvas.overrideSorting = true;
            float travelSeconds = Mathf.Max(.1f, entrySeconds) * .62f;
            for (float elapsed = 0f; elapsed < travelSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Smooth(Mathf.Clamp01(elapsed / travelSeconds));
                Vector3 from = space.TransformPoint(localOrigin);
                Vector3 to = destination.TransformPoint(destination.rect.center);
                Vector3 bend = Vector3.Lerp(from, to, .5f) + space.up *
                    (storedBlueprint.rectTransform.rect.height * storedBlueprint.rectTransform.lossyScale.y * .2f);
                rect.position = Vector3.Lerp(Vector3.Lerp(from, bend, t), Vector3.Lerp(bend, to, t), t);
                rect.rotation = Quaternion.Slerp(space.rotation * localRotation, destination.rotation, t);
                Vector3 targetScale = destination.lossyScale;
                targetScale.x *= destination.rect.width / rect.rect.width;
                targetScale.y *= destination.rect.height / rect.rect.height;
                FolderEntryMotion.SetWorldScale(rect, Vector3.Lerp(Vector3.Scale(localScale, space.lossyScale), targetScale, t));
                entryImage.color = game.IsChoosingBlueprint ? Color.clear : Color.white;
                yield return null;
            }
            var blueprint = returningBlueprint;
            returningBlueprint = null;
            exit = null;
            ResetEntryImage();
            if (blueprint != null)
            {
                blueprint.Rect.position = destination.TransformPoint(destination.rect.center);
                blueprint.EndBlueprintReturn(this);
                if (blueprint.StoredInVault == game.Vault) blueprint.gameObject.SetActive(false);
            }
        }

        private Vector3 MouthPosition() => entryMouth != null ? entryMouth.position :
            storedBlueprint.rectTransform.position + storedBlueprint.rectTransform.up *
                (storedBlueprint.rectTransform.rect.height * storedBlueprint.rectTransform.lossyScale.y * 1.05f);

        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        internal void CancelReturn(DraggableCard blueprint)
        {
            if (blueprint == null || returningBlueprint != blueprint) return;
            if (exit != null) StopCoroutine(exit);
            exit = null;
            returningBlueprint = null;
            ResetEntryImage();
            blueprint.EndBlueprintReturn(this);
        }

        private void ResetEntryImage()
        {
            if (entryImage == null) return;
            if (entryCanvas != null) entryCanvas.overrideSorting = false;
            entryImage.gameObject.SetActive(false);
            // Unity forbids reparenting while an ancestor is deactivating or unloading.
            if (pocket == null || !gameObject.activeInHierarchy) return;
            var rect = entryImage.rectTransform;
            rect.SetParent(pocket, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private void SaveHintPose()
        {
            if (hintPoseSaved || personHint == null || arrowHint == null) return;
            arrowRest = arrowHint.anchoredPosition;
            personScale = personHint.localScale;
            hintPoseSaved = true;
        }

        private void LateUpdate()
        {
            if (game == null || factory == null) return;
            // Reconcile cancellation, turn reset and a temporarily disabled building without ghost cards.
            if (shownBlueprint != null && shownBlueprint != factory.PendingBlueprint) ClearBlueprint();
            if (shownBlueprint == null && factory.PendingBlueprint != null && storedBlueprint != null)
            {
                var art = factory.PendingBlueprint.transform.Find("CardSymbol")?.GetComponent<UpgradeIcon>();
                if (art != null)
                {
                    shownBlueprint = factory.PendingBlueprint;
                    storedBlueprint.sprite = art.Artwork;
                    storedBlueprint.gameObject.SetActive(true);
                }
            }
            bool concealed = game.IsChoosingBlueprint;
            if (storedBlueprint != null) storedBlueprint.color = concealed ? Color.clear : Color.white;
            bool needsWorker = factory.PendingBlueprint != null && station != null && !factory.HasEnoughWorkers &&
                !IsEntering && !concealed && !game.ActionsLocked;
            if (workerHint == null) return;
            if (needsWorker && !HasAffordableWorker()) { HideHint(); return; }
            SaveHintPose();
            if (needsWorker) workerHint.gameObject.SetActive(true);
            workerHint.alpha = Mathf.MoveTowards(workerHint.alpha, needsWorker ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(.01f, hintFadeSeconds));
            if (!needsWorker && workerHint.alpha <= 0f) { HideHint(); return; }
            hintTime += Time.unscaledDeltaTime;
            float wave = .5f - .5f * Mathf.Cos(hintTime * Mathf.PI * 2f / Mathf.Max(.1f, hintCycleSeconds));
            if (hintPoseSaved)
            {
                personHint.localScale = personScale * (1f + wave * .045f);
                arrowHint.anchoredPosition = arrowRest + Vector2.down * (wave * hintTravel);
            }
        }

        private void HideHint()
        {
            hintTime = 0f;
            if (workerHint != null) { workerHint.alpha = 0f; workerHint.gameObject.SetActive(false); }
            if (!hintPoseSaved) return;
            if (personHint != null) personHint.localScale = personScale;
            if (arrowHint != null) arrowHint.anchoredPosition = arrowRest;
        }

        private bool HasAffordableWorker()
        {
            if (game.AbilityPoints != null && game.AbilityPoints.CurrentPoints <= 0) return false;
            foreach (var person in game.Cards)
                if (person != null && person.CanStartWork && !person.IsStored && station.CanAccept(person)) return true;
            return false;
        }

        private void OnDisable() => ClearBlueprint();
    }
}
