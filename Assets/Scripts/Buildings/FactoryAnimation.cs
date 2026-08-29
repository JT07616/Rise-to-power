using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FactoryAnimation : MonoBehaviour
{
    public GameObject indicator;
    public Image gearFill;
    public float pulseAmount = 0.02f;
    public float pulseSpeed = 3f;
    public Transform[] smokes;
    public Transform[] indicatorSpots;
    public float smokeRise = 250f;
    public float smokeTime = 2f;
    public AudioSource machineAudio;

    private BuildingInfo factory;
    private Vector3 startScale;
    private float timeLeft;
    private List<Transform> puffs = new List<Transform>();
    private List<Vector3> puffHomes = new List<Vector3>();
    private List<Vector3> puffSizes = new List<Vector3>();

    void Start()
    {
        factory = GetComponent<BuildingInfo>();
        startScale = transform.localScale;

        foreach (Transform smoke in smokes)
        {
            if (smoke == null) continue;

            foreach (Transform puff in smoke)
            {
                puffs.Add(puff);
                puffHomes.Add(puff.localPosition);
                puffSizes.Add(puff.localScale);
            }
        }
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
            foreach (Transform smoke in smokes)
            {
                if (smoke != null && smoke.gameObject.activeSelf)
                    smoke.gameObject.SetActive(false);
            }
            if (machineAudio != null && machineAudio.isPlaying) machineAudio.Stop();
            return;
        }

        if (!indicator.activeSelf)
        {
            indicator.SetActive(true);
            timeLeft = factory.productionDurationSeconds;
            CenterIndicator();

            if (machineAudio != null) machineAudio.Play();
        }

        timeLeft -= Time.deltaTime;
        float progress = 1f - timeLeft / factory.productionDurationSeconds;
        gearFill.fillAmount = progress;

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = startScale * pulse;

        if (Camera.main != null)
            indicator.transform.rotation = Camera.main.transform.rotation;

        AnimateSmoke();
    }

    void CenterIndicator()
    {
        if (indicatorSpots.Length == 0) return;

        int level = Mathf.Clamp(factory.upgradeLevel, 0, indicatorSpots.Length - 1);
        if (indicatorSpots[level] != null)
            indicator.transform.position = indicatorSpots[level].position;
    }

    void AnimateSmoke()
    {
        for (int i = 0; i < puffs.Count; i++)
        {
            Transform puff = puffs[i];
            if (!puff.gameObject.activeSelf) puff.gameObject.SetActive(true);

            float phase = (Time.time / smokeTime + i * 0.37f) % 1f;
            float sway = Mathf.Sin(phase * 6f + i) * 20f;

            puff.localPosition = puffHomes[i] + new Vector3(sway, phase * smokeRise, 0f);
            puff.localScale = puffSizes[i] * Mathf.Sin(phase * Mathf.PI);
        }

        foreach (Transform smoke in smokes)
        {
            if (smoke == null) continue;

            if (!smoke.gameObject.activeSelf) smoke.gameObject.SetActive(true);
            if (Camera.main != null) smoke.rotation = Camera.main.transform.rotation;
        }
    }
}