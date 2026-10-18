using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CivilGameJam.Menu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string gameplayScene = "Assets/2DCardScene.unity";

        [Header("Saved menu UI")]
        [SerializeField] private CanvasGroup mainPanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Button playButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private TMP_Text playLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private TMP_Text masterVolumeLabel;

        public bool IsLoading { get; private set; }
        public bool OptionsOpen => optionsPanel != null && optionsPanel.activeSelf;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameAudioSettings.ApplySavedVolume();
            masterVolumeSlider.SetValueWithoutNotify(GameAudioSettings.MasterVolume);
            UpdateVolumeLabel(GameAudioSettings.MasterVolume);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            mainPanel.interactable = true;
            mainPanel.blocksRaycasts = true;
        }

        private void Start() => Select(playButton.gameObject);

        private void Update()
        {
            bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            back |= Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (back && OptionsOpen && !IsLoading) CloseOptions();
        }

        public void OpenOptions()
        {
            if (IsLoading || optionsPanel == null) return;
            optionsPanel.SetActive(true);
            mainPanel.interactable = false;
            mainPanel.blocksRaycasts = false;
            Select(masterVolumeSlider.gameObject);
        }

        public void CloseOptions()
        {
            if (optionsPanel != null) optionsPanel.SetActive(false);
            mainPanel.interactable = true;
            mainPanel.blocksRaycasts = true;
            GameAudioSettings.Save();
            Select(optionsButton != null ? optionsButton.gameObject : playButton.gameObject);
        }

        public void SetMasterVolume(float volume)
        {
            GameAudioSettings.SetMasterVolume(volume);
            UpdateVolumeLabel(GameAudioSettings.MasterVolume);
        }

        private void UpdateVolumeLabel(float volume)
        {
            masterVolumeLabel.text = Mathf.RoundToInt(volume * 100f) + "%";
        }

        public void PlayGame()
        {
            if (IsLoading || OptionsOpen) return;
            if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                statusLabel.text = "Game scene is not available.";
                Debug.LogError("Add the gameplay scene to Build Settings: " + gameplayScene, this);
                return;
            }
            GameAudioSettings.Save();
            IsLoading = true;
            mainPanel.interactable = false;
            playLabel.text = "Loading...";
            StartCoroutine(LoadGame());
        }

        private IEnumerator LoadGame()
        {
            // Let the loading label render before loading the city and its cards.
            yield return null;
            yield return SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single);
        }

        public void ExitGame()
        {
            if (IsLoading || OptionsOpen) return;
            GameAudioSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void Select(GameObject target)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) GameAudioSettings.Save();
        }

        private void OnDisable() => GameAudioSettings.Save();
    }
}
