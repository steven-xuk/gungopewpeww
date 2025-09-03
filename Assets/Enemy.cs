using UnityEngine;

public class Enemy : MonoBehaviour
{

    public UnityEngine.AI.NavMeshAgent agent;
    public Transform targetPosition;
    public GameObject deathEffect;

    public float health;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        agent.destination = targetPosition.position;

        if (health <= 0f)
        {
            Instantiate(deathEffect, gameObject.transform.position, Quaternion.identity);
            Destroy(gameObject, 0f);
        }
    }

    public void DecreaseHealth(float damage)
    {
        health -= damage;
    }
}
