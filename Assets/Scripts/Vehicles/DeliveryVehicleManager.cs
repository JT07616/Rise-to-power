using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DeliveryVehicleManager : MonoBehaviour
{
    public static DeliveryVehicleManager Instance { get; private set; }

    [Header("Vehicle")]
    public DeliveryVehicle vehiclePrefab;
    public List<DeliveryVehicle> additionalVehiclePrefabs = new List<DeliveryVehicle>();

    [Header("Goods transfer vehicle")]
    public DeliveryVehicle goodsTransferVehiclePrefab;

    [Min(0.5f)]
    public float navMeshSearchDistance = 50f;

    public Transform transferRippleSpot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool StartVehicleJourney(
        Vector3 startPosition,
        Vector3 destination,
        float duration,
        Action onArrival,
        Action onVehicleReturned = null)
    {
        return StartJourney(
            PickDeliveryPrefab(), startPosition, destination, duration,
            onArrival, onVehicleReturned, null, null);
    }

    public bool StartGoodsTransferJourney(
        Vector3 startPosition,
        Vector3 destination,
        float duration,
        Action onArrival = null,
        Action onVehicleReturned = null,
        Vector3? returnDestination = null,
        Vector3? outboundWaypoint = null)
    {
        return StartJourney(
            goodsTransferVehiclePrefab, startPosition, destination, duration,
            onArrival, onVehicleReturned, returnDestination, outboundWaypoint);
    }

    private DeliveryVehicle PickDeliveryPrefab()
    {
        List<DeliveryVehicle> prefabs = new List<DeliveryVehicle>();
        if (vehiclePrefab != null) prefabs.Add(vehiclePrefab);
        foreach (DeliveryVehicle prefab in additionalVehiclePrefabs)
            if (prefab != null) prefabs.Add(prefab);

        return prefabs.Count > 0
            ? prefabs[UnityEngine.Random.Range(0, prefabs.Count)]
            : null;
    }

    private bool StartJourney(
        DeliveryVehicle prefab,
        Vector3 start,
        Vector3 destination,
        float duration,
        Action onArrival,
        Action onReturned,
        Vector3? returnPoint,
        Vector3? waypoint)
    {
        if (prefab == null)
        {
            Debug.LogError("DeliveryVehicleManager nema dodijeljen prefab vozila.");
            return false;
        }

        // Spawn na najblizoj tocki navmesha; ostale provjere radi samo vozilo.
        if (!NavMesh.SamplePosition(start, out NavMeshHit hit, navMeshSearchDistance, NavMesh.AllAreas))
        {
            Debug.LogError($"Nema NavMesha blizu starta vozila: {start}");
            return false;
        }

        DeliveryVehicle vehicle = Instantiate(prefab, hit.position, Quaternion.identity);
        if (vehicle.BeginJourney(hit.position, destination, duration, onArrival, onReturned, returnPoint, waypoint))
        {
            if (prefab == goodsTransferVehiclePrefab && transferRippleSpot != null)
                vehicle.rippleSpot = transferRippleSpot.position;

            return true;
        }

        Destroy(vehicle.gameObject);
        return false;
    }
}
