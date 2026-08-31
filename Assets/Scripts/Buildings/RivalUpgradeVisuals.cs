using UnityEngine;

public class RivalUpgradeVisuals : MonoBehaviour
{
    public AiFacilityRole role;
    public GameObject level1Props;
    public GameObject level2Props;
    public GameObject level3Props;
    public Material[] levelMaterials;
    public Renderer[] copies;
    public Material[] copyMaterials;
    public Transform dustGroup;
    public float dustDuration = 1.5f;
    public float dustSpread = 2f;
    public AudioSource upgradeAudio;
    public AudioClip upgradeSound;

    private Renderer rend;
    private float dustTime = 1f;
    private bool changePending;
    private int lastLevel = -1;

    void Start()
    {
        rend = GetComponent<Renderer>();

        if (dustGroup != null)
            dustGroup.gameObject.SetActive(false);
    }

    void Update()
    {
        GameResources resources = GameResources.Instance;
        if (resources == null) return;

        int level = GetLevel(resources.Opponent);

        if (level != lastLevel)
        {
            bool loading = lastLevel < 0;
            lastLevel = level;

            if (loading || dustGroup == null)
            {
                ApplyLevel();
            }
            else
            {
                changePending = true;
                dustGroup.gameObject.SetActive(true);
                dustGroup.localScale = Vector3.one;
                dustTime = 0f;

                if (upgradeAudio != null && upgradeSound != null)
                    upgradeAudio.PlayOneShot(upgradeSound);
            }
        }

        if (dustTime < 1f && dustGroup != null)
            AnimateDust();
    }

    void AnimateDust()
    {
        dustTime = Mathf.Min(1f, dustTime + Time.deltaTime / dustDuration);

        if (changePending && dustTime >= 0.3f)
        {
            changePending = false;
            ApplyLevel();
        }

        dustGroup.localScale = Vector3.one * (1f + dustTime * (dustSpread - 1f));

        foreach (SpriteRenderer puff in dustGroup.GetComponentsInChildren<SpriteRenderer>())
        {
            Color color = puff.color;
            color.a = 1f - dustTime;
            puff.color = color;

            if (Camera.main != null)
                puff.transform.rotation = Camera.main.transform.rotation;
        }

        if (dustTime >= 1f)
            dustGroup.gameObject.SetActive(false);
    }

    void ApplyLevel()
    {
        if (level1Props != null) level1Props.SetActive(lastLevel >= 1);
        if (level2Props != null) level2Props.SetActive(lastLevel >= 2);
        if (level3Props != null) level3Props.SetActive(lastLevel >= 3);

        if (rend != null && lastLevel < levelMaterials.Length && levelMaterials[lastLevel] != null)
        {
            rend.material = levelMaterials[lastLevel];
        }

        if (lastLevel < copyMaterials.Length && copyMaterials[lastLevel] != null)
        {
            foreach (Renderer copy in copies)
            {
                if (copy != null) copy.material = copyMaterials[lastLevel];
            }
        }
    }

    int GetLevel(OpponentResources ai)
    {
        if (role == AiFacilityRole.Warehouse) return ai.warehouseUpgradeLevel;
        if (role == AiFacilityRole.Apartment) return ai.apartmentUpgradeLevel;
        return ai.factoryUpgradeLevel;
    }
}
