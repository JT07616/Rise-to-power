using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class SimpleStrategyCamera : MonoBehaviour
{
    public float zoomSpeed = 0.1f;
    public float moveSpeed = 8f;
    public float rotationSpeed = 0.2f;

    public float minHeight = 8f;
    public float maxHeight = 40f;

    public LayerMask clickableLayers = ~0;

    private Vector3 targetPosition;
    private Vector2 lastMousePosition;

    void Start()
    {
        targetPosition = transform.position;
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

        HandleZoom();
        HandleClickMove();
        HandleRotation();

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );
    }

    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
        {
            return;
        }

        Vector3 zoomMove = transform.forward * scroll * zoomSpeed;
        Vector3 newPosition = targetPosition + zoomMove;

        newPosition.y = Mathf.Clamp(newPosition.y, minHeight, maxHeight);
        targetPosition = newPosition;
    }

    void HandleClickMove()
{
    if (!Mouse.current.leftButton.wasPressedThisFrame)
    {
        return;
    }

    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
    {
        return;
    }

    Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

    if (Physics.Raycast(ray, out RaycastHit hit, 500f, clickableLayers))
    {
        BuildingInfo building = hit.collider.GetComponentInParent<BuildingInfo>();

        if (building != null)
        {
            return;
        }

        Vector3 clickedPoint = hit.point;

        targetPosition = new Vector3(
            clickedPoint.x,
            targetPosition.y,
            clickedPoint.z
        );
    }
}
    void HandleRotation()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            lastMousePosition = mousePosition;
        }

        if (!Mouse.current.rightButton.isPressed)
        {
            return;
        }

        Vector2 mouseDelta = mousePosition - lastMousePosition;
        lastMousePosition = mousePosition;

        transform.Rotate(0f, mouseDelta.x * rotationSpeed, 0f, Space.World);
    }
}