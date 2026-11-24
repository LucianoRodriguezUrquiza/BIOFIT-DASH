using UnityEngine;
namespace Biofit.GeomeTryOriginal
{
    // Imported solids only. Never suppress spike/pit triggers or front impacts.
    public sealed class BiofitStereoSolidContact : MonoBehaviour
    {
        private BoxCollider2D surface;
        private bool shipEntrySupport;
        public void Configure(bool entrySupport) { shipEntrySupport=entrySupport; }
        private void Awake() { surface=GetComponent<BoxCollider2D>(); }
        private void OnCollisionEnter2D(Collision2D c) { Evaluate(c); }
        private void OnCollisionStay2D(Collision2D c) { Evaluate(c); }
        private void Evaluate(Collision2D c)
        {
            var cube=c.gameObject.GetComponent<Cube>();
            if(cube==null || !cube.isAlive || surface==null) return;
            var body=cube.GetComponent<Collider2D>();
            if(body==null) return;
            var a=body.bounds; var b=surface.bounds;
            var motor=cube.GetComponent<BiofitModeMotor>();
            bool ship=motor!=null && motor.Mode==BiofitMode.Ship;
            // A valid upper-face contact also covers seams between coplanar tiles.
            // Never extend this exception to an obstacle ahead of the avatar.
            float overlap=Mathf.Min(a.max.y,b.max.y)-Mathf.Max(a.min.y,b.min.y);
            bool top=a.center.y>b.center.y && a.min.y>=b.max.y-.10f && overlap<=.10f;
            bool reverseMode=motor!=null && (motor.Mode==BiofitMode.Ball || motor.Mode==BiofitMode.Spider);
            bool bottom=a.center.y<b.center.y && a.max.y<=b.min.y+.10f && overlap<=.10f;
            // Both faces are legitimate support during a gravity flip; lateral impacts stay lethal.
            if(reverseMode && (top || bottom)) return;
            if(top && (!ship || shipEntrySupport)) return;
            if(ship) { Kill(cube,shipEntrySupport?"ship-entry-side-or-bottom":"ship-solid"); return; }
            bool side=false, underside=false;
            for(int i=0;i<c.contactCount;i++)
            {
                var hit=c.GetContact(i);
                if(Mathf.Abs(hit.normal.x)>.65f && hit.point.y<b.max.y-.075f) side=true;
                if(Mathf.Abs(hit.normal.y)>.65f && a.center.y<b.center.y && hit.point.y<=b.min.y+.075f) underside=true;
            }
            if((side && overlap>.08f) || (underside && !reverseMode)) Kill(cube,side?"side":"underside");
        }
        private void Kill(Cube cube,string reason)
        {
            var motor=cube.GetComponent<BiofitModeMotor>();
            Debug.LogWarning("BIOFIT Stereo collision V3: "+reason+" "+name+" mode="+(motor==null?"Cube":motor.Mode.ToString())+" playerX="+cube.transform.position.x.ToString("F3")+" playerY="+cube.transform.position.y.ToString("F3")+" surfaceY="+surface.bounds.max.y.ToString("F3"));
            cube.Die();
        }
    }
    public sealed class BiofitStereoHazardTrace : MonoBehaviour
    {
        public int id,index;
        private void OnTriggerEnter2D(Collider2D other)
        {
            var cube=other.GetComponent<Cube>();
            if(cube!=null && cube.isAlive) Debug.LogWarning("BIOFIT Stereo hazard V3: ID="+id+" index="+index+" playerX="+cube.transform.position.x.ToString("F3")+" playerY="+cube.transform.position.y.ToString("F3"));
        }
    }
}
