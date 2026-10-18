using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CivilGameJam.Menu
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-1000)]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private string mainMenuScene = "Assets/Scenes/MainMenuBackup.unity";
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private TMP_Text masterVolumeLabel;

        private float previousTimeScale;
        private bool previousAudioPause;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private GameObject previousSelection;

        public bool IsPaused { get; private set; }
        public bool IsLoading { get; private set; }

        private void Awake()
        {
            RefreshMasterVolume();
            pausePanel.SetActive(false);
        }

        private void RefreshMasterVolume()
        {
            GameAudioSettings.ApplySavedVolume();
            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(GameAudioSettings.MasterVolume);
            UpdateVolumeLabel(GameAudioSettings.MasterVolume);
        }

        public void SetMasterVolume(float volume)
        {
            GameAudioSettings.SetMasterVolume(volume);
            UpdateVolumeLabel(GameAudioSettings.MasterVolume);
        }

        private void UpdateVolumeLabel(float volume)
        {
            if (masterVolumeLabel != null) masterVolumeLabel.text = Mathf.RoundToInt(volume * 100f) + "%";
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ToggleMenu();
        }

        public void ToggleMenu()
        {
            if (IsLoading) return;
            if (IsPaused) ContinueGame();
            else PauseGame();
        }

        public void PauseGame()
        {
            if (IsPaused || IsLoading) return;
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshMasterVolume();
            pausePanel.SetActive(true);
            Select(continueButton.gameObject);
        }

        public void ContinueGame()
        {
            if (!IsPaused || IsLoading) return;
            RestoreGameplay();
            pausePanel.SetActive(false);
            GameAudioSettings.Save();
            Select(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        }

        public void BackToMainMenu()
        {
            if (!IsPaused || IsLoading) return;
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogError("Add the main menu scene to Build Settings: " + mainMenuScene, this);
                return;
            }
            IsLoading = true;
            continueButton.interactable = mainMenuButton.interactable = false;
            if (masterVolumeSlider != null) masterVolumeSlider.interactable = false;
            GameAudioSettings.Save();
            RestoreGameplay();
            Select(null);
            SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Single);
        }

        private void RestoreGameplay()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private static void Select(GameObject target)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
        }

        private void OnDisable()
        {
            RestoreGameplay();
            if (pausePanel != null) pausePanel.SetActive(false);
            GameAudioSettings.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) GameAudioSettings.Save();
        }
    }
}
