using UnityEngine;

public class HealthBoost : MonoBehaviour
{

    public Health healthScript;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(other.gameObject.tag);

        if (other.gameObject.tag == "Player")
        {
            healthScript.IncreaseHealth(50);
            Destroy(gameObject);
        }
    }
}
