using UnityEngine;
namespace Biofit.GeomeTryOriginal
{
    public sealed class BiofitV63Finish : MonoBehaviour
    {
        private bool finished;
        public static void Install(GameObject wall)
        {
            foreach(var original in wall.GetComponentsInChildren<EndZone>(true))
            {
                var host=original.gameObject;
                if(host.GetComponent<BiofitV63Finish>()==null)host.AddComponent<BiofitV63Finish>();
                // Destroy the legacy handler, rather than disabling it: Unity can
                // send trigger messages even to disabled MonoBehaviours.
                Destroy(original);
            }
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if(finished || GameManager.Instance==null || !GameManager.Instance.PlayMode) return;
            var avatar=other.GetComponent<Character>();
            if(avatar==null || (avatar is Cube c && !c.isAlive)) return;
            finished=true;
            if(!BiofitGeometryUX.ShowV63Completion())
            {
                Debug.LogError("BIOFIT V6.3: no se encontro interfaz de meta.");
                GameManager.Instance.PlayMode=false;
                return;
            }
            Destroy(avatar.gameObject);
            Debug.Log("BIOFIT V6.3: nivel completado; esperando seleccion del usuario.");
        }
    }
    public sealed class BiofitV63CompletionAnimation : MonoBehaviour
    {
        private float elapsed;
        private CanvasGroup group;
        private void Awake(){group=gameObject.AddComponent<CanvasGroup>();group.alpha=0;transform.localScale=Vector3.one*.70f;group.interactable=false;}
        private void Update()
        {
            elapsed+=Time.unscaledDeltaTime;
            float t=Mathf.Clamp01(elapsed/.45f), e=1-Mathf.Pow(1-t,3);
            transform.localScale=Vector3.one*Mathf.Lerp(.70f,1f,e);group.alpha=e;
            if(t>=1){group.interactable=true;enabled=false;}
        }
    }
}
