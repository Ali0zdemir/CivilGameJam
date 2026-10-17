using TMPro;
using CivilGameJam.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Disasters
{
    /// <summary>Runs a camera-only earthquake test and restores the camera afterwards.</summary>
    [DisallowMultipleComponent]
    public sealed class EarthquakeTestController : MonoBehaviour
    {
        private const float MaximumShakeFrequency = 4f;
        private const float JoltDuration = 0.6f;
        private const float DefaultHorizontalDisplacement = 0.054f;
        private const float DefaultVerticalDisplacement = 0.0396f;

        [Header("Timing")]
        [SerializeField, Min(3f)] private float duration = 7f;

        [Header("Camera shake")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Side-to-side movement. Lower values also reduce horizontal angular shake; zero locks this screen axis.")]
        [SerializeField, Range(0f, 0.15f)] private float horizontalDisplacement = DefaultHorizontalDisplacement;
        [Tooltip("Up-and-down movement. Lower values also reduce vertical angular shake; zero locks this screen axis.")]
        [SerializeField, Range(0f, 0.15f)] private float verticalDisplacement = DefaultVerticalDisplacement;
        [SerializeField, Range(0f, 1.5f)] private float cameraRoll = 0.25f;

        [Header("Camera vibration")]
        [Tooltip("Maximum pitch and yaw variation, measured in degrees.")]
        [SerializeField, Range(0f, 1f)] private float cameraVibrationAngle = 0.45f;
        [SerializeField, Range(1f, 8f)] private float vibrationFrequency = 4f;

        [Header("Irregular jolts")]
        [Tooltip("Brief angular jolts during the stronger part of the earthquake.")]
        [SerializeField, Range(0f, 0.6f)] private float joltAngle = 0.3f;

        [Header("Gradual speed (cycles per second)")]
        [Tooltip("Starts with slow, irregular movement before gradually speeding up.")]
        [SerializeField, Range(0.05f, 1f)] private float startingFrequency = 0.27f;
        [Tooltip("Upper shaking speed, capped to keep the movement gentle.")]
        [SerializeField, Range(0.5f, MaximumShakeFrequency)] private float maximumFrequency = 2.6f;

        [Header("Test panel")]
        [SerializeField] private Button testButton;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private ProgressBarFeedback progressFeedback;

        private Vector3 cameraPosition;
        private Quaternion cameraRotation;
        private float elapsed;
        private float shakePhase;
        private float vibrationPhase;
        private float noiseSeed;
        private float nextJoltTime;
        private float joltAge;
        private Vector3 joltRotation;
        private bool poseCaptured;
        private readonly System.Random random = new System.Random();

        public bool IsRunning { get; private set; }
        public float NormalizedProgress => IsRunning ? Mathf.Clamp01(elapsed / Mathf.Max(3f, duration)) : 0f;

        private void Awake()
        {
            if (progressFeedback == null && progressFill != null) progressFeedback = progressFill.GetComponentInParent<ProgressBarFeedback>(true);
            ResetTest();
        }

        [ContextMenu("Start earthquake test (Play Mode)")]
        public void StartEarthquake()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsRunning || cameraTransform == null) return;
            if (cameraTransform.GetComponent<Camera>() == null) return;
            cameraPosition = cameraTransform.localPosition;
            cameraRotation = cameraTransform.localRotation;
            poseCaptured = true;
            elapsed = 0f;
            shakePhase = 0f;
            vibrationPhase = 0f;
            noiseSeed = RandomRange(0f, 1000f);
            nextJoltTime = Mathf.Max(3f, duration) * RandomRange(0.22f, 0.30f);
            joltAge = JoltDuration;
            joltRotation = Vector3.zero;
            IsRunning = true;
            if (progressFeedback != null) progressFeedback.Begin();
            if (testButton != null) testButton.interactable = false;
            if (statusLabel != null) statusLabel.text = "Earthquake started";
        }

        private void LateUpdate()
        {
            if (!IsRunning) return;
            AdvanceShake(Time.unscaledDeltaTime);
        }

        private void AdvanceShake(float deltaTime)
        {
            if (cameraTransform == null)
            {
                ResetTest();
                return;
            }

            var testDuration = Mathf.Max(3f, duration);
            elapsed = Mathf.Min(elapsed + deltaTime, testDuration);
            var progress = elapsed / testDuration;
            var ramp = Mathf.SmoothStep(0f, 1f, progress / 0.8f);
            var speedLimit = Mathf.Clamp(maximumFrequency, 0.5f, MaximumShakeFrequency);
            var startSpeed = Mathf.Clamp(startingFrequency, 0.05f, Mathf.Min(1f, speedLimit));
            var frequency = Mathf.Lerp(startSpeed, speedLimit, ramp);
            // Integrate frequency so accelerating never introduces a sudden phase jump.
            shakePhase += frequency * deltaTime;
            vibrationPhase += Mathf.Lerp(vibrationFrequency * 0.45f, vibrationFrequency, ramp) * deltaTime;

            var attack = Mathf.SmoothStep(0f, 1f, progress / 0.22f);
            var release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, progress));
            var surge = Mathf.Lerp(0.65f, 1f, SampleNoise(elapsed * 0.65f, 113.5f) * 0.5f + 0.5f);
            var energy = attack * release * Mathf.Lerp(0.25f, 1f, ramp) * surge;

            UpdateJolt(deltaTime, progress);
            ShakeCamera(energy);
            if (progressFeedback != null) progressFeedback.SetProgress(progress, true);
            else if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(progress);
            if (statusLabel != null)
                statusLabel.text = progress < 0.30f ? "Light tremors" : progress < 0.72f ? "Shaking intensifies" : "Shaking subsides";
            if (elapsed >= testDuration) ResetTest(true);
        }

        private void UpdateJolt(float deltaTime, float progress)
        {
            joltAge += deltaTime;
            if (elapsed < nextJoltTime || progress >= 0.72f) return;

            joltAge = elapsed - nextJoltTime;
            joltRotation = new Vector3(RandomRange(-1f, 1f), RandomRange(-0.75f, 0.75f), RandomRange(-0.25f, 0.25f))
                * (joltAngle * RandomRange(0.65f, 1f));
            nextJoltTime = elapsed + RandomRange(0.75f, 1.3f);
        }

        private void ShakeCamera(float energy)
        {
            if (cameraTransform == null) return;
            // Independent noise layers avoid a repeating, pendulum-like motion.
            var low = SampleNoiseVector(shakePhase * 0.3f, 0f);
            var medium = SampleNoiseVector(shakePhase, 37.3f);
            var detail = SampleNoiseVector(vibrationPhase, 79.1f);
            var broadMotion = low * 0.2f + medium * 0.8f;
            var angularMotion = low * 0.18f + medium * 0.62f + detail * 0.2f;
            var sway = new Vector3(broadMotion.x * horizontalDisplacement, broadMotion.y * verticalDisplacement, 0f);
            var rotation = new Vector3(angularMotion.x * cameraVibrationAngle,
                angularMotion.y * cameraVibrationAngle * 0.75f, angularMotion.z * cameraRoll);

            // A short, damped response adds occasional impacts without a positional jump.
            var joltFade = 1f - Mathf.SmoothStep(0f, 1f, joltAge / JoltDuration);
            var jolt = joltRotation * (Mathf.Sin(joltAge * (2f * Mathf.PI) * 3.5f) * Mathf.Exp(-joltAge * 7.5f) * joltFade);

            // Angular noise and jolts must obey the same screen-axis controls as translation.
            // Roll affects both screen axes, so it fades out when either axis is disabled.
            var horizontalWeight = Mathf.Clamp01(horizontalDisplacement / DefaultHorizontalDisplacement);
            var verticalWeight = Mathf.Clamp01(verticalDisplacement / DefaultVerticalDisplacement);
            rotation += jolt;
            rotation.x *= verticalWeight;
            rotation.y *= horizontalWeight;
            rotation.z *= Mathf.Min(horizontalWeight, verticalWeight);

            // Always apply offsets to the captured pose, never to the previous frame.
            cameraTransform.localPosition = cameraPosition + cameraRotation * (sway * energy);
            cameraTransform.localRotation = cameraRotation *
                Quaternion.Euler(rotation * energy);
        }

        private float SampleNoise(float phase, float channel) =>
            Mathf.PerlinNoise(noiseSeed + phase, noiseSeed * 0.13f + channel) * 2f - 1f;

        private Vector3 SampleNoiseVector(float phase, float channel) => new Vector3(
            SampleNoise(phase, channel + 17.1f), SampleNoise(phase, channel + 43.7f), SampleNoise(phase, channel + 81.2f));

        private float RandomRange(float minimum, float maximum) =>
            Mathf.Lerp(minimum, maximum, (float)random.NextDouble());

        [ContextMenu("Stop earthquake test")]
        public void StopEarthquake()
        {
            if (Application.isPlaying) ResetTest();
        }

        private void OnDisable()
        {
            if (poseCaptured || IsRunning) ResetTest();
        }

        private void ResetTest(bool completed = false)
        {
            IsRunning = false;
            if (poseCaptured && cameraTransform != null)
                cameraTransform.SetLocalPositionAndRotation(cameraPosition, cameraRotation);
            poseCaptured = false;
            if (testButton != null) testButton.interactable = true;
            if (progressFeedback != null)
            {
                if (completed) progressFeedback.Complete(); else progressFeedback.ResetPresentation();
            }
            else if (progressFill != null) progressFill.fillAmount = 0f;
            if (statusLabel != null) statusLabel.text = "Ready";
        }

    }
}
