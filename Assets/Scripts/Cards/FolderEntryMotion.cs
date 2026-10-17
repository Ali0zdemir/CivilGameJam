using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    // Shared by people and blueprints: approach above the lip, then slide behind it.
    internal static class FolderEntryMotion
    {
        public static IEnumerator Play(Image image, Canvas canvas, RectTransform space, RectTransform pocket,
            RectTransform target, RectTransform mouth, float seconds, Vector3 localOrigin, Vector3 localScale,
            Quaternion localRotation, Func<bool> concealed, Action<float> progress = null)
        {
            var rect = image.rectTransform;
            bool behindFlap = false;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / seconds);
                progress?.Invoke(t);
                Vector3 origin = space.TransformPoint(localOrigin);
                Vector3 fromScale = Vector3.Scale(localScale, space.lossyScale);
                Quaternion rotation = space.rotation * localRotation;
                image.color = concealed() ? Color.clear : Color.white;
                Vector3 destination = target.position;
                Vector3 above = mouth != null ? mouth.position :
                    destination + target.up * (target.rect.height * target.lossyScale.y * 1.05f);
                Vector3 toScale = new Vector3(target.lossyScale.x * target.rect.width / rect.rect.width,
                    target.lossyScale.y * target.rect.height / rect.rect.height, target.lossyScale.z);
                if (t < .62f)
                {
                    float approach = Smooth(t / .62f);
                    Vector3 bend = Vector3.Lerp(origin, above, .5f) + target.up * (target.rect.height * target.lossyScale.y * .2f);
                    rect.position = Vector3.Lerp(Vector3.Lerp(origin, bend, approach), Vector3.Lerp(bend, above, approach), approach);
                    rect.rotation = Quaternion.Slerp(rotation, target.rotation, approach);
                    SetWorldScale(rect, Vector3.Lerp(fromScale, toScale, approach));
                }
                else
                {
                    if (!behindFlap)
                    {
                        if (canvas != null) canvas.overrideSorting = false;
                        rect.SetParent(pocket, true);
                        behindFlap = true;
                    }
                    rect.position = Vector3.Lerp(above, destination, Smooth((t - .62f) / .38f));
                    rect.rotation = target.rotation;
                    SetWorldScale(rect, toScale);
                }
                yield return null;
            }
        }

        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        internal static void SetWorldScale(RectTransform rect, Vector3 scale)
        {
            Vector3 parentScale = rect.parent.lossyScale;
            rect.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
        }
    }
}
