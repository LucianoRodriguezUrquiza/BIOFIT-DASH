using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Biofit.GeomeTryOriginal
{
    public sealed class BiofitV2Hud : MonoBehaviour
    {
        public TMP_Text percentage, practiceStatus;
        public RectTransform bar;
        public RawImage[] coins;
        private RectTransform fill;
        private Texture2D emptyCoin, fullCoin;
        private int lastCoins=-1, lastTotal=-1;
        private Transform player,end;
        private void Start()
        {
            HideLegacy();
            emptyCoin=BiofitV2BitmapText.Art("coin_empty"); fullCoin=BiofitV2BitmapText.Art("coin_full");
            if(bar!=null)
            {
                var frame=bar.gameObject.AddComponent<RawImage>();frame.texture=BiofitV2BitmapText.Art("progress_frame");frame.raycastTarget=false;
                var bg=new GameObject("Progress fill",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
                fill=bg.GetComponent<RectTransform>(); fill.SetParent(bar,false);
                fill.anchorMin=new Vector2(0,0);fill.anchorMax=new Vector2(0,1);fill.pivot=new Vector2(0,.5f);fill.offsetMin=new Vector2(3,3);fill.offsetMax=new Vector2(3,-3);
                var image=bg.GetComponent<RawImage>();image.texture=Texture2D.whiteTexture;image.color=new Color32(112,235,0,255);image.raycastTarget=false;
                // Frame is rendered over fill so the white rounded border stays visible.
                var overlay=new GameObject("Progress border",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
                var rt=overlay.GetComponent<RectTransform>();rt.SetParent(bar,false);rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
                var o=overlay.GetComponent<RawImage>();o.texture=frame.texture;o.raycastTarget=false;frame.enabled=false;
            }
        }
        private static void HideLegacy()
        {
            var original=FindFirstObjectByType<ProgressBar>();
            if(original!=null) { original.enabled=false; if(original.textMesh!=null) original.textMesh.enabled=false; }
            var old=GameObject.Find("Progress Bar"); if(old!=null) old.SetActive(false);
            old=GameObject.Find("Progress Percentage"); if(old!=null) old.SetActive(false);
        }
        private void LateUpdate()
        {
            if(player==null) {var p=GameObject.FindGameObjectWithTag("Player");if(p!=null) player=p.transform;}
            if(end==null) {var e=GameObject.FindWithTag("EndOfMap");if(e!=null) end=e.transform;}
            if(player!=null && end!=null)
            {
                float value=Mathf.Clamp01((player.position.x-Character.InitialPosition)/Mathf.Max(1,end.position.x-Character.InitialPosition));
                if(percentage!=null) percentage.text=Mathf.FloorToInt(value*100)+"%";
                if(fill!=null) fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Max(0,(bar.rect.width-6)*value));
            }
            int count=BiofitStereoCoinPickup.Collected;
            int total=BiofitStereoLocalRuntime.TotalCoins;
            if(coins!=null && (count!=lastCoins || total!=lastTotal))
            {
                for(int i=0;i<coins.Length;i++) if(coins[i]!=null) { coins[i].gameObject.SetActive(i<total); coins[i].texture=i<count?fullCoin:emptyCoin; }
                lastCoins=count;lastTotal=total;
            }
            if(practiceStatus!=null) {
                string feedback=BiofitStereoPractice.Feedback;
                practiceStatus.text="PRACTICA  |  CP: "+BiofitStereoPractice.Count+"  |  AUTO: "+(BiofitStereoPractice.Auto?"SI":"NO")+(string.IsNullOrEmpty(feedback)?"":"  |  "+feedback);
            }
        }
    }
}
