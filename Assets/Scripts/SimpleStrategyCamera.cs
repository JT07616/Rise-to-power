using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class SimpleStrategyCamera : MonoBehaviour
{
    public float zoomSpeed = 1.5f;
    public float moveSpeed = 8f;

    public float rotationSpeed = 0.3f;
    public float verticalRotationSpeed = 0.25f;

    public float minHeight = 15f;
    public float maxHeight = 73.6f;

    public float minX = 202.8f;
    public float maxX = 834f;

    public float minZ = 694.6f;
    public float maxZ = 1603.29f;

    public float minPitch = 10f;
    public float maxPitch = 85f;

    public LayerMask clickableLayers = ~0;

    private Vector3 targetPosition;
    private Vector2 lastMousePosition;

    private float currentYaw;
    private float currentPitch;

    void Start()
    {
        targetPosition = transform.position;

        Vector3 currentRotation = transform.eulerAngles;

        currentYaw = currentRotation.y;
        currentPitch = currentRotation.x;

        if (currentPitch > 180f)
        {
            currentPitch -= 360f;
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

        HandleZoom();
        HandleClickMove();
        HandleRotation();

        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);
        targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);

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

        newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
        newPosition.y = Mathf.Clamp(newPosition.y, minHeight, maxHeight);
        newPosition.z = Mathf.Clamp(newPosition.z, minZ, maxZ);

        targetPosition = newPosition;
    }

    void HandleClickMove()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, clickableLayers))
        {
            BuildingInfo building =
                hit.collider.GetComponentInParent<BuildingInfo>();

            if (building != null)
            {
                return;
            }

            Vector3 clickedPoint = hit.point;

            float clampedX = Mathf.Clamp(clickedPoint.x, minX, maxX);
            float clampedZ = Mathf.Clamp(clickedPoint.z, minZ, maxZ);

            targetPosition = new Vector3(
                clampedX,
                targetPosition.y,
                clampedZ
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

        currentYaw += mouseDelta.x * rotationSpeed;
        currentPitch += mouseDelta.y * verticalRotationSpeed;

        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(
            currentPitch,
            currentYaw,
            0f
        );
    }
}