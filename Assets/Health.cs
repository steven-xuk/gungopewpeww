using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Health : MonoBehaviour
{

    public float health;
    public Volume volume;

    Vignette vig;

    void Start()
    {
        if (volume.profile.TryGet<Vignette>(out Vignette vign))
        {
            vig = vign;
        }
    }


    void Update()
    {
        if (health <= 0)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        vig.intensity.value = 0.5f*Mathf.Clamp((1f - (health / 100)), 0, 0.777f);

    }

    public void DecreaseHealth(float damage)
    {
        health -= damage;
    }

    public void IncreaseHealth(float damage)
    {
        health += damage;
    }
}
