using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Biofit.GeomeTryOriginal
{
    // Independent practice input: press, not release, and a polling fallback
    // for Android / editor pointer modules. Preserve the texture aspect ratio.
    public sealed class BiofitStereoPracticeButton : MonoBehaviour, IPointerDownHandler
    {
        private static readonly List<BiofitStereoPracticeButton> ActiveButtons = new List<BiofitStereoPracticeButton>();
        private static bool captured;
        private bool place;
        private int lastPressFrame=-1;
        private RectTransform area;
        public static void Create(Transform parent, Vector2 anchor, Vector2 maxSize, Texture2D art, bool add)
        {
            var go=new GameObject(add?"V3 Place checkpoint":"V3 Remove checkpoint",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
            var rt=go.GetComponent<RectTransform>(); rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=anchor; rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=Vector2.zero;
            var raw=go.GetComponent<RawImage>();raw.raycastTarget=true;
            rt.sizeDelta=maxSize;
            raw.texture=Resources.Load<Texture2D>("BiofitV63/checkpoint_frame");
            var iconTexture=Resources.Load<Texture2D>("BiofitStereoV2/UI/checkpoint");
            if(iconTexture!=null)
            {
                var icon=new GameObject("Checkpoint diamond",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
                var ir=icon.GetComponent<RectTransform>();ir.SetParent(rt,false);
                ir.anchorMin=ir.anchorMax=new Vector2(.5f,.5f);ir.pivot=new Vector2(.5f,.5f);ir.anchoredPosition=Vector2.zero;
                float k=Mathf.Min(46f/iconTexture.width,62f/iconTexture.height);
                ir.sizeDelta=new Vector2(iconTexture.width*k,iconTexture.height*k);
                var img=icon.GetComponent<RawImage>();img.texture=iconTexture;img.raycastTarget=false;
            }
            if(!add)
            {
                var slash=new GameObject("Remove checkpoint symbol",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
                var sr=slash.GetComponent<RectTransform>();sr.SetParent(rt,false);sr.anchorMin=sr.anchorMax=new Vector2(.5f,.5f);sr.pivot=new Vector2(.5f,.5f);sr.anchoredPosition=Vector2.zero;sr.sizeDelta=new Vector2(65f,65f);
                var im=slash.GetComponent<RawImage>();im.texture=Resources.Load<Texture2D>("BiofitV63/checkpoint_remove");im.raycastTarget=false;
            }
            go.AddComponent<BiofitSettingsButtonFeedback>();
            var button=go.AddComponent<BiofitStereoPracticeButton>();button.area=rt;button.place=add;
        }
        private void OnEnable() { if(!ActiveButtons.Contains(this)) ActiveButtons.Add(this); }
        private void OnDisable() { ActiveButtons.Remove(this);if(ActiveButtons.Count==0) captured=false; }
        private bool Hit(Vector2 point) => area!=null && isActiveAndEnabled && RectTransformUtility.RectangleContainsScreenPoint(area,point,null);
        private static bool OverAny(Vector2 point)
        {
            foreach(var button in ActiveButtons) if(button!=null && button.Hit(point)) return true;
            return false;
        }
        // Scope: only a pointer press over checkpoint controls, while in practice.
        // Keyboard, VR BOX and BLE input paths are not affected.
        public static bool BlocksGameplayPointer()
        {
            if(!BiofitStereoPractice.Enabled || ActiveButtons.Count==0) {captured=false;return false;}
            if(Input.GetMouseButtonDown(0)) captured=OverAny(Input.mousePosition);
            for(int i=0;i<Input.touchCount;i++)
            {
                var touch=Input.GetTouch(i);
                if(touch.phase==TouchPhase.Began && OverAny(touch.position)) captured=true;
            }
            if(!Input.GetMouseButton(0) && Input.touchCount==0) captured=false;
            return captured || (Input.GetMouseButton(0) && OverAny(Input.mousePosition));
        }
        private void Press()
        {
            if(!BiofitStereoPractice.Enabled || Time.timeScale<=0f || GameManager.Instance==null || !GameManager.Instance.PlayMode || lastPressFrame==Time.frameCount) return;
            lastPressFrame=Time.frameCount;
            captured=true;
            Debug.Log("BIOFIT V3 checkpoint pointer: "+(place?"ADD":"REMOVE"));
            if(place) BiofitStereoPractice.Place();else BiofitStereoPractice.Remove();
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if(eventData.button==PointerEventData.InputButton.Left) Press();
        }
        private void Update()
        {
            if(Input.touchCount>0)
            {
                for(int i=0;i<Input.touchCount;i++)
                {
                    var t=Input.GetTouch(i);
                    if(t.phase==TouchPhase.Began && Hit(t.position)) Press();
                }
            }
            else if(Input.GetMouseButtonDown(0) && Hit(Input.mousePosition)) Press();
        }
    }
}
