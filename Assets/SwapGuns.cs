using UnityEngine;

public class SwapGuns : MonoBehaviour
{
    public GameObject currentGun;
    public GameObject swapGun;

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
            Debug.Log("aasd");
            currentGun.SetActive(false);
            swapGun.SetActive(true);
            Destroy(gameObject);
        }
    }
}
