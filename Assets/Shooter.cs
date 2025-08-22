using UnityEngine;

public class Shooter : MonoBehaviour
{
    public Transform firePoint;

    public GameObject hitEffect;

    // Update is called once per frame
    void Update()
    {
        // New Input System (VR Right Controller)
        var triggerButton = UnityEngine.InputSystem.InputSystem.FindControl("<XRController>{RightHand}/triggerPressed") as UnityEngine.InputSystem.Controls.ButtonControl;
        var triggerAxis = UnityEngine.InputSystem.InputSystem.FindControl("<XRController>{RightHand}/trigger") as UnityEngine.InputSystem.Controls.AxisControl;

        bool triggerHeld = (triggerButton != null && triggerButton.isPressed) ||
                           (triggerAxis != null && triggerAxis.ReadValue() >= 0.5f);

        // Shooting
        if (triggerHeld)
        {
            Shoot();
        }
    }

    public void Shoot()
    {
        RaycastHit hitInfo;
        bool hit = Physics.Raycast(firePoint.position, firePoint.forward, out hitInfo);
        if (hit)
        {
            Debug.Log(hitInfo.collider.gameObject.name);
            Instantiate(hitEffect, hitInfo.point, hitEffect.transform.rotation);
            Destroy(hitEffect, 5);
        }
    }
}
