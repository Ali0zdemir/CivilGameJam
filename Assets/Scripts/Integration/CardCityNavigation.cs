using CivilGameJam.Cards;
using UnityEngine;

namespace CivilGameJam
{
    // Only input ownership is shared. Card movement never changes the city's camera pose.
    [DefaultExecutionOrder(-200)]
    public sealed class CardCityNavigation : MonoBehaviour
    {
        [SerializeField] private CardGameController game;
        [SerializeField] private CityCameraController navigation;
        [SerializeField] private CanvasGroup testControls;

        private void Update()
        {
            if (navigation != null)
                navigation.InputBlocked = game != null && (game.IsDraggingAnyCard || game.IsChoosingBlueprint);
            if (testControls != null)
                testControls.interactable = testControls.blocksRaycasts = game == null || !game.IsChoosingBlueprint;
        }

        private void OnDisable()
        {
            if (navigation != null) navigation.InputBlocked = false;
            if (testControls != null) testControls.interactable = testControls.blocksRaycasts = true;
        }
    }
}
