using UnityEngine;
namespace Biofit.GeomeTryOriginal
{
    // Physics-only adapter for new imported objects. Never modifies the BLE detector.
    public sealed class BiofitV5Interaction : MonoBehaviour
    {
        public string kind;
        private Cube used;
        private bool IsOrb => kind=="orb";
        private void OnTriggerEnter2D(Collider2D other) { if(!IsOrb) Activate(other); }
        private void OnTriggerStay2D(Collider2D other)
        {
            if(!IsOrb) { if(kind=="gravityUp"||kind=="gravityDown") Activate(other); return; }
            Cube cube=other.GetComponent<Cube>(); if(cube==null || !cube.isAlive || used==cube) return;
            var motor=cube.GetComponent<BiofitModeMotor>();
            if(motor==null || (motor.Mode!=BiofitMode.Cube && motor.Mode!=BiofitMode.InvertedCube)) return;
            if(Input.GetKey(KeyCode.Space) || (Input.GetMouseButton(0) && !BiofitStereoPracticeButton.BlocksGameplayPointer()) || BiofitVrBoxInput.AnyHeld || (motor.Mode==BiofitMode.Cube && BiofitGeomeTryLegacyBridge.TryConsumeOrbPulse())) Activate(other);
        }
        private void Activate(Collider2D other)
        {
            var cube=other.GetComponent<Cube>(); if(cube==null || !cube.isAlive || used==cube || GameManager.Instance==null || !GameManager.Instance.PlayMode) return;
            var motor=cube.GetComponent<BiofitModeMotor>();var rb=cube.GetComponent<Rigidbody2D>();if(motor==null || rb==null) return;
            if(motor.Mode!=BiofitMode.Cube && motor.Mode!=BiofitMode.InvertedCube) return;
            used=cube;
            if(kind=="gravityUp"||kind=="gravityDown") {motor.SetMode(kind=="gravityUp"?BiofitMode.InvertedCube:BiofitMode.Cube);return;}
            if(kind=="gravityPad") {motor.SetMode(motor.Mode==BiofitMode.InvertedCube?BiofitMode.Cube:BiofitMode.InvertedCube);return;}
            // GeomeTry yellow pad/orb 20; pink pad 12.5. Opposite of current gravity.
            rb.linearVelocity=new Vector2(rb.linearVelocity.x,(motor.Mode==BiofitMode.InvertedCube?-1f:1f)*(kind=="pinkPad"?12.5f:20f));
        }
        public bool TryInvertedOrb(Cube cube)
        {
            if(!IsOrb || used==cube || cube==null || !cube.isAlive) return false;
            var col=cube.GetComponent<Collider2D>();if(col==null) return false;
            Activate(col);return used==cube;
        }
    }
}
