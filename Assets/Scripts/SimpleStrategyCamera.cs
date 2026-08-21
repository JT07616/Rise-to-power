using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System;

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

    [Header("Order Focus")]
    public float focusDistance = 35f;
    public float focusHeight = 25f;
    public float focusMoveSpeed = 80f;
    public float focusRotationSpeed = 120f;

    public LayerMask clickableLayers = ~0;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector2 lastMousePosition;

    private float currentYaw;
    private float currentPitch;
    private Action onFocusCompleted;
    private bool isFocusing;
    private float timeScaleBeforeFocus = 1f;

    public bool IsFocusing
    {
        get { return isFocusing; }
    }

    void Start()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;

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

        if (GameEventManager.IsPauseMenuOpen)
        {
            return;
        }

        if (GameEventManager.IsPopupOpen && !GameEventManager.IsPlayerTurnAnnouncementActive)
        {
            return;
        }

        if (DeliveryOrderManager.IsPopupOpen)
        {
            return;
        }

        if (BuildingPopupUI.IsAnyOpen)
        {
            return;
        }

        if (!isFocusing)
        {
            HandleZoom();
            HandleClickMove();
            HandleRotation();
        }

        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);
        targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);

        if (isFocusing)
        {
            float focusDeltaTime = Time.unscaledDeltaTime;
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                focusMoveSpeed * focusDeltaTime
            );
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                focusRotationSpeed * focusDeltaTime
            );
        }
        else
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );
        }

        if (isFocusing &&
            Vector3.Distance(transform.position, targetPosition) <= 0.5f &&
            Quaternion.Angle(transform.rotation, targetRotation) <= 0.5f)
        {
            Action completed = onFocusCompleted;
            onFocusCompleted = null;
            isFocusing = false;
            RestoreTimeAfterFocus();
            completed?.Invoke();
        }
    }

    public void FocusOn(Vector3 worldPosition, Action onCompleted)
    {
        Vector3 approachDirection = Vector3.ProjectOnPlane(
            worldPosition - transform.position,
            Vector3.up
        ).normalized;
        if (approachDirection.sqrMagnitude < 0.01f)
        {
            approachDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        }
        if (approachDirection.sqrMagnitude < 0.01f)
        {
            approachDirection = Vector3.forward;
        }

        Vector3 focusPosition = worldPosition - approachDirection * focusDistance;
        focusPosition.y = worldPosition.y + focusHeight;

        targetPosition = new Vector3(
            Mathf.Clamp(focusPosition.x, minX, maxX),
            Mathf.Clamp(focusPosition.y, minHeight, maxHeight),
            Mathf.Clamp(focusPosition.z, minZ, maxZ)
        );

        targetRotation = Quaternion.LookRotation(worldPosition - targetPosition, Vector3.up);
        Vector3 focusAngles = targetRotation.eulerAngles;
        currentYaw = focusAngles.y;
        currentPitch = focusAngles.x > 180f ? focusAngles.x - 360f : focusAngles.x;

        if (!isFocusing)
        {
            timeScaleBeforeFocus = Time.timeScale;
            Time.timeScale = 0f;
        }

        isFocusing = true;
        onFocusCompleted = onCompleted;
    }

    public void CancelFocus()
    {
        if (isFocusing)
        {
            isFocusing = false;
            RestoreTimeAfterFocus();
        }

        onFocusCompleted = null;
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    void OnDisable()
    {
        if (isFocusing)
        {
            isFocusing = false;
            RestoreTimeAfterFocus();
        }
    }

    void RestoreTimeAfterFocus()
    {
        Time.timeScale = timeScaleBeforeFocus;
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

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        if (BuildingSelector.IsPointerOverMapLabel(pointerPosition) ||
            BuildingSelector.IsPointerOverShortcutBar(pointerPosition) ||
            DeliveryOrderManager.IsPointerOverMapLabel(pointerPosition))
        {
            return;
        }

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(
            pointerPosition
        );

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, clickableLayers))
        {
            BuildingInfo building =
                hit.collider.GetComponentInParent<BuildingInfo>();
            DeliveryOrderTarget deliveryTarget =
                hit.collider.GetComponentInParent<DeliveryOrderTarget>();

            if (building != null || deliveryTarget != null)
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
