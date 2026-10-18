using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    /// <summary>The dimmer and contours follow the artwork's saved alpha silhouettes.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TutorialSpotlight : MaskableGraphic
    {
        [Flags] public enum Workplaces { ResearchCenter = 1, Factory = 2, School = 4, All = 7 }

        [Serializable]
        private sealed class Silhouette
        {
            public Sprite artwork;
            public Sprite mask;
            public Rect contentUV;
        }

        private struct Window
        {
            public Image artwork;
            public Silhouette silhouette;
            public Vector4 x, y;
            public bool Contains(Vector2 point)
            {
                if (silhouette == null) return false;
                float u = x.x * point.x + x.y * point.y + x.w;
                float v = y.x * point.x + y.y * point.y + y.w;
                return u >= 0f && u <= 1f && v >= 0f && v <= 1f &&
                    silhouette.mask.texture.GetPixelBilinear(u, v).a > .1f;
            }
        }

        [SerializeField] private RectTransform[] targets;
        [SerializeField] private Image[] frames;
        [SerializeField] private Image personFrame;
        [SerializeField] private RectTransform peoplePanel;
        [SerializeField] private RectTransform[] progressBars;
        private readonly Vector4[] progressX = new Vector4[3], progressY = new Vector4[3];
        private int visibleProgress;
        private bool showPeoplePanel = true;
        private Vector4 peopleX, peopleY;
        private RectTransform highlightedArea;
        private IReadOnlyList<DraggableCard> highlightedProducts;
        private readonly Vector3[] areaCorners = new Vector3[4];
        private bool visibleArea;
        private bool areaInteractive;
        private IReadOnlyList<HouseUpgrades> highlightedHouses;
        private bool houseInputEnabled;
        [Header("Saved artwork silhouettes")]
        [SerializeField] private Image[] targetArtwork;
        [SerializeField] private Silhouette[] silhouettes;
        [SerializeField] private Material contourMaterial;
        [SerializeField, Range(1f, 4f)] private float contourPixels = 2f;
        private RectTransform practicePerson, practiceSchool, highlightedPerson;
        private Workplaces highlightedWorkplaces = Workplaces.All;
        private bool showAbilityPoints, showPersonOutline = true;
        private readonly Window[] windows = new Window[9];
        private Material dimmerInstance;
        private Material[] contourInstances;
        private static readonly int[] MaskIds = Properties("Mask"), XIds = Properties("X"), YIds = Properties("Y");
        private static readonly int ActiveId = Shader.PropertyToID("_ActiveMask");
        private static readonly int ThicknessId = Shader.PropertyToID("_ThicknessUV");
        private static readonly int PeopleEnabledId = Shader.PropertyToID("_ShowPeoplePanel");
        private static readonly int PeopleXId = Shader.PropertyToID("_PeopleX"), PeopleYId = Shader.PropertyToID("_PeopleY");

        public bool IsPersonOutlineVisible => personFrame != null && personFrame.gameObject.activeInHierarchy;

        private static int[] Properties(string suffix)
        {
            var ids = new int[9];
            for (int i = 0; i < ids.Length; i++) ids[i] = Shader.PropertyToID("_Focus" + i + suffix);
            return ids;
        }

        protected override void OnEnable() { base.OnEnable(); RefreshLayout(); }
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (isActiveAndEnabled) RefreshLayout();
        }

        private void PrepareMaterials()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (dimmerInstance == null && material != null && material.shader.name == "CivilGameJam/UI/Tutorial Dimmer")
            {
                dimmerInstance = new Material(material) { name = "Tutorial dimmer instance", hideFlags = HideFlags.HideAndDontSave };
                material = dimmerInstance;
            }
            if (contourMaterial == null) return;
            // Script reload can retain the earlier five-window cache. Every current
            // window needs its own material, including the four house contours.
            if (contourInstances == null) contourInstances = new Material[windows.Length];
            else if (contourInstances.Length < windows.Length) Array.Resize(ref contourInstances, windows.Length);
            for (int i = 0; i < contourInstances.Length; i++)
                if (contourInstances[i] == null)
                    contourInstances[i] = new Material(contourMaterial) { name = "Tutorial contour " + i, hideFlags = HideFlags.HideAndDontSave };
        }

        public void SetLessonFocus(Workplaces workplaces, bool abilityPoints, RectTransform person, bool outline = true)
        {
            highlightedProducts = null;
            showPeoplePanel = false; highlightedArea = null; areaInteractive = false; highlightedHouses = null; houseInputEnabled = false;
            highlightedWorkplaces = workplaces; showAbilityPoints = abilityPoints;
            highlightedPerson = person; showPersonOutline = outline;
            RefreshLayout();
        }

        public void SetOverviewFocus()
        {
            highlightedProducts = null;
            highlightedWorkplaces = Workplaces.All; showAbilityPoints = false;
            highlightedPerson = null; showPeoplePanel = true;
            highlightedHouses = null;
            highlightedArea = peoplePanel; areaInteractive = false;
            RefreshLayout();
        }

        public void SetAreaFocus(Workplaces workplaces, bool abilityPoints, RectTransform person,
            RectTransform area, bool interactive = false, bool outline = true)
        {
            SetLessonFocus(workplaces, abilityPoints, person, outline);
            highlightedArea = area; showPeoplePanel = area != null; areaInteractive = interactive;
            RefreshLayout();
        }

        public void SetPracticeTargets(RectTransform person, RectTransform school)
        { practicePerson = person; practiceSchool = school; }

        public void SetHouseInstallationFocus(IReadOnlyList<HouseUpgrades> houses, RectTransform products, bool interactive = true)
        {
            SetAreaFocus(0, false, null, products, interactive);
            highlightedHouses = houses; houseInputEnabled = interactive;
            RefreshLayout();
        }

        public void SetHouseProductFocus(IReadOnlyList<HouseUpgrades> houses, IReadOnlyList<DraggableCard> products, bool interactive = true)
        {
            SetHouseInstallationFocus(houses, null, interactive);
            highlightedProducts = products; showPeoplePanel = true;
            RefreshLayout();
        }

        public static Image HouseArtwork(HouseUpgrades house)
        {
            var edge = house.transform.Find("FolderArt/DışKenar")?.GetComponent<Image>();
            if (edge != null && edge.isActiveAndEnabled && edge.sprite != null && edge.color.a > .01f) return edge;
            Image best = null; float largest = 0f;
            foreach (var graphic in house.GetComponentsInChildren<Image>(true))
            {
                if (!graphic.isActiveAndEnabled || graphic.sprite == null || graphic.color.a <= .01f) continue;
                float area = graphic.rectTransform.rect.width * graphic.rectTransform.rect.height * Mathf.Abs(graphic.rectTransform.lossyScale.x * graphic.rectTransform.lossyScale.y);
                if (area <= largest) continue;
                largest = area; best = graphic;
            }
            return best;
        }

        public void PulsePerson(float alpha)
        {
            if (personFrame == null) return;
            var tint = personFrame.color; tint.a = alpha; personFrame.color = tint;
        }

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (LocalPoint(screenPoint, eventCamera, out var point))
            {
                if (areaInteractive && AreaContains(point)) return false;
                if (houseInputEnabled && highlightedHouses != null)
                    for (int i = 5; i < windows.Length; i++) if (windows[i].Contains(point)) return false;
                if (practicePerson != null && windows[4].Contains(point)) return false;
                if (practiceSchool != null && targets != null)
                    for (int i = 0; i < 3 && i < targets.Length; i++)
                        if (targets[i] == practiceSchool && windows[i].Contains(point)) return false;
            }
            return base.Raycast(screenPoint, eventCamera);
        }

        public bool IsHighlighted(Vector2 screenPosition, Camera eventCamera = null)
        {
            if (!LocalPoint(screenPosition, eventCamera, out var point)) return false;
            if (AreaContains(point)) return true;
            for (int i = 0; i < progressX.Length; i++)
                if ((visibleProgress & (1 << i)) != 0 && InUnitRect(
                    progressX[i].x * point.x + progressX[i].y * point.y + progressX[i].w,
                    progressY[i].x * point.x + progressY[i].y * point.y + progressY[i].w)) return true;
            foreach (var window in windows) if (window.Contains(point)) return true;
            return false;
        }

        private static bool InUnitRect(float x, float y) => x >= 0f && x <= 1f && y >= 0f && y <= 1f;

        private bool AreaContains(Vector2 point) => visibleArea &&
            InUnitRect(peopleX.x * point.x + peopleX.y * point.y + peopleX.w,
                peopleY.x * point.x + peopleY.y * point.y + peopleY.w);

        private bool LocalPoint(Vector2 screenPosition, Camera eventCamera, out Vector2 point) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, eventCamera, out point);

        private void LateUpdate() => RefreshLayout();

        public void RefreshLayout()
        {
            // CanvasScaler can resize the canvas before this graphic's OnEnable.
            PrepareMaterials();
            visibleProgress = 0;
            for (int i = 0; i < progressX.Length; i++)
            {
                var bar = progressBars != null && i < progressBars.Length ? progressBars[i] : null;
                if (bar == null || !bar.gameObject.activeInHierarchy || bar.rect.width <= 0f || bar.rect.height <= 0f) continue;
                var map = bar.worldToLocalMatrix * rectTransform.localToWorldMatrix;
                var rect = bar.rect;
                progressX[i] = new Vector4(map.m00 / rect.width, map.m01 / rect.width, 0f, (map.m03 - rect.xMin) / rect.width);
                progressY[i] = new Vector4(map.m10 / rect.height, map.m11 / rect.height, 0f, (map.m13 - rect.yMin) / rect.height);
                visibleProgress |= 1 << i;
                if (material == null) continue;
                material.SetVector("_Progress" + i + "X", progressX[i]);
                material.SetVector("_Progress" + i + "Y", progressY[i]);
            }
            if (material != null) material.SetFloat("_VisibleProgress", visibleProgress);
            int active = 0;
            for (int i = 0; i < windows.Length; i++)
            {
                bool houseVisible = i >= 5 && highlightedHouses != null && i - 5 < highlightedHouses.Count;
                var source = houseVisible ? HouseArtwork(highlightedHouses[i - 5]) : i == 4 ? PersonArtwork(highlightedPerson) :
                    targetArtwork != null && i < targetArtwork.Length ? targetArtwork[i] : null;
                bool visible = i >= 5 ? houseVisible : i == 4 ? highlightedPerson != null : i < 3 ? ((int)highlightedWorkplaces & (1 << i)) != 0 : showAbilityPoints;
                var silhouette = visible && source != null ? FindSilhouette(source.sprite) : null;
                windows[i] = new Window();
                var frame = i == 4 ? personFrame : frames != null && i < frames.Length ? frames[i] : null;
                if (silhouette != null && source != null && source.gameObject.activeInHierarchy)
                {
                    windows[i] = MakeWindow(source, silhouette);
                    active |= 1 << i;
                    var owner = source.GetComponentInParent<DraggableCard>();
                    bool held = owner != null && owner.IsHeld;
                    if (frame != null)
                    {
                        bool show = !held && (i != 4 || showPersonOutline);
                        frame.gameObject.SetActive(show);
                        if (show) MatchContour(frame, windows[i], i);
                    }
                }
                else if (frame != null) frame.gameObject.SetActive(false);
                if (material == null || !material.HasProperty(ActiveId)) continue;
                material.SetTexture(MaskIds[i], silhouette != null ? silhouette.mask.texture : Texture2D.blackTexture);
                material.SetVector(XIds[i], windows[i].x); material.SetVector(YIds[i], windows[i].y);
            }
            if (material != null && material.HasProperty(ActiveId)) material.SetInt(ActiveId, active);
            visibleArea = UpdateAreaMapping();
            if (material != null && material.HasProperty(PeopleEnabledId))
            {
                material.SetFloat(PeopleEnabledId, visibleArea ? 1f : 0f);
                if (visibleArea)
                {
                    material.SetVector(PeopleXId, peopleX); material.SetVector(PeopleYId, peopleY);
                }
            }
            SetVerticesDirty();
        }

        private bool UpdateAreaMapping()
        {
            if (!showPeoplePanel) return false;
            if (highlightedProducts != null)
            {
                DraggableCard held = null;
                foreach (var product in highlightedProducts)
                    if (product != null && product.IsHeld) { held = product; break; }
                var min = Vector2.positiveInfinity; var max = Vector2.negativeInfinity;
                foreach (var product in highlightedProducts)
                {
                    if (product == null || product.InstalledIn != null || !product.gameObject.activeInHierarchy ||
                        product.IsStored || held != null && held != product) continue;
                    product.Rect.GetWorldCorners(areaCorners);
                    foreach (var corner in areaCorners)
                    {
                        Vector2 local = rectTransform.InverseTransformPoint(corner);
                        min = Vector2.Min(min, local); max = Vector2.Max(max, local);
                    }
                }
                if (!float.IsFinite(min.x) || max.x - min.x <= .001f || max.y - min.y <= .001f) return false;
                peopleX = new Vector4(1f / (max.x - min.x), 0f, 0f, -min.x / (max.x - min.x));
                peopleY = new Vector4(0f, 1f / (max.y - min.y), 0f, -min.y / (max.y - min.y));
                return true;
            }
            if (highlightedArea == null || !highlightedArea.gameObject.activeInHierarchy) return false;
            var rect = highlightedArea.rect;
            if (rect.width <= .001f || rect.height <= .001f) return false;
            var map = highlightedArea.worldToLocalMatrix * rectTransform.localToWorldMatrix;
            peopleX = new Vector4(map.m00 / rect.width, map.m01 / rect.width, 0f, (map.m03 - rect.xMin) / rect.width);
            peopleY = new Vector4(map.m10 / rect.height, map.m11 / rect.height, 0f, (map.m13 - rect.yMin) / rect.height);
            return true;
        }

        private Silhouette FindSilhouette(Sprite sprite)
        {
            if (sprite == null || silhouettes == null) return null;
            foreach (var item in silhouettes) if (item.artwork == sprite && item.mask != null) return item;
            return null;
        }

        private static Image PersonArtwork(RectTransform target)
        {
            if (target == null) return null;
            var image = target.GetComponent<Image>();
            if (image != null && image.sprite != null) return image;
            return target.Find("Artwork")?.GetComponent<Image>();
        }

        private static Rect DrawingRect(Image image)
        {
            var rect = image.GetPixelAdjustedRect();
            if (!image.preserveAspect || image.sprite == null) return rect;
            float aspect = image.sprite.rect.width / image.sprite.rect.height;
            float height = Mathf.Min(rect.height, rect.width / aspect), width = height * aspect;
            return new Rect(rect.x + (rect.width - width) * image.rectTransform.pivot.x,
                rect.y + (rect.height - height) * image.rectTransform.pivot.y, width, height);
        }

        private Window MakeWindow(Image image, Silhouette silhouette)
        {
            var drawing = DrawingRect(image);
            var transformToImage = image.rectTransform.worldToLocalMatrix * rectTransform.localToWorldMatrix;
            float u = silhouette.contentUV.width / drawing.width, v = silhouette.contentUV.height / drawing.height;
            return new Window { artwork = image, silhouette = silhouette,
                x = new Vector4(transformToImage.m00 * u, transformToImage.m01 * u, 0f,
                    (transformToImage.m03 - drawing.xMin) * u + silhouette.contentUV.xMin),
                y = new Vector4(transformToImage.m10 * v, transformToImage.m11 * v, 0f,
                    (transformToImage.m13 - drawing.yMin) * v + silhouette.contentUV.yMin) };
        }

        private void MatchContour(Image frame, Window window, int index)
        {
            var source = window.artwork.rectTransform;
            var drawing = DrawingRect(window.artwork);
            var uv = window.silhouette.contentUV;
            Vector2 size = new Vector2(drawing.width / uv.width, drawing.height / uv.height);
            var fullRect = new Rect(drawing.xMin - uv.xMin * size.x, drawing.yMin - uv.yMin * size.y, size.x, size.y);
            var rect = frame.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = fullRect.size;
            rect.SetPositionAndRotation(source.TransformPoint(fullRect.center), source.rotation);
            Vector3 scale = source.lossyScale, parent = rect.parent.lossyScale;
            rect.localScale = new Vector3(scale.x / parent.x, scale.y / parent.y, scale.z / parent.z);
            frame.sprite = window.silhouette.mask; frame.type = Image.Type.Simple; frame.preserveAspect = false; frame.fillCenter = true;
            // Edit Mode/Play Mode transitions can still expose an old cache; do not
            // index it until the slot exists and contains a live material.
            frame.material = contourInstances != null && index >= 0 && index < contourInstances.Length && contourInstances[index] != null
                ? contourInstances[index] : contourMaterial;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var center = source.TransformPoint(fullRect.center);
            float width = Vector2.Distance(RectTransformUtility.WorldToScreenPoint(camera, center),
                RectTransformUtility.WorldToScreenPoint(camera, center + source.TransformVector(Vector3.right * size.x)));
            float height = Vector2.Distance(RectTransformUtility.WorldToScreenPoint(camera, center),
                RectTransformUtility.WorldToScreenPoint(camera, center + source.TransformVector(Vector3.up * size.y)));
            if (frame.material != null) frame.material.SetVector(ThicknessId,
                new Vector4(contourPixels / Mathf.Max(1f, width), contourPixels / Mathf.Max(1f, height), 0f, 0f));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = rectTransform.rect;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (dimmerInstance != null) Destroy(dimmerInstance);
            if (contourInstances != null) foreach (var instance in contourInstances) if (instance != null) Destroy(instance);
        }
    }
}
