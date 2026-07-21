using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class BuildingSelector : MonoBehaviour
{
    public Camera mainCamera;
    public LayerMask clickableLayers = ~0;
    public BuildingPopupUI popupUI;

    private BuildingInfo hoveredBuilding;
    private BuildingInfo selectedBuilding;

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (GameEventManager.IsPauseMenuOpen)
        {
            return;
        }

        if (GameEventManager.IsPopupOpen)
        {
            return;
        }

        if (DeliveryOrderManager.IsPopupOpen)
        {
            return;
        }

        if (!GameEventManager.CanPlayerAct)
        {
            return;
        }

        if (popupUI != null && popupUI.IsOpen)
        {
            return;
        }

        HandleHover();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleClick();
        }
    }

    void HandleHover()
    {
        BuildingInfo building = GetBuildingUnderMouse();

        if (building == hoveredBuilding)
        {
            return;
        }

        if (hoveredBuilding != null && hoveredBuilding != selectedBuilding)
        {
            hoveredBuilding.ClearColor();
        }

        hoveredBuilding = building;

        if (hoveredBuilding != null && hoveredBuilding != selectedBuilding)
        {
            hoveredBuilding.ShowHover();
        }
    }

    void HandleClick()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        DeliveryOrderTarget deliveryTarget = GetDeliveryTargetUnderMouse();
        if (deliveryTarget != null)
        {
            deliveryTarget.OpenOrder();
            return;
        }

        if (hoveredBuilding == null)
        {
            return;
        }

        if (selectedBuilding != null)
        {
            selectedBuilding.ClearColor();
        }

        selectedBuilding = hoveredBuilding;
        selectedBuilding.ShowSelected();

        if (popupUI != null)
        {
            popupUI.Show(selectedBuilding);
        }
    }

    BuildingInfo GetBuildingUnderMouse()
    {
        if (mainCamera == null)
        {
            return null;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, clickableLayers))
        {
            return hit.collider.GetComponentInParent<BuildingInfo>();
        }

        return null;
    }

    DeliveryOrderTarget GetDeliveryTargetUnderMouse()
    {
        if (mainCamera == null)
        {
            return null;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, clickableLayers))
        {
            return hit.collider.GetComponentInParent<DeliveryOrderTarget>();
        }

        return null;
    }
}
