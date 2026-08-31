using UnityEngine;

// Strelica iznad odredista, stoji dok vozilo ne stigne.
public class DeliveryArrow : MonoBehaviour
{
    [Min(0f)] public float clearance = 2f;
    [Min(0f)] public float searchRadius = 15f;
    [Min(0f)] public float bobHeight = 0.8f;
    [Min(0f)] public float bobSpeed = 2.5f;

    private float baseHeight;

    // Stize pivot zgrade, a on zna biti na rubu i pri tlu, pa se uzimaju granice modela.
    // Skladistu se salje marker na cesti, pa tamo upada najbliza zgrada.
    private void Awake()
    {
        Vector3 spot = transform.position;
        Bounds target = new Bounds(spot, Vector3.zero);
        float nearest = searchRadius;

        foreach (MeshCollider building in FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
        {
            Bounds area = building.bounds;
            if (area.size.y < 4f) continue;

            float gap = building.transform.position == spot || area.Contains(spot)
                ? 0f
                : Vector2.Distance(
                    new Vector2(area.center.x, area.center.z),
                    new Vector2(spot.x, spot.z));

            if (gap >= nearest) continue;

            nearest = gap;
            target = area;
        }

        baseHeight = target.max.y + clearance;
        transform.position = new Vector3(target.center.x, baseHeight, target.center.z);
    }

    private void Update()
    {
        transform.position = new Vector3(
            transform.position.x,
            baseHeight + Mathf.Abs(Mathf.Sin(Time.time * bobSpeed)) * bobHeight,
            transform.position.z);

        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }
}
