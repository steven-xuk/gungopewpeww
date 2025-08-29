using UnityEngine;

public class Shooter : MonoBehaviour
{
    public Transform firePoint;
    public bool canShoot = true;
    public ParticleSystem muzzleFlash;
    public GameObject hitEffect;

    [SerializeField] float explosionForce = 10;
    [SerializeField] float explosionRadius = 10;
    Collider[] colliders = new Collider[100];

    // Update is called once per frame
    void Update()
    {
        // New Input System (VR Right Controller)
        var triggerButton = UnityEngine.InputSystem.InputSystem.FindControl("<XRController>{RightHand}/triggerPressed") as UnityEngine.InputSystem.Controls.ButtonControl;
        var triggerAxis = UnityEngine.InputSystem.InputSystem.FindControl("<XRController>{RightHand}/trigger") as UnityEngine.InputSystem.Controls.AxisControl;

        bool triggerHeld = (triggerButton != null && triggerButton.isPressed) ||
                           (triggerAxis != null && triggerAxis.ReadValue() >= 0.5f);

        // Shooting
        if (triggerHeld && canShoot)
        {
            Shoot();
        }
    }

    public void Shoot()
    {
        RaycastHit hitInfo;
        canShoot = false;
        muzzleFlash.Play(true);
        bool hit = Physics.Raycast(firePoint.position, firePoint.forward, out hitInfo);
        if (hit)
        {
            Debug.Log(hitInfo.collider.gameObject.name);
            ExplodeNonAlloc(hitInfo.point);
            Instantiate(hitEffect, hitInfo.point, hitEffect.transform.rotation);
        }
        Invoke("EnableShooting", 0.5f);
    }

    void ExplodeNonAlloc(Vector3 position)
    {
        int numColliders = Physics.OverlapSphereNonAlloc(position, explosionRadius, colliders);

        if (numColliders > 0)
        {
            for (int i = 0; i < numColliders; i++)
            {
                if (colliders[i].TryGetComponent(out Rigidbody rb))
                {
                    rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
                }
            }
        }
    }

    void EnableShooting()
    {
        canShoot = true;
    }
}
