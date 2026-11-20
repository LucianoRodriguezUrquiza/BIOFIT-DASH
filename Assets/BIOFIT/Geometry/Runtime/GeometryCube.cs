using System;
using UnityEngine;

namespace Biofit.Geometry
{
    // Derived from GeomeTry Character/Cube (MIT). Rigidbody motion replaces
    // transform.Translate; edge-triggered TryJump replaces held keyboard input.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class GeometryCube : MonoBehaviour, IGeometryJumpTarget
    {
        public Rigidbody2D Body { get; private set; }
        public bool Running { get; private set; }
        public bool Alive { get; private set; } = true;
        public event Action Died;
        public event Action<string> AutomaticAction;
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[12];
        private GeometryWorldItem orb;
        private Transform artwork;

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Body.gravityScale = 0; // Explicit local acceleration; never edit global Physics2D settings.
            Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            GetComponent<BoxCollider2D>().size = Vector2.one * GeometryPhysics.CubeSize;
        }
        public void Initialize(Transform visual) { artwork = visual; SetRunning(false); }
        public void SetRunning(bool running) { Running = running && Alive; Body.simulated = Running; }
        public bool Grounded
        {
            get
            {
                if (!Alive || Body.linearVelocity.y > .1f) return false;
                int count = Body.Cast(Vector2.down, groundHits, .055f);
                for (int i=0;i<count;i++)
                {
                    var hit = groundHits[i];
                    var item = hit.collider == null ? null : hit.collider.GetComponent<GeometryWorldItem>();
                    if (item != null && item.Solid && hit.normal.y > .65f) return true;
                }
                return false;
            }
        }
        public JumpOutcome TryJump()
        {
            if (!Running || !Alive) return JumpOutcome.Inactive;
            if (orb != null && orb.TryConsume())
            {
                Launch(); return JumpOutcome.ExecutedOrb;
            }
            if (!Grounded) return JumpOutcome.Airborne;
            Launch(); return JumpOutcome.Executed;
        }
        private void Launch()
        {
            Body.linearVelocity = new Vector2(GeometryPhysics.Speed, GeometryPhysics.JumpVelocity);
        }
        private void FixedUpdate() { StepPhysics(Time.fixedDeltaTime); }
        // Also used by the Unity Play-mode validator with a local PhysicsScene2D.
        public void StepPhysics(float dt)
        {
            if (!Running || !Alive) return;
            Body.linearVelocity = new Vector2(GeometryPhysics.Speed,
                Body.linearVelocity.y - GeometryPhysics.Gravity * dt);
            if (Body.position.y < -4) Die();
        }
        private void Update()
        {
            if (!Running || artwork == null) return;
            if (!Grounded) artwork.Rotate(0,0,-280f*Time.deltaTime);
            else artwork.rotation = Quaternion.Euler(0,0,Mathf.Round(artwork.eulerAngles.z/90)*90);
        }
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!Running) return;
            var item = collision.collider.GetComponent<GeometryWorldItem>();
            if (item == null || !item.Solid) return;
            for (int i=0;i<collision.contactCount;i++)
                if (Mathf.Abs(collision.GetContact(i).normal.x) > .6f) { Die(); return; }
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!Running) return;
            var item = other.GetComponent<GeometryWorldItem>();
            if (item == null) return;
            if (item.Kind == "spike") Die();
            else if (item.Kind == "pad" && item.TryConsume())
            {
                Launch(); AutomaticAction?.Invoke("PAD id="+item.Id);
            }
            else if (item.Kind == "orb") orb = item;
        }
        private void OnTriggerExit2D(Collider2D other)
        {
            if (orb != null && other.gameObject == orb.gameObject) orb = null;
        }
        public void Die()
        {
            if (!Alive) return;
            Alive = false; SetRunning(false); Died?.Invoke();
        }
    }
}
