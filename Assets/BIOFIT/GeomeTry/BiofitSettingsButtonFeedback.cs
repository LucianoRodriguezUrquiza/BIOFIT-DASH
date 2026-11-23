using UnityEngine;
using UnityEngine.EventSystems;

namespace Biofit.GeomeTryOriginal
{
    /// <summary>
    /// Visible touch/mouse feedback for the six opaque Settings buttons.
    /// Pure presentation: the existing Unity UI Button remains responsible
    /// for invoking exactly one listener, with no BLE or capture side effects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BiofitSettingsButtonFeedback : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private RectTransform frame;
        private bool held;
        private float bounceUntil;

        private void Awake()
        {
            frame = transform as RectTransform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            held = true;
            bounceUntil = 0f;
            if (frame != null) frame.localScale = Vector3.one * .92f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            held = false;
            bounceUntil = Time.unscaledTime + .11f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!held) return;
            held = false;
            bounceUntil = 0f;
        }

        private void Update()
        {
            if (frame == null) return;
            float target = held ? .92f : Time.unscaledTime < bounceUntil ? 1.04f : 1f;
            float blend = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
            frame.localScale = Vector3.Lerp(frame.localScale, Vector3.one * target, blend);
        }

        private void OnDisable()
        {
            held = false;
            bounceUntil = 0f;
            if (frame != null) frame.localScale = Vector3.one;
        }
    }
}
