using System;
using CivilGameJam.Upgrades;
using UnityEngine;

namespace CivilGameJam.Disasters
{
    /// <summary>Samples the authored apartment earthquake motion for the installed protection.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class ApartmentEarthquakeAnimation : MonoBehaviour
    {
        [Serializable]
        private sealed class Part
        {
            public Transform body;
            public Transform[] attachments = Array.Empty<Transform>();
            [NonSerialized] public Pose rest;
            [NonSerialized] public Matrix4x4 inverse;
            [NonSerialized] public Pose[] attachmentRest;
            [NonSerialized] public Vector3[] positions;
            [NonSerialized] public Quaternion[] rotations;
        }

        [Serializable]
        private sealed class Variant
        {
            public Transform root;
            public AnimationClip collapse;
            public AnimationClip seismic;
            public Part[] parts = Array.Empty<Part>();
        }

        private struct Pose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public static Pose Read(Transform target) => new Pose
                { position = target.localPosition, rotation = target.localRotation, scale = target.localScale };
            public void Restore(Transform target)
            {
                if (target == null) return;
                target.SetLocalPositionAndRotation(position, rotation);
                target.localScale = scale;
            }
        }

        [SerializeField] private ApartmentUpgradeModel apartment;
        [SerializeField] private ApartmentTsunamiCollapse tsunamiCollapse;
        [SerializeField] private BuildingCollapseFeedback collapseFeedback;
        [SerializeField] private Variant normal = new Variant();
        [SerializeField] private Variant elevated = new Variant();
        private Variant active;
        private bool elevatedAtStart;

        public bool IsRunning { get; private set; }
        public bool HasCollapsed { get; private set; }
        public bool IsProtected { get; private set; }
        public float Progress { get; private set; }
        public AnimationClip ActiveClip { get; private set; }

        public bool BeginEarthquake()
        {
            ResetEarthquake();
            if (!Application.isPlaying || !isActiveAndEnabled || apartment == null ||
                tsunamiCollapse != null && tsunamiCollapse.HasCollapsed) return false;
            if (!TryGetComponent<BuildingModelGeometry>(out var geometry) || !geometry.IsResidential) return false;
            elevatedAtStart = apartment.IsElevated;
            IsProtected = apartment.HasSeismic;
            var variant = elevatedAtStart ? elevated : normal;
            var clip = IsProtected ? variant.seismic : variant.collapse;
            if (variant.root == null || clip == null || variant.parts.Length == 0) return false;
            foreach (var part in variant.parts)
                if (part == null || part.body == null || Array.Exists(part.attachments, target => target == null)) return false;
            active = variant;
            ActiveClip = clip;
            foreach (var part in active.parts)
            {
                part.rest = Pose.Read(part.body);
                part.inverse = Matrix4x4.TRS(part.rest.position, part.rest.rotation, part.rest.scale).inverse;
                part.attachmentRest = new Pose[part.attachments.Length];
                part.positions = new Vector3[part.attachments.Length];
                part.rotations = new Quaternion[part.attachments.Length];
                for (var i = 0; i < part.attachments.Length; i++)
                {
                    var target = part.attachments[i];
                    part.attachmentRest[i] = Pose.Read(target);
                    part.positions[i] = active.root.InverseTransformPoint(target.position);
                    part.rotations[i] = Quaternion.Inverse(active.root.rotation) * target.rotation;
                }
            }
            IsRunning = true;
            if (!IsProtected && collapseFeedback != null) collapseFeedback.PrepareCollapse();
            SetEarthquakeProgress(0f);
            return true;
        }

        public void SetEarthquakeProgress(float progress)
        {
            if (!IsRunning || active == null) return;
            if (apartment.HasSeismic != IsProtected || apartment.IsElevated != elevatedAtStart)
                if (!BeginEarthquake()) return;
            Progress = Mathf.Clamp01(progress);
            ActiveClip.SampleAnimation(active.root.gameObject, ActiveClip.length * Progress);
            FollowAttachments();
            if (!IsProtected && collapseFeedback != null)
            {
                if (!collapseFeedback.Reported || Progress < 1f)
                    foreach (var part in active.parts)
                        if (part.body.localPosition.y < part.rest.position.y - .05f || Quaternion.Angle(part.rest.rotation, part.body.localRotation) > 12f)
                        { collapseFeedback.ReportCollapse(); break; }
                if (Progress >= 1f) collapseFeedback.FinishCollapse();
            }
        }

        public void FinishEarthquake()
        {
            if (!IsRunning) return;
            SetEarthquakeProgress(1f);
            IsRunning = false;
            if (IsProtected) ResetEarthquake();
            else HasCollapsed = true;
        }

        private void LateUpdate()
        {
            if (active == null) return;
            if (apartment.HasSeismic != IsProtected || apartment.IsElevated != elevatedAtStart)
            {
                if (IsRunning) SetEarthquakeProgress(Progress);
                else ResetEarthquake();
            }
            if (active != null) FollowAttachments();
        }

        private void FollowAttachments()
        {
            foreach (var part in active.parts)
            {
                var delta = Matrix4x4.TRS(part.body.localPosition, part.body.localRotation, part.body.localScale) * part.inverse;
                var rotation = part.body.localRotation * Quaternion.Inverse(part.rest.rotation);
                for (var i = 0; i < part.attachments.Length; i++)
                {
                    var target = part.attachments[i];
                    if (target != null)
                        target.SetPositionAndRotation(active.root.TransformPoint(delta.MultiplyPoint3x4(part.positions[i])),
                            active.root.rotation * rotation * part.rotations[i]);
                }
            }
        }

        public void ResetEarthquake() => RestoreEarthquake(true);
        public void ReleaseForTsunami() => RestoreEarthquake(false);

        private void RestoreEarthquake(bool clearFeedback)
        {
            if (active != null && clearFeedback && collapseFeedback != null) collapseFeedback.ResetFeedback();
            if (active != null)
                foreach (var part in active.parts)
                {
                    part.rest.Restore(part.body);
                    for (var i = 0; i < part.attachments.Length; i++) part.attachmentRest[i].Restore(part.attachments[i]);
                }
            active = null;
            ActiveClip = null;
            IsRunning = HasCollapsed = IsProtected = false;
            Progress = 0f;
        }

        private void OnDisable() => ResetEarthquake();
    }
}
