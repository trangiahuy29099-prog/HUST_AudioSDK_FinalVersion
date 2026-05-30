using UnityEngine;

public class HybridGameCameraController : MonoBehaviour
{
    public enum CameraMode
    {
        ThirdPersonFollow,
        FreeCamera
    }

    [Header("Mode")]
    public CameraMode currentMode = CameraMode.ThirdPersonFollow;
    public KeyCode switchModeKey = KeyCode.V;

    [Header("Third Person Follow")]
    public Transform followTarget;      // PlayerListener
    public Transform lookTarget;        // HeadOrientationPivot / AudioListenerPoint

    public float followDistance = 5.5f;
    public float followHeight = 2.2f;
    public float lookHeight = 1.55f;
    public float followSmooth = 14.0f;
    public float rotationSmooth = 18.0f;

    [Header("Third Person Mouse Orbit")]
    public bool allowMouseOrbitInFollowMode = true;

    [Tooltip("Bat thi giu chuot phai moi xoay camera. Tat thi chi can di chuot la camera xoay.")]
    public bool holdRightMouseToOrbit = true;

    public bool lockCursorWhileOrbiting = true;
    public float orbitSensitivity = 3.0f;
    public float minPitch = -25.0f;
    public float maxPitch = 65.0f;

    [Header("Camera Behind Player")]
    [Tooltip("Bat cai nay de khi khong xoay chuot, camera tu dong nam sau lung nhan vat.")]
    public bool keepCameraBehindTargetWhenNotOrbiting = true;

    [Tooltip("Bat cai nay de khi dung yen va xoay chuot, than nhan vat cung xoay theo huong camera.")]
    public bool rotateTargetWithCameraYaw = true;

    [Tooltip("Nen bat. Khi dang bam W/A/S/D thi script nhan vat se tu xoay theo huong di chuyen.")]
    public bool rotateTargetOnlyWhenNoMoveInput = true;

    public float targetYawFollowSpeed = 18.0f;

    [Header("Zoom")]
    public float minFollowDistance = 2.5f;
    public float maxFollowDistance = 14.0f;
    public float followZoomSpeed = 4.0f;

    [Header("Free Camera")]
    public float freeLookSensitivity = 2.0f;
    public float freeMoveSpeed = 12.0f;
    public float freeFastMultiplier = 3.0f;
    public float freeZoomSpeed = 12.0f;

    [Header("Free Orbit")]
    public bool enableFreeOrbit = true;
    public Transform freeOrbitTarget;
    public Vector3 fallbackOrbitPoint = Vector3.zero;
    public float freeOrbitDistance = 15.0f;
    public float freeOrbitSensitivity = 4.0f;
    public float minFreeOrbitDistance = 2.0f;
    public float maxFreeOrbitDistance = 80.0f;

    [Header("UI")]
    public bool showCameraUI = true;
    public int uiFontSize = 16;

    private float yaw;
    private float pitch;

    private bool wasOrbitingLastFrame = false;
    private bool cursorLockedByThisScript = false;

    private GUIStyle labelStyle;
    private GUIStyle buttonStyle;

    private void Start()
    {
        SyncAnglesFromCurrentCamera();

        if (followTarget != null)
        {
            yaw = followTarget.eulerAngles.y;
        }

        if (freeOrbitTarget != null)
        {
            freeOrbitDistance = Vector3.Distance(transform.position, freeOrbitTarget.position);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(switchModeKey))
        {
            ToggleCameraMode();
        }
    }

    private void LateUpdate()
    {
        if (currentMode == CameraMode.ThirdPersonFollow)
        {
            UpdateThirdPersonFollow();
        }
        else
        {
            UpdateFreeCamera();
        }
    }

    public void ToggleCameraMode()
    {
        if (currentMode == CameraMode.ThirdPersonFollow)
        {
            currentMode = CameraMode.FreeCamera;
            UnlockCursorIfNeeded();
            SyncAnglesFromCurrentCamera();
        }
        else
        {
            currentMode = CameraMode.ThirdPersonFollow;

            if (followTarget != null)
            {
                yaw = followTarget.eulerAngles.y;
            }
        }
    }

