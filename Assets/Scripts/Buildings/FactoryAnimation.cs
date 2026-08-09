using UnityEngine;
using UnityEngine.UI;

public class FactoryAnimation : MonoBehaviour
{
    public GameObject indicator;
    public Image gearFill;
    public float pulseAmount = 0.02f;
    public float pulseSpeed = 3f;

    private BuildingInfo factory;
    private Vector3 startScale;
    private float timeLeft;

    void Start()
    {
        factory = GetComponent<BuildingInfo>();
        startScale = transform.localScale;
    }

    void Update()
    {
        if (!factory.IsProducingGoods)
        {
            if (indicator.activeSelf)
            {
                indicator.SetActive(false);
                transform.localScale = startScale;
            }
            return;
        }

        if (!indicator.activeSelf)
        {
            indicator.SetActive(true);
            timeLeft = factory.productionDurationSeconds;
        }

        timeLeft -= Time.deltaTime;
        float progress = 1f - timeLeft / factory.productionDurationSeconds;
        gearFill.fillAmount = progress;

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = startScale * pulse;

        if (Camera.main != null)
            indicator.transform.rotation = Camera.main.transform.rotation;
    }
}
