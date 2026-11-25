using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Biofit.Game.UI
{
    [DisallowMultipleComponent]
    public class BiofitMenuPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressedScale = 0.92f;
        [SerializeField] private float animationTime = 0.08f;
        [SerializeField] private bool useUnscaledTime = true;

        private Vector3 _initialScale;
        private Coroutine _running;

        private void Awake()
        {
            _initialScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (_initialScale == Vector3.zero)
                _initialScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            AnimateTo(_initialScale * pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            AnimateTo(_initialScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateTo(_initialScale);
        }

        private void AnimateTo(Vector3 target)
        {
            if (_running != null)
                StopCoroutine(_running);
            _running = StartCoroutine(AnimateScale(target));
        }

        private IEnumerator AnimateScale(Vector3 target)
        {
            Vector3 start = transform.localScale;
            float t = 0f;
            while (t < animationTime)
            {
                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float k = animationTime > 0f ? Mathf.Clamp01(t / animationTime) : 1f;
                transform.localScale = Vector3.Lerp(start, target, 1f - Mathf.Pow(1f - k, 3f));
                yield return null;
            }
            transform.localScale = target;
            _running = null;
        }
    }
}
