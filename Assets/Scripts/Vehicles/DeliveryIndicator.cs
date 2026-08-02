using UnityEngine;

// Ikona paketa iznad vozila — child objekt "Indicator" na prefabu,
// ovdje se samo pali/gasi i animira.
public class DeliveryIndicator : MonoBehaviour
{
    [Header("Animation")]
    [Min(0f)] public float bobHeight = 0.6f;
    [Min(0f)] public float bobSpeed = 3f;
    [Min(0f)] public float pulseAmount = 0.05f;
    [Min(0f)] public float pulseSpeed = 3f;

    private Transform icon;
    private Camera cam;
    private Vector3 baseScale;
    private float baseHeight;

    private void Awake()
    {
        icon = transform.Find("Indicator");
        if (icon == null)
        {
            Debug.LogWarning($"{name}: prefab nema child objekt 'Indicator'.");
            return;
        }

        baseScale = icon.localScale;
        baseHeight = icon.localPosition.y;
        icon.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (icon == null || !icon.gameObject.activeSelf) return;

        float bob = Mathf.Abs(Mathf.Sin(Time.time * bobSpeed)) * bobHeight;
        icon.localPosition = new Vector3(0f, baseHeight + bob, 0f);
        icon.localScale = baseScale * (1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount);

        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            Vector3 toIcon = icon.position - cam.transform.position;
            if (toIcon.sqrMagnitude > 0.001f)
                icon.rotation = Quaternion.LookRotation(toIcon, Vector3.up);
        }
    }

    public void Show()
    {
        if (icon != null) icon.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (icon != null) icon.gameObject.SetActive(false);
    }
}
