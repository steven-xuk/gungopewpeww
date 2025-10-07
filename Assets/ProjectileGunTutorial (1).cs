using UnityEngine;
using TMPro;

public class ProjectileGunTutorial : MonoBehaviour
{
    //bullet 
    public GameObject bullet;

    //bullet force
    public float shootForce, upwardForce;
    public AudioSource gunShootSound;

    //Gun stats
    public float timeBetweenShooting, spread, reloadTime, timeBetweenShots;
    public int magazineSize, bulletsPerTap;
    public bool allowButtonHold;

    int bulletsLeft, bulletsShot;

    //Recoil
    public Rigidbody playerRb;
    public float recoilForce;

    //bools
    bool shooting, readyToShoot, reloading;

    //References
    // Keep the camera serialized for VR rigs/prefabs, but we won't use it for aiming.
    public Camera fpsCam;
    public Transform attackPoint; // muzzle/barrel tip

    //Graphics
    public GameObject muzzleFlash;
    public TextMeshProUGUI ammunitionDisplay;

    //bug fixing :D
    public bool allowInvoke = true;

    private void Awake()
    {
        //make sure magazine is full
        bulletsLeft = magazineSize;
        readyToShoot = true;
    }

    private void Update()
    {
        MyInput();

        //Set ammo display, if it exists :D
        if (ammunitionDisplay != null)
            ammunitionDisplay.SetText(bulletsLeft / bulletsPerTap + " / " + magazineSize / bulletsPerTap);
    }

    private void MyInput()
    {
        // New Input System (VR Right Controller)
        var triggerButton = UnityEngine.InputSystem.InputSystem.FindControl("<XRController>{RightHand}/triggerPressed") as UnityEngine.InputSystem.Controls.ButtonControl;
        var triggerAxis   = UnityEngine.InputSystem.InputSystem.FindControl("<XRController>{RightHand}/trigger")         as UnityEngine.InputSystem.Controls.AxisControl;
        var reloadButton  = UnityEngine.InputSystem.InputSystem.FindControl("<XRController>{RightHand}/primaryButton")   as UnityEngine.InputSystem.Controls.ButtonControl;

        bool triggerHeld = (triggerButton != null && triggerButton.isPressed) ||
                           (triggerAxis   != null && triggerAxis.ReadValue() >= 0.5f);

        // Check if allowed to hold down button and take corresponding input
        if (allowButtonHold) shooting = triggerHeld;
        else shooting = triggerButton != null && triggerButton.wasPressedThisFrame;

        // Reloading
        if (reloadButton != null && reloadButton.wasPressedThisFrame && bulletsLeft < magazineSize && !reloading) Reload();

        // Reload automatically when trying to shoot without ammo
        if (readyToShoot && shooting && !reloading && bulletsLeft <= 0) Reload();

        // Shooting
        if ((readyToShoot && shooting && !reloading && bulletsLeft > 0))
        {
            // Set bullets shot to 0
            bulletsShot = 0;
            Shoot();
        }
    }

    private void Shoot()
    {
        readyToShoot = false;

        // === Aim strictly along the gun's orientation ===
        Vector3 forward = attackPoint.forward; // local Z in world space
        Vector3 right   = attackPoint.right;
        Vector3 up      = attackPoint.up;

        // Spread (in the plane perpendicular to forward)
        float x = Random.Range(-spread, spread);
        float y = Random.Range(-spread, spread);
        Vector3 directionWithSpread = (forward + right * x + up * y).normalized;

        //Instantiate bullet/projectile
        Quaternion bulletRotation = Quaternion.LookRotation(directionWithSpread, up);
        GameObject currentBullet = Instantiate(bullet, attackPoint.position, bulletRotation);

        //Add forces to bullet
        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        rb.AddForce(directionWithSpread * shootForce, ForceMode.Impulse);
        rb.AddForce(up * upwardForce, ForceMode.Impulse); // use gun's up, not camera

        gunShootSound.Play(0);

        //Instantiate muzzle flash, if you have one
        if (muzzleFlash != null)
            Instantiate(muzzleFlash, attackPoint.position, attackPoint.rotation);

        bulletsLeft--;
        bulletsShot++;

        //Invoke resetShot function (if not already invoked), with your timeBetweenShooting
        if (allowInvoke)
        {
            Invoke("ResetShot", timeBetweenShooting);
            allowInvoke = false;

            //Add recoil to player (should only be called once)
            playerRb.AddForce(-directionWithSpread * recoilForce, ForceMode.Impulse);
        }

        //if more than one bulletsPerTap make sure to repeat shoot function
        if (bulletsShot < bulletsPerTap && bulletsLeft > 0)
            Invoke("Shoot", timeBetweenShots);
    }

    private void ResetShot()
    {
        //Allow shooting and invoking again
        readyToShoot = true;
        allowInvoke = true;
    }

    private void Reload()
    {
        reloading = true;
        Invoke("ReloadFinished", reloadTime); //Invoke ReloadFinished function with your reloadTime as delay
    }

    private void ReloadFinished()
    {
        //Fill magazine
        bulletsLeft = magazineSize;
        reloading = false;
    }
}
