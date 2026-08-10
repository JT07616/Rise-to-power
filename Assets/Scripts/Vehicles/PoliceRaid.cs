using System.Collections;
using UnityEngine;

public class PoliceRaid : MonoBehaviour
{
    public DeliveryVehicle policeCarPrefab;
    public Transform spawnPoint;
    public Transform[] warehouseSpots;
    public Transform[] factorySpots;
    public float driveSpeed = 12f;
    public float secondsBetweenCars = 1.2f;

    private int lastRaidCount;

    void Start()
    {
        if (GameResources.Instance != null)
            lastRaidCount = GameResources.Instance.policeRaidCount;
    }

    void Update()
    {
        GameResources resources = GameResources.Instance;
        if (resources == null) return;

        if (resources.policeRaidCount > lastRaidCount)
            StartCoroutine(SendConvoy());

        lastRaidCount = resources.policeRaidCount;
    }

    IEnumerator SendConvoy()
    {
        // policija nasumicno bira hoce li banuti pred skladiste ili pred tvornicu
        Transform[] spots = Random.value < 0.5f ? warehouseSpots : factorySpots;
        if (spots == null || spots.Length == 0)
            spots = warehouseSpots != null && warehouseSpots.Length > 0 ? warehouseSpots : factorySpots;
        if (spots == null) yield break;

        foreach (Transform spot in spots)
        {
            if (spot == null) continue;

            // trajanje po autu, da citav konvoj vozi istom brzinom
            float duration = Vector3.Distance(spawnPoint.position, spot.position) / driveSpeed;

            DeliveryVehicle car = Instantiate(policeCarPrefab, spawnPoint.position, spawnPoint.rotation);
            Quaternion parkRotation = spot.rotation;

            if (!car.BeginJourney(spawnPoint.position, spot.position, duration,
                    () => StartCoroutine(RotateIntoSpot(car.transform, parkRotation)), null, spawnPoint.position))
                Destroy(car.gameObject);

            // sljedeci auto krece tek kad se prethodni odmakne — konvoj umjesto hrpe
            yield return new WaitForSeconds(secondsBetweenCars);
        }
    }

    IEnumerator RotateIntoSpot(Transform car, Quaternion targetRotation)
    {
        float t = 0f;
        Quaternion startRotation = car.rotation;

        while (t < 1f && car != null)
        {
            t += Time.deltaTime * 2f;
            car.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }
    }
}