    private void UpdateThirdPersonFollow()
    {
        if (followTarget == null)
        {
            return;
        }

        bool orbiting = IsFollowOrbitInputActive();

        HandleFollowMouseOrbit(orbiting);
        HandleFollowZoom();

        RotateTargetWithCameraYawIfNeeded(orbiting);

        if (!orbiting && keepCameraBehindTargetWhenNotOrbiting)
        {
            yaw = Mathf.LerpAngle(
                yaw,
                followTarget.eulerAngles.y,
                Time.deltaTime * 6.0f
            );
        }

        ApplyThirdPersonCamera();
    }

    private bool IsFollowOrbitInputActive()
    {
        if (!allowMouseOrbitInFollowMode)
        {
            return false;
        }

        if (holdRightMouseToOrbit)
        {
            return Input.GetMouseButton(1);
        }

        return true;
    }

    private void HandleFollowMouseOrbit(bool orbiting)
    {
        if (!allowMouseOrbitInFollowMode)
        {
            UnlockCursorIfNeeded();
            return;
        }

        if (!orbiting)
        {
            UnlockCursorIfNeeded();
            return;
        }

        if (lockCursorWhileOrbiting)
        {
            LockCursor();
        }

        float mouseX = Input.GetAxis("Mouse X") * orbitSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * orbitSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void RotateTargetWithCameraYawIfNeeded(bool orbiting)
    {
        if (!rotateTargetWithCameraYaw)
        {
            return;
        }

        if (holdRightMouseToOrbit && !orbiting)
        {
            return;
        }

        if (rotateTargetOnlyWhenNoMoveInput && HasMoveInput())
        {
            return;
        }

        Quaternion targetRotation = Quaternion.Euler(0.0f, yaw, 0.0f);

        float t = 1.0f - Mathf.Exp(-targetYawFollowSpeed * Time.deltaTime);

        followTarget.rotation = Quaternion.Slerp(
            followTarget.rotation,
            targetRotation,
            t
        );
    }

    private bool HasMoveInput()
    {
        return
            Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.A) ||
            Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.UpArrow) ||
            Input.GetKey(KeyCode.DownArrow) ||
            Input.GetKey(KeyCode.LeftArrow) ||
            Input.GetKey(KeyCode.RightArrow);
    }

    private void ApplyThirdPersonCamera()
    {
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0.0f);

        Vector3 desiredOffset = orbitRotation * new Vector3(0.0f, 0.0f, -followDistance);
        desiredOffset.y += followHeight;

        Vector3 desiredPosition = followTarget.position + desiredOffset;

        float positionT = 1.0f - Mathf.Exp(-followSmooth * Time.deltaTime);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionT
        );

        Vector3 lookPoint;

        if (lookTarget != null)
        {
            lookPoint = lookTarget.position;
        }
        else
        {
            lookPoint = followTarget.position + Vector3.up * lookHeight;
        }

        Vector3 lookDirection = lookPoint - transform.position;

        if (lookDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(
            lookDirection.normalized,
            Vector3.up
        );

        float rotationT = 1.0f - Mathf.Exp(-rotationSmooth * Time.deltaTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationT
        );
    }

    private void HandleFollowZoom()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.01f)
        {
            return;
        }

        followDistance -= scroll * followZoomSpeed;
        followDistance = Mathf.Clamp(followDistance, minFollowDistance, maxFollowDistance);
    }

    private void UpdateFreeCamera()
    {
        HandleFreeMouseLook();
        HandleFreeMovement();
        HandleFreeZoom();
        HandleFreeOrbit();
    }

    private void HandleFreeMouseLook()
    {
        if (!Input.GetMouseButton(1))
        {
            return;
        }

        LockCursor();

        float mouseX = Input.GetAxis("Mouse X") * freeLookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * freeLookSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -85.0f, 85.0f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0.0f);
    }

    private void HandleFreeMovement()
    {
        if (!Input.GetMouseButton(1))
        {
            UnlockCursorIfNeeded();
            return;
        }

        float speed = freeMoveSpeed;

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            speed *= freeFastMultiplier;
        }

        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
        {
            move += transform.forward;
        }

        if (Input.GetKey(KeyCode.S))
        {
            move -= transform.forward;
        }

        if (Input.GetKey(KeyCode.A))
        {
            move -= transform.right;
        }

        if (Input.GetKey(KeyCode.D))
        {
            move += transform.right;
        }

        if (Input.GetKey(KeyCode.E))
        {
            move += Vector3.up;
        }

        if (Input.GetKey(KeyCode.Q))
        {
            move -= Vector3.up;
        }

        transform.position += move * speed * Time.deltaTime;
    }

    private void HandleFreeZoom()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.01f)
        {
            return;
        }

        transform.position += transform.forward * scroll * freeZoomSpeed;
    }

    private void HandleFreeOrbit()
    {
        if (!enableFreeOrbit)
        {
            return;
        }

        bool orbitModifier =
            Input.GetKey(KeyCode.LeftAlt) ||
            Input.GetKey(KeyCode.RightAlt) ||
            Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl);

        bool orbitingNow = orbitModifier && Input.GetMouseButton(0);

        if (!orbitingNow)
        {
            wasOrbitingLastFrame = false;
            return;
        }

        if (!wasOrbitingLastFrame)
        {
            SyncFreeOrbitFromCurrentCamera();
            wasOrbitingLastFrame = true;
        }

        float mouseX = Input.GetAxis("Mouse X") * freeOrbitSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * freeOrbitSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -85.0f, 85.0f);

        ApplyFreeOrbitPosition();
    }

    private void SyncFreeOrbitFromCurrentCamera()
    {
        Vector3 pivot = GetFreeOrbitPoint();

        freeOrbitDistance = Vector3.Distance(transform.position, pivot);
        freeOrbitDistance = Mathf.Clamp(
            freeOrbitDistance,
            minFreeOrbitDistance,
            maxFreeOrbitDistance
        );

        Vector3 lookDirection = pivot - transform.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            Vector3 euler = lookRotation.eulerAngles;

            yaw = euler.y;
            pitch = NormalizePitch(euler.x);
        }
        else
        {
            SyncAnglesFromCurrentCamera();
        }
    }

    private void ApplyFreeOrbitPosition()
    {
        Vector3 pivot = GetFreeOrbitPoint();

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0.0f);
        Vector3 offset = rotation * new Vector3(0.0f, 0.0f, -freeOrbitDistance);

        transform.position = pivot + offset;
        transform.rotation = rotation;
    }

    private Vector3 GetFreeOrbitPoint()
    {
        if (freeOrbitTarget != null)
        {
            return freeOrbitTarget.position;
        }

        if (followTarget != null)
        {
            return followTarget.position;
        }

        return fallbackOrbitPoint;
    }

    private void SyncAnglesFromCurrentCamera()
    {
        Vector3 euler = transform.rotation.eulerAngles;

        yaw = euler.y;
        pitch = NormalizePitch(euler.x);
    }

    private float NormalizePitch(float angle)
    {
        if (angle > 180.0f)
        {
            angle -= 360.0f;
        }

        return angle;
    }

    private void LockCursor()
    {
        if (!lockCursorWhileOrbiting)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorLockedByThisScript = true;
    }

    private void UnlockCursorIfNeeded()
    {
        if (!cursorLockedByThisScript)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorLockedByThisScript = false;
    }

    private void SetupGUIStyles()
    {
        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = uiFontSize;
        labelStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = uiFontSize;
    }

    private void OnGUI()
    {
        if (!showCameraUI)
        {
            return;
        }

        if (labelStyle == null || buttonStyle == null)
        {
            SetupGUIStyles();
        }

        GUILayout.BeginArea(new Rect(Screen.width - 310, 20, 290, 125), GUI.skin.box);

        GUILayout.Label("Camera Mode: " + currentMode, labelStyle);

        if (GUILayout.Button("Switch Camera (" + switchModeKey + ")", buttonStyle, GUILayout.Height(32)))
        {
            ToggleCameraMode();
        }

        if (currentMode == CameraMode.ThirdPersonFollow)
        {
            if (holdRightMouseToOrbit)
            {
                GUILayout.Label("Third Person: Hold RMB + Mouse", labelStyle);
            }
            else
            {
                GUILayout.Label("Third Person: Mouse Look Always", labelStyle);
            }

            GUILayout.Label("WASD = Move, Wheel = Zoom", labelStyle);
        }
        else
        {
            GUILayout.Label("Free: RMB + WASD/QE, Wheel zoom", labelStyle);
        }

        GUILayout.EndArea();
    }

    private void OnDisable()
    {
        UnlockCursorIfNeeded();
    }
}