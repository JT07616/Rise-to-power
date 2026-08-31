using System.Collections;
using UnityEngine;

// Sacekusa: na mjestu presretanja bukne noz, pa odande krene auto
// prema skladistu onoga tko je preoteo posiljku.
public class AmbushEffect : MonoBehaviour
{
    public GameObject effectPrefab;
    public float effectHeight = 2f;
    public float effectLife = 3f;
    public float carDelay = 1.2f;
    public float carDuration = 12f;
    public AudioSource ambushAudio;
    public AudioClip ambushSound;

    private static AmbushEffect instance;

    void Awake()
    {
        instance = this;
    }

    public static void Play(Vector3 spot, TerritoryOwner winner)
    {
        if (instance != null) instance.StartCoroutine(instance.Run(spot, winner));
    }

    IEnumerator Run(Vector3 spot, TerritoryOwner winner)
    {
        spot = CarSpot(spot);

        if (ambushAudio != null && ambushSound != null)
            ambushAudio.PlayOneShot(ambushSound);

        if (effectPrefab != null)
        {
            GameObject effect = Instantiate(
                effectPrefab, spot + Vector3.up * effectHeight, effectPrefab.transform.rotation);
            StartCoroutine(FaceCamera(effect.transform));
            Destroy(effect, effectLife);
        }

        yield return new WaitForSeconds(carDelay);

        Vector3 warehouse = WarehousePosition(winner);
        if (warehouse == Vector3.zero || DeliveryVehicleManager.Instance == null) yield break;

        DeliveryVehicleManager.Instance.StartVehicleJourney(
            spot, warehouse, carDuration, null, null, winner == TerritoryOwner.AI);
    }

    // Presretanje se dogadja na vozilu, a ne na zgradi, pa efekt ide na
    // presretnuti kamion; ako ga vise nema, ostaje pozicija kuce.
    static Vector3 CarSpot(Vector3 spot)
    {
        DeliveryVehicle nearest = null;
        float best = 25f;

        foreach (DeliveryVehicle car in FindObjectsByType<DeliveryVehicle>(FindObjectsSortMode.None))
        {
            float distance = Vector3.Distance(car.transform.position, spot);
            if (distance >= best) continue;

            best = distance;
            nearest = car;
        }

        return nearest != null ? nearest.transform.position : spot;
    }

    // noz je sprite pa se mora okretati prema kameri, inace ga vidis s ruba
    IEnumerator FaceCamera(Transform knife)
    {
        while (knife != null)
        {
            if (Camera.main != null)
                knife.rotation = Camera.main.transform.rotation;

            yield return null;
        }
    }

    static Vector3 WarehousePosition(TerritoryOwner owner)
    {
        if (owner == TerritoryOwner.AI)
        {
            AiFacilityMarker facility = AiFacilityMarker.Find(AiFacilityRole.Warehouse);
            return facility != null ? facility.transform.position : Vector3.zero;
        }

        foreach (BuildingInfo building in FindObjectsByType<BuildingInfo>(FindObjectsSortMode.None))
        {
            if (building.buildingRole == BuildingRole.Warehouse) return building.transform.position;
        }

        return Vector3.zero;
    }
}
