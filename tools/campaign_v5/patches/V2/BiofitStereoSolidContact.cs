using UnityEngine;
namespace Biofit.GeomeTryOriginal
{
    // Only imported solid objects. No death trigger on a walkable face.
    public sealed class BiofitStereoSolidContact : MonoBehaviour
    {
        private BoxCollider2D surface;
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
            // Ship contact with a solid obstacle is fatal; original floor and roof are separate.
            if(motor!=null && motor.Mode==BiofitMode.Ship) { Kill(cube,"ship-solid"); return; }
            // Tolerance for solver contact offsets and adjacent tile seams.
            if(a.min.y>=b.max.y-.075f && a.center.y>b.center.y) return;
            bool side=false, underside=false;
            for(int i=0;i<c.contactCount;i++)
            {
                var hit=c.GetContact(i);
                if(Mathf.Abs(hit.normal.x)>.65f && hit.point.y<b.max.y-.075f) side=true;
                if(Mathf.Abs(hit.normal.y)>.65f && a.center.y<b.center.y && hit.point.y<=b.min.y+.075f) underside=true;
            }
            float overlap=Mathf.Min(a.max.y,b.max.y)-Mathf.Max(a.min.y,b.min.y);
            if((side && overlap>.08f) || underside) Kill(cube,side?"side":"underside");
        }
        private void Kill(Cube cube,string reason)
        {
            Debug.Log("BIOFIT Stereo collision V2: "+reason+" "+name+" x="+cube.transform.position.x.ToString("F3"));
            cube.Die();
        }
    }
    // Diagnostic only; never alters the existing spike/pit death collider.
    public sealed class BiofitStereoHazardTrace : MonoBehaviour
    {
        public int id,index;
        private void OnTriggerEnter2D(Collider2D other)
        {
            var cube=other.GetComponent<Cube>();
            if(cube!=null) Debug.Log("BIOFIT Stereo hazard V2: ID="+id+" index="+index+" playerX="+cube.transform.position.x.ToString("F3"));
        }
    }
}
