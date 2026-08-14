using UnityEngine;

public class DeliveryRipple : MonoBehaviour
{
    public float duration = 1.2f;
    public float spread = 3f;

    private SpriteRenderer rend;
    private float time;

    void Start()
    {
        rend = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        time += Time.deltaTime / duration;

        transform.localScale = Vector3.one * (0.5f + time * spread);

        Color color = rend.color;
        color.a = 1f - time;
        rend.color = color;

        if (time >= 1f)
            Destroy(gameObject);
    }
}