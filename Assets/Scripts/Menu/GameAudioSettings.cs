using UnityEngine;

namespace CivilGameJam.Menu
{
    /// <summary>One master volume for the menu and every gameplay audio source.</summary>
    public static class GameAudioSettings
    {
        public const string MasterVolumeKey = "CivilGameJam.Audio.MasterVolume";

        public static float MasterVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplySavedVolume()
        {
            AudioListener.volume = MasterVolume;
        }

        public static void SetMasterVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(MasterVolumeKey, volume);
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
