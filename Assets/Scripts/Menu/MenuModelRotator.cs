using UnityEngine;

namespace CivilGameJam.Menu
{
    [DisallowMultipleComponent]
    public sealed class MenuModelRotator : MonoBehaviour
    {
        [SerializeField, Range(0f, 30f)] private float degreesPerSecond = 10f;

        private void Update()
        {
            transform.Rotate(Vector3.up, degreesPerSecond * Time.unscaledDeltaTime, Space.Self);
        }
    }
}
