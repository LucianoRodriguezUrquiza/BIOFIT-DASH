using UnityEngine;
namespace Biofit.GeomeTryOriginal
{
    // Physics-only adapter for new imported objects. Never modifies the BLE detector.
    public sealed class BiofitV5Interaction : MonoBehaviour
    {
        public string kind;
        private Cube used;
        public static bool IsOrbKind(string value) => value=="orb" || value=="pinkOrb" || value=="redOrb" || value=="blueOrb" || value=="greenOrb";
        private bool IsOrb => IsOrbKind(kind);
        private bool IsPad => kind=="pad" || kind=="pinkPad" || kind=="redPad" || kind=="gravityPad";
        public const float PinkOrbImpulse=12.5f, RedOrbImpulse=9.5f, GreenOrbImpulse=13.5f;
        private void OnTriggerEnter2D(Collider2D other) { if(!IsOrb) Activate(other); }
        private void OnTriggerStay2D(Collider2D other)
        {
            if(!IsOrb) { if(kind=="gravityUp"||kind=="gravityDown") Activate(other); return; }
            Cube cube=other.GetComponent<Cube>(); if(cube==null || !cube.isAlive || used==cube) return;
            var motor=cube.GetComponent<BiofitModeMotor>();
            // Ball/Spider use their discrete TryPulse path instead of held
            // input here. One action must not both activate an orb and flip/teleport.
            if(motor==null || !motor.IsCubeMode) return;
            if(Input.GetKey(KeyCode.Space) || (Input.GetMouseButton(0) && !BiofitStereoPracticeButton.BlocksGameplayPointer()) || BiofitVrBoxInput.AnyHeld || (motor.Mode==BiofitMode.Cube && BiofitGeomeTryLegacyBridge.TryConsumeOrbPulse())) Activate(other);
        }
        private void Activate(Collider2D other)
        {
            var cube=other.GetComponent<Cube>(); if(cube==null || !cube.isAlive || used==cube || GameManager.Instance==null || !GameManager.Instance.PlayMode) return;
            var motor=cube.GetComponent<BiofitModeMotor>();var rb=cube.GetComponent<Rigidbody2D>();if(motor==null || rb==null) return;
            // Size/gravity portals remain Cube-only. Only pads and orbs
            // are extended to Ball/Spider; Ship and other kinds are untouched.
            if(!motor.IsCubeMode && !(motor.IsBallSpiderMode && (IsPad||IsOrb))) return;
            used=cube;
            if(kind=="miniPortal"||kind=="normalSizePortal") {motor.SetMini(kind=="miniPortal");return;}
            if(kind=="gravityUp"||kind=="gravityDown") {motor.SetCubeGravity(kind=="gravityUp");return;}
            if(kind=="gravityPad")
            {
                if(motor.IsCubeMode) motor.SetCubeGravity(!motor.StereoInverted);
                else
                {
                    motor.SetBallSpiderGravity(!motor.StereoInverted);
                    rb.linearVelocity=new Vector2(rb.linearVelocity.x,0f);
                }
                return;
            }
            if(kind=="blueOrb"||kind=="greenOrb")
            {
                // Keep the vehicle and size; use its own gravity state.
                // Blue starts falling under the new gravity; green adds an initial
                // velocity toward the new supporting surface.
                bool invert=!motor.StereoInverted;
                if(motor.IsCubeMode) motor.SetCubeGravity(invert);
                else motor.SetBallSpiderGravity(invert);
                rb.linearVelocity=new Vector2(rb.linearVelocity.x,kind=="greenOrb"?(invert?1f:-1f)*GreenOrbImpulse:0f);
                return;
            }
            if(kind=="pinkOrb"||kind=="redOrb")
            {
                rb.linearVelocity=new Vector2(rb.linearVelocity.x,(motor.StereoInverted?-1f:1f)*(kind=="pinkOrb"?PinkOrbImpulse:RedOrbImpulse));
                return;
            }
            // GeomeTry yellow pad/orb 20; pink pad 12.5. Opposite of current gravity.
            rb.linearVelocity=new Vector2(rb.linearVelocity.x,(motor.StereoInverted?-1f:1f)*(kind=="redPad"?9.5f:kind=="pinkPad"?12.5f:20f));
        }
        // Ball/Spider require actual contact with the orb trigger; no extra
        // activation radius, queued input or automatic gravity action.
        public bool TryBallSpiderOrb(Cube cube)
        {
            if(!IsOrb || cube==null || used==cube || !cube.isAlive) return false;
            var motor=cube.GetComponent<BiofitModeMotor>();
            if(motor==null || !motor.IsBallSpiderMode) return false;
            var col=cube.GetComponent<Collider2D>();
            var trigger=GetComponent<Collider2D>();
            if(col==null || trigger==null || !trigger.isTrigger || !trigger.enabled || !col.enabled) return false;
            var distance=trigger.Distance(col);
            if(!distance.isValid || distance.distance>0f) return false;
            Activate(col);
            return used==cube;
        }
        public bool TryInvertedOrb(Cube cube)
        {
            if(!IsOrb || used==cube || cube==null || !cube.isAlive) return false;
            var col=cube.GetComponent<Collider2D>();if(col==null) return false;
            Activate(col);return used==cube;
        }
    }
}
