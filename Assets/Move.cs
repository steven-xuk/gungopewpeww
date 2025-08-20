using UnityEngine;

public class Move : MonoBehaviour
{
    public Rigidbody rb;
    public float force;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        rb.AddForce(Vector3.forward * force, ForceMode.Force);
    }
}
