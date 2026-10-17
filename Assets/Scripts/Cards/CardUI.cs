using UnityEngine;

namespace CivilGameJam.Cards
{
    internal static class CardUI
    {
        public static readonly Color NormalWorker = new Color(0.94f, 0.63f, 0.18f);
        public static readonly Color EducatedWorker = new Color(0.18f, 0.63f, 0.94f);
        public static readonly Color EmptySlot = new Color(0.23f, 0.27f, 0.31f);

        public static void SetProgress(RectTransform fill, float progress)
        {
            // Work timers stay continuous; only the displayed bar advances in ten fixed steps.
            float amount = Mathf.Floor(Mathf.Clamp01(progress) * 10f + 0.00001f) / 10f;
            Stretch(fill, Vector2.zero, new Vector2(amount, 1f));
        }

        public static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

    }
}
