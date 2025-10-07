using UnityEngine;

public class Enemy : MonoBehaviour
{

    public UnityEngine.AI.NavMeshAgent agent;
    public Transform targetPosition;
    public GameObject deathEffect;
    public Transform eyesPosition;
    public Transform eyesPosition2;
    public Transform eyesPosition3;
    public GameObject bullet;
    public Transform attackPoint;

    public Animator animator;

    public float turnSpeed;
    public float shootForce;

    public bool canSee = false;
    public bool couldSee = false;
    public bool isGoodRotation = false;
    public bool canShoot = true;

    public float health;

    public Rigidbody[] rigidbodies = new Rigidbody[13];
    public Collider[] colliders = new Collider[13];
    public CharacterJoint[] chJoints = new CharacterJoint[13];

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetKinematic(true);

    }

    void SetKinematic(bool newValue)
    {
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in bodies)
        {
            rb.isKinematic = newValue;
        }
    }

    // Update is called once per frame
    void Update()
    {
        agent.destination = targetPosition.position;

        if (health <= 0f)
        {
            Instantiate(deathEffect, gameObject.transform.position, Quaternion.identity);
            Destroy(gameObject, 0f);
            //SetKinematic(false);
            //GetComponent<Animator>().enabled = false;

        }

        

        if (couldSee == false)
        {
            RaycastHit hit;
            RaycastHit hit2;
            RaycastHit hit3;

            Vector3 direction = (targetPosition.transform.position - eyesPosition.transform.position).normalized;
            Vector3 direction2 = (targetPosition.transform.position - eyesPosition2.transform.position).normalized;
            Vector3 direction3 = (targetPosition.transform.position - eyesPosition3.transform.position).normalized;

            if (Physics.Raycast(eyesPosition.position, direction, out hit, 100.0f) && Physics.Raycast(eyesPosition2.position, direction2, out hit2, 100.0f) && Physics.Raycast(eyesPosition3.position, direction3, out hit3, 100.0f))
            {
                if(hit.collider.gameObject.name == "Main Camera" && hit2.collider.gameObject.name == "Main Camera" && hit3.collider.gameObject.name == "Main Camera")
                {
                    canSee = true;
                    couldSee = true;
                    agent.stoppingDistance = 5f;
                    Vector3 to = targetPosition.position - transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude < 0.0001f) return;

                    Quaternion targetRot = Quaternion.LookRotation(to);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRot,
                        1f - Mathf.Exp(-turnSpeed * Time.deltaTime)
                    );
                }
                else
                {
                    canSee = false;
                    agent.stoppingDistance = 0f;
                }
            }
        } else
        {
            RaycastHit hit;
            Vector3 direction = (targetPosition.transform.position - eyesPosition.transform.position).normalized;
            if (Physics.Raycast(eyesPosition.position, direction, out hit, 100.0f))
            {
                if (hit.collider.gameObject.name == "Main Camera")
                {
                    canSee = true;
                    agent.stoppingDistance = 5f;
                    Vector3 to = targetPosition.position - transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude < 0.0001f) return;

                    Quaternion targetRot = Quaternion.LookRotation(to);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRot,
                        1f - Mathf.Exp(-turnSpeed * Time.deltaTime)
                    );
                }
                else
                {
                    canSee = false;
                    couldSee = false;
                    agent.stoppingDistance = 0f;
                }
            }
        }

        if (canSee == true)
        {
            if (canShoot)
            {
                Shoot();
            }
        }

           
        if (agent.velocity.magnitude >= 1f)
        {
            animator.Play("run");
        }
        else
        {
            animator.Play("aim");
        }



        
    }

    public void DecreaseHealth(float damage)
    {
        health -= damage;
    }
    void Shoot()
    {
        Vector3 direction = (targetPosition.transform.position - eyesPosition.transform.position).normalized;
        canShoot = false;
        Debug.Log("shooting");
        GameObject shotBullet = Instantiate(bullet, attackPoint.position, Quaternion.LookRotation(direction));
        shotBullet.GetComponent<Rigidbody>().AddForce(direction.normalized * shootForce * Time.deltaTime);
        Invoke(nameof(Reload), 1);
    }

    void Reload()
    {
        canShoot = true;
    }
}
