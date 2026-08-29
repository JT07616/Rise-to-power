using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleTest : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            DeliveryVehicleManager.Instance.StartVehicleJourney(pointA.position, pointB.position, 10f, null);
            DeliveryVehicleManager.Instance.StartVehicleJourney(pointB.position, pointA.position, 10f, null);
        }
    }
}