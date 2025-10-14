using UnityEngine;

public class Level1Enemy : MonoBehaviour
{

    public GameObject fallEnemy;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Invoke("DoStuff", 3);
    }

    void DoStuff()
    {
        fallEnemy.transform.position = gameObject.transform.position;
        gameObject.SetActive(false);
    }
}
