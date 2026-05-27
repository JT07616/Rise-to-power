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

        if (GameEventManager.IsPopupOpen)
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
}