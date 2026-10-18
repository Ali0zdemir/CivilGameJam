using UnityEngine;

namespace CivilGameJam.Menu
{
    [DisallowMultipleComponent]
    public sealed class MenuModelCarousel : MonoBehaviour
    {
        [SerializeField] private Transform[] items = new Transform[3];
        [SerializeField, Min(0.1f)] private float shiftInterval = 4.5f;
        [SerializeField, Min(0.1f)] private float transitionSeconds = 1f;
        [SerializeField, Min(0.1f)] private float radius = 3.2f;
        [SerializeField, Range(0.1f, 1f)] private float sideScale = 0.84f;
        [SerializeField, Range(0.1f, 1f)] private float backScale = 0.45f;

        private double elapsed;

        private void OnEnable() => ResetLayout();

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyLayout(elapsed);
        }

        [ContextMenu("Reset Carousel Layout")]
        public void ResetLayout()
        {
            elapsed = 0d;
            ApplyLayout(0d);
        }

        private void ApplyLayout(double time)
        {
            if (items == null || items.Length != 3) return;
            double interval = Mathf.Max(0.1f, shiftInterval);
            int step = (int)(time / interval);
            float progress = step == 0 ? 0f : Mathf.Clamp01((float)((time - step * interval) /
                Mathf.Min(Mathf.Max(0.1f, transitionSeconds), (float)interval)));
            // Zero acceleration at either end keeps each move from snapping into its resting slot.
            float ease = progress * progress * progress * (progress * (progress * 6f - 15f) + 10f);

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                int from = (i - Mathf.Max(0, step - 1) % 3 + 3) % 3;
                float startAngle = (from - 1) * 90f;
                // The left item travels behind the arc to reach the right, without teleporting.
                float travel = from == 0 ? -180f : -90f;
                float angle = (startAngle + (step == 0 ? 0f : travel * ease)) * Mathf.Deg2Rad;
                float facing = Mathf.Cos(angle);
                items[i].localPosition = new Vector3(Mathf.Sin(angle) * radius, 0f, -facing * radius);
                float scale = facing >= 0f ? Mathf.Lerp(sideScale, 1f, facing) : Mathf.Lerp(sideScale, backScale, -facing);
                items[i].localScale = Vector3.one * scale;
                // Rotation belongs to each item's child; changing slots never resets its spin.
            }
        }
    }
}
