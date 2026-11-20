using System;
using UnityEngine;

/// <summary>
/// Small, isolated port of the GeomeTry cube's essential mechanics:
/// automatic horizontal movement, a fixed vertical jump and airborne spin.
/// Source inspiration: Christ0u/GeomeTry, d922cadd, MIT; see docs license.
/// This is NOT the original prefab, animator or level manager.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class BiofitGeomeTryCube : MonoBehaviour
{
    public const float DefaultSpeed = 6.5f;
    public const float JumpVelocity = 13.5f;
    public const float GravityScale = 4f;
    public const float RotationDegreesPerSecond = 280f;

    public event Action Died;

    private Rigidbody2D body;
    private BoxCollider2D box;
    private BoxCollider2D ground;
    private Transform artwork;
    private SpriteRenderer drawing;

    public bool Running { get; set; }
    public bool Alive { get; private set; } = true;
    public float Speed => DefaultSpeed;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();
        body.gravityScale = GravityScale;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        box.size = Vector2.one;
    }

    public void Configure(BoxCollider2D groundCollider, SpriteRenderer spriteRenderer)
    {
        ground = groundCollider;
        drawing = spriteRenderer;
        artwork = spriteRenderer == null ? null : spriteRenderer.transform;
        Alive = true;
        Running = true;
    }

    public bool IsGrounded()
    {
        if (!Alive || ground == null || box == null || body == null) return false;
        Bounds player = box.bounds;
        Bounds floor = ground.bounds;
        float distance = player.min.y - floor.max.y;
        // A geometry-specific floor test; do not interpret MOV_OFF as landing.
        return distance > -0.13f && distance < 0.10f &&
               player.max.x > floor.min.x && player.min.x < floor.max.x &&
               body.linearVelocity.y <= 0.4f;
    }

    public bool TryJump()
    {
        if (!Running || !IsGrounded()) return false;
        // This is intentionally one impulse, NOT held Input.GetKey().
        body.linearVelocity = new Vector2(DefaultSpeed, JumpVelocity);
        return true;
    }

    private void FixedUpdate()
    {
        if (!Running || !Alive) return;
        // Drive via Rigidbody2D rather than transform.Translate so obstacle
        // collisions remain part of the physics simulation.
        body.linearVelocity = new Vector2(DefaultSpeed, body.linearVelocity.y);
    }

    private void Update()
    {
        if (!Alive || artwork == null) return;
        if (!IsGrounded())
            artwork.Rotate(0f, 0f, -RotationDegreesPerSecond * Time.deltaTime);
        else
        {
            float angle = artwork.eulerAngles.z;
            float snapped = Mathf.Round(angle / 90f) * 90f;
            artwork.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(angle, snapped, 0.25f));
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Alive || !Running || other == null) return;
        if (other.gameObject.name.StartsWith("BIOFIT_SPIKE_", StringComparison.Ordinal))
            Die();
    }

    private void Die()
    {
        Alive = false;
        Running = false;
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        if (drawing != null) drawing.enabled = false;
        Died?.Invoke();
    }
}
