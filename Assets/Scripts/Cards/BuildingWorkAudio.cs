using UnityEngine;

namespace CivilGameJam.Cards
{
    /// <summary>One intro, a continuous work loop and one outro for a building's progress bar.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BuildingStation))]
    public sealed class BuildingWorkAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip startClip;
        [SerializeField] private AudioClip loopClip;
        [SerializeField] private AudioClip endClip;
        [SerializeField, Range(0f, 1f)] private float volume = .5f;

        private BuildingStation station;
        private FactoryProduction factory;
        private AudioSource startSource, loopSource, endSource;
        private bool working;

        private void Awake()
        {
            station = GetComponent<BuildingStation>();
            factory = GetComponent<FactoryProduction>();
            startSource = CreateSource(startClip, false);
            loopSource = CreateSource(loopClip, true);
            endSource = CreateSource(endClip, false);
        }

        private AudioSource CreateSource(AudioClip clip, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.bypassReverbZones = true;
            if (clip != null) clip.LoadAudioData();
            return source;
        }

        private void LateUpdate() => Refresh();

        // Called by the work controllers too, so completion and cancellation stop the loop immediately.
        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            var game = station != null && station.Card != null ? station.Card.Owner : null;
            if (game == null || !game.isActiveAndEnabled || !station.isActiveAndEnabled ||
                factory != null && !factory.isActiveAndEnabled || Time.timeScale <= 0f ||
                game.ActionsLocked && !game.IsTutorialActive)
            {
                StopImmediately();
                return;
            }

            bool shouldWork = !game.ActionsLocked && (factory != null
                ? factory.PendingBlueprint != null && factory.WorkRate > 0f && !game.IsChoosingBlueprint
                : station.HasActiveWork);
            if (shouldWork == working) return;
            if (shouldWork) BeginWork();
            else EndWork();
        }

        private void BeginWork()
        {
            StopImmediately();
            if (loopSource == null || loopSource.clip == null) return;
            double startsAt = AudioSettings.dspTime + .1d;
            if (startSource != null && startSource.clip != null)
            {
                double loopAt = startsAt + (double)startSource.clip.samples / startSource.clip.frequency;
                startSource.PlayScheduled(startsAt);
                startSource.SetScheduledEndTime(loopAt);
                loopSource.PlayScheduled(loopAt);
            }
            else loopSource.PlayScheduled(startsAt);
            working = true;
        }

        private void EndWork()
        {
            // Stop cancels a scheduled loop even when the job finishes during its intro.
            if (startSource != null) startSource.Stop();
            if (loopSource != null) loopSource.Stop();
            working = false;
            if (endSource != null && endSource.clip != null) endSource.Play();
        }

        private void StopImmediately()
        {
            if (startSource != null) startSource.Stop();
            if (loopSource != null) loopSource.Stop();
            if (endSource != null) endSource.Stop();
            working = false;
        }

        private void OnDisable() => StopImmediately();
    }
}
