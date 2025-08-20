using UnityEngine;

public class BulletCleanup : MonoBehaviour
{
    [Tooltip("Seconds before the bullet auto-destroys if it doesn't hit anything.")]
    public float maxLifetime = 5f;

    [Tooltip("Optional impact effect prefab (e.g., decal/particles).")]
    public GameObject impactVFX;

    private bool hasHit = false;

    private void Start()
    {
        // Auto-destroy after a while in case we never collide
        Destroy(gameObject, maxLifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        hasHit = true;

        // Spawn impact VFX at the contact point (if assigned)
        if (impactVFX != null && collision.contactCount > 0)
        {
            var contact = collision.GetContact(0);
            Instantiate(impactVFX, contact.point, Quaternion.LookRotation(contact.normal));
        }

        Destroy(gameObject);
    }

    // If your bullet uses a trigger collider instead of a solid collider,
    // this will still clean it up on contact.
    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        hasHit = true;

        if (impactVFX != null)
            Instantiate(impactVFX, transform.position, transform.rotation);

        Destroy(gameObject);
    }
}
