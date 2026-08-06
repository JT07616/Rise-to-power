using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class BuildingSelector : MonoBehaviour
{
    private static BuildingSelector instance;

    public Camera mainCamera;
    public LayerMask clickableLayers = ~0;
    public BuildingPopupUI popupUI;

    private BuildingInfo hoveredBuilding;
    private BuildingInfo selectedBuilding;
    private BuildingInfo[] labeledBuildings;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        labeledBuildings = FindObjectsByType<BuildingInfo>(FindObjectsSortMode.None);
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
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

        if (popupUI != null && selectedBuilding != null)
        {
            selectedBuilding.ClearColor();
            selectedBuilding = null;
            hoveredBuilding = null;
        }

        HandleHover();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            if (!IsPointerOverMapLabel(pointerPosition) &&
                !DeliveryOrderManager.IsPointerOverMapLabel(pointerPosition))
            {
                HandleClick();
            }
        }
    }

    public static bool IsPointerOverMapLabel(Vector2 screenPosition)
    {
        if (instance == null || instance.mainCamera == null || instance.labeledBuildings == null)
        {
            return false;
        }

        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        foreach (BuildingInfo building in instance.labeledBuildings)
        {
            if (building == null || !IsLabeledBuilding(building))
            {
                continue;
            }

            Vector3 screen = instance.mainCamera.WorldToScreenPoint(building.LabelPosition);
            if (screen.z <= 0f)
            {
                continue;
            }

            Rect rect = new Rect(screen.x - 65f, Screen.height - screen.y - 15f, 130f, 30f);
            if (rect.Contains(guiPosition))
            {
                return true;
            }
        }

        return false;
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

        if (IsLabeledBuilding(hoveredBuilding))
        {
            FocusAndShow(hoveredBuilding);
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

    void OnGUI()
    {
        if (mainCamera == null || labeledBuildings == null ||
            GameEventManager.IsPauseMenuOpen || GameEventManager.IsPopupOpen ||
            DeliveryOrderManager.IsPopupOpen || !GameEventManager.CanPlayerAct)
        {
            return;
        }

        int previousDepth = GUI.depth;
        GUI.depth = 100;

        foreach (BuildingInfo building in labeledBuildings)
        {
            if (building == null || !IsLabeledBuilding(building))
            {
                continue;
            }

            Vector3 screen = mainCamera.WorldToScreenPoint(building.LabelPosition);
            if (screen.z <= 0f)
            {
                continue;
            }

            Rect rect = new Rect(screen.x - 65f, Screen.height - screen.y - 15f, 130f, 30f);
            if (GUI.Button(rect, GetBuildingLabel(building)))
            {
                FocusAndShow(building);
            }
        }

        GUI.depth = previousDepth;
    }

    void FocusAndShow(BuildingInfo building)
    {
        if (building == null || popupUI == null)
        {
            return;
        }

        if (popupUI.IsOpen)
        {
            popupUI.ClosePanel();
        }

        SimpleStrategyCamera strategyCamera = mainCamera != null
            ? mainCamera.GetComponent<SimpleStrategyCamera>()
            : null;

        if (strategyCamera != null)
        {
            strategyCamera.FocusOn(building.LabelPosition, () => SelectAndShow(building));
            return;
        }

        SelectAndShow(building);
    }

    void SelectAndShow(BuildingInfo building)
    {
        if (building == null || popupUI == null)
        {
            return;
        }

        if (selectedBuilding != null && selectedBuilding != building)
        {
            selectedBuilding.ClearColor();
        }

        selectedBuilding = building;
        selectedBuilding.ShowSelected();
        popupUI.Show(selectedBuilding);
    }

    static bool IsLabeledBuilding(BuildingInfo building)
    {
        return building != null &&
               (building.buildingRole != BuildingRole.General || building.buildingName == "Apartment");
    }

    static string GetBuildingLabel(BuildingInfo building)
    {
        if (building.buildingName == "Apartment")
        {
            return "APARTMENT";
        }

        switch (building.buildingRole)
        {
            case BuildingRole.Factory:
                return "FACTORY";
            case BuildingRole.WorkerContact:
                return "HIRE WORKERS";
            case BuildingRole.Warehouse:
                return "WAREHOUSE";
            default:
                return "BUILDING";
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
