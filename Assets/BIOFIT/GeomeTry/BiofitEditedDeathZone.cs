using UnityEngine;
namespace Biofit.GeomeTryOriginal
{
    // Only attached to explicitly edited lethal objects, never to legacy hazards.
    public sealed class BiofitEditedDeathZone : MonoBehaviour
    {
        public int deathMask=15;
        public static bool Applies(Cube cube,int mask)
        {
            var motor=cube.GetComponent<BiofitModeMotor>();
            var mode=motor==null?BiofitMode.Cube:motor.Mode;
            int bit=mode==BiofitMode.Ball?2:mode==BiofitMode.Spider?4:mode==BiofitMode.Ship?8:1;
            return (mask&bit)!=0;
        }
        private void OnTriggerEnter2D(Collider2D other){Evaluate(other);}
        private void OnTriggerStay2D(Collider2D other){Evaluate(other);}
        private void Evaluate(Collider2D other)
        {
            var cube=other.GetComponent<Cube>();
            if(cube!=null && cube.isAlive && GameManager.Instance!=null && GameManager.Instance.PlayMode && Applies(cube,deathMask)) cube.Die();
        }
    }
}
