using UnityEngine;
using UnityEngine.EventSystems;

namespace CivilGameJam.Cards
{
    public sealed class CardInspectionDismiss : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private CardGameController game;
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) game.CloseInspection();
        }
    }
}
