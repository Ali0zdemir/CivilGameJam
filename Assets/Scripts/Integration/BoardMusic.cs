using CivilGameJam.Disasters;
using UnityEngine;

namespace CivilGameJam
{
    /// <summary>Plays the board intro once, then loops until the preparation turn ends.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(400)]
    public sealed class BoardMusic : MonoBehaviour
    {
        [SerializeField] private EarthquakeRoundController round;
        [SerializeField] private MonsterSequenceController sequence;
        [SerializeField] private AudioClip startClip;
        [SerializeField] private AudioClip loopClip;
        [SerializeField, Range(0f, 1f)] private float volume = .3f;

        private AudioSource startSource, loopSource;
        private bool playbackScheduled;

        private void Awake()
        {
            startSource = CreateSource(startClip, false);
            loopSource = CreateSource(loopClip, true);
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
            source.priority = 0;
            source.bypassReverbZones = true;
            if (clip != null) clip.LoadAudioData();
            return source;
        }

        private void OnEnable()
        {
            if (round != null) round.TurnStarted += StopPlayback;
        }

        private void Update()
        {
            // Tutorial locks and 3D upgrade presentations still belong to preparation.
            bool preparing = round != null && round.isActiveAndEnabled && round.Owner != null &&
                round.Owner.isActiveAndEnabled && !round.IsRoundActive && !round.IsResultsVisible &&
                (sequence == null || !sequence.IsRunning);
            if (preparing) StartPlayback();
            else if (playbackScheduled) StopPlayback();
        }

        private void StartPlayback()
        {
            if (playbackScheduled || loopSource == null || loopSource.clip == null) return;
            double startsAt = AudioSettings.dspTime + .1d;
            if (startSource != null && startSource.clip != null)
            {
                // Schedule both voices on the audio clock so the intro joins the loop without a frame gap.
                double loopAt = startsAt + (double)startSource.clip.samples / startSource.clip.frequency;
                startSource.PlayScheduled(startsAt);
                startSource.SetScheduledEndTime(loopAt);
                loopSource.PlayScheduled(loopAt);
            }
            else loopSource.PlayScheduled(startsAt);
            playbackScheduled = true;
        }

        private void StopPlayback()
        {
            // Stop also cancels the pending loop when READY is pressed during the intro.
            if (startSource != null) startSource.Stop();
            if (loopSource != null) loopSource.Stop();
            playbackScheduled = false;
        }

        private void OnDisable()
        {
            if (round != null) round.TurnStarted -= StopPlayback;
            StopPlayback();
        }
    }
}
