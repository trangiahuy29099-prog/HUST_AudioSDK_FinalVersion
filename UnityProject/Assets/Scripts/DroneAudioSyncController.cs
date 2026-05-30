using System.IO;
using UnityEngine;

public class DroneAudioSyncController : MonoBehaviour
{
    [Header("Listener / Human")]
    public Transform listenerTransform;
    public Transform listenerVisualRoot;

    [Header("Occlusion Debug")]
    public bool debugOcclusionRay = true;
    public string currentOcclusionHitName = "None";
    public string currentOcclusionMaterialName = "None";
    public float currentOcclusionAmount = 0.0f;
    [Header("Drone")]
    public Transform droneRoot;
    public Transform emitterPoint;
    public Rigidbody droneRigidbody;

    [Header("Camera")]
    public Camera mainCamera;
    public bool enableFollowCamera = false;
    public Vector3 cameraDirection = new Vector3(0.55f, 0.65f, -0.85f);
    public float minCameraDistance = 35.0f;
    public float maxCameraDistance = 90.0f;
    public float cameraDistanceMultiplier = 1.25f;
    public float cameraLookHeight = 3.5f;
    public float cameraSmooth = 5.0f;
    public float cameraFOV = 70.0f;

    [Header("Audio Features")]
    public bool playOnStart = true;
    public bool loopSound = true;

    [Header("Automatic Environment Detection")]
    public bool automaticOcclusion = true;
    public bool automaticReverb = true;

    [Header("Automatic Occlusion")]
    public GameObject occlusionWall;          // Gán OcclusionWoodWalls vào đây
    public LayerMask occlusionMask = ~0;
    public float maxOcclusionAmount = 0.85f;

    [Header("Automatic Reverb Zone")]
    public BoxCollider caveReverbZone;        // Gán CaveReverbZone vào đây
    public bool requireBothListenerAndDroneInCave = true;

    [Header("Optional Visuals")]
    public Light redLight;
    public Light blueLight;
    public TrailRenderer droneTrail;
    public Transform[] propellers;

    [Header("Propeller Visual")]
    public float propellerSpinSpeed = 1000.0f;

    private bool sdkReady = false;
    private bool audioPlaying = false;
    private bool reverbEnabled = false;
    private bool occlusionEnabled = false;
    private int hrtfEnabled = 0;

    private Vector3 previousEmitterPosition;
    private Vector3 previousListenerPosition;

    private Vector3 sourceVelocity;
    private Vector3 listenerVelocity;

    [Header("Debug UI")]
    // Quan trong
    public bool showDebugUI = false;

    private GUIStyle labelStyle;
    private GUIStyle buttonStyle;

    private void Start()
    {
        // KHÔNG gọi SetupGUIStyles() ở đây.
        // GUI.skin chỉ được dùng bên trong OnGUI().

        if (mainCamera != null)
        {
            mainCamera.fieldOfView = cameraFOV;
        }

        if (occlusionWall != null)
        {
            // Tường gỗ phải luôn bật để raycast kiểm tra occlusion.
            occlusionWall.SetActive(true);
        }

        if (redLight != null)
        {
            redLight.enabled = false;
        }

        if (blueLight != null)
        {
            blueLight.enabled = false;
        }

        if (droneTrail != null)
        {
            droneTrail.enabled = true;
        }

        if (emitterPoint != null)
        {
            previousEmitterPosition = emitterPoint.position;
        }
        else if (droneRoot != null)
        {
            previousEmitterPosition = droneRoot.position;
        }

        if (listenerTransform != null)
        {
            previousListenerPosition = listenerTransform.position;
        }

        int initOk = AudioSDKBridge.AudioSDK_Init();

        if (initOk == 0)
        {
            Debug.LogError("[DroneAudioSync] AudioSDK_Init failed.");
            sdkReady = false;
            return;
        }

        hrtfEnabled = AudioSDKBridge.AudioSDK_IsHRTFEnabled();

        string wavPath = Path.Combine(Application.streamingAssetsPath, "siren.wav");
        wavPath = Path.GetFullPath(wavPath);

        Debug.Log("[DroneAudioSync] Trying to load wav: " + wavPath);
        Debug.Log("[DroneAudioSync] File exists: " + File.Exists(wavPath));

        if (!File.Exists(wavPath))
        {
            Debug.LogError("[DroneAudioSync] siren.wav not found at: " + wavPath);
            sdkReady = false;
            return;
        }

        byte[] wavBytes = File.ReadAllBytes(wavPath);
        int loadOk = AudioSDKBridge.AudioSDK_LoadSoundFromMemory(wavBytes, wavBytes.Length);

        Debug.Log("[DroneAudioSync] AudioSDK_LoadSoundFromMemory returned: " + loadOk);

        if (loadOk == 0)
        {
            Debug.LogError("[DroneAudioSync] Failed to load sound from memory.");
            sdkReady = false;
            return;
        }

        AudioSDKBridge.AudioSDK_SetLooping(loopSound ? 1 : 0);

        AudioSDKBridge.AudioSDK_SetReverb(0);
        AudioSDKBridge.AudioSDK_SetOcclusion(0.0f);

        reverbEnabled = false;
        occlusionEnabled = false;

        sdkReady = true;

        Debug.Log("[DroneAudioSync] HUSTAudioSDK initialized. HRTF: " + hrtfEnabled);

        UpdateAudioTransforms();

        if (playOnStart)
        {
            PlayAudio();
        }
    }

    private void Update()
    {
        if (!sdkReady)
        {
            return;
        }

        UpdateVelocities();
        UpdateAudioTransforms();

        UpdateAutomaticOcclusion();
        UpdateAutomaticReverb();

        UpdateOptionalVisuals();

        if (enableFollowCamera)
        {
            UpdateFollowCamera();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            ToggleAudio();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            enableFollowCamera = !enableFollowCamera;
        }
    }

    private void UpdateVelocities()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        Vector3 currentEmitterPosition = GetEmitterPosition();
        Vector3 currentListenerPosition = listenerTransform != null ? listenerTransform.position : Vector3.zero;

        // Nếu drone đang được autopilot điều khiển bằng Rigidbody kinematic,
        // linearVelocity thường sẽ bằng 0. Vì vậy phải tính velocity bằng delta position.
        if (droneRigidbody != null && !droneRigidbody.isKinematic)
        {
            sourceVelocity = droneRigidbody.linearVelocity;
        }
        else
        {
            sourceVelocity = (currentEmitterPosition - previousEmitterPosition) / dt;
        }

        listenerVelocity = (currentListenerPosition - previousListenerPosition) / dt;

        previousEmitterPosition = currentEmitterPosition;
        previousListenerPosition = currentListenerPosition;
    }

    private void UpdateAudioTransforms()
    {
        if (listenerTransform == null || emitterPoint == null)
        {
            return;
        }

        Vector3 sdkSourcePos = AudioSDKBridge.UnityToSDKPosition(GetEmitterPosition());
        Vector3 sdkSourceVel = AudioSDKBridge.UnityToSDKDirection(sourceVelocity);

        AudioSDKBridge.AudioSDK_SetSourcePosition(
            sdkSourcePos.x,
            sdkSourcePos.y,
            sdkSourcePos.z
        );

        AudioSDKBridge.AudioSDK_SetSourceVelocity(
            sdkSourceVel.x,
            sdkSourceVel.y,
            sdkSourceVel.z
        );

        Vector3 sdkListenerPos = AudioSDKBridge.UnityToSDKPosition(listenerTransform.position);
        Vector3 sdkListenerVel = AudioSDKBridge.UnityToSDKDirection(listenerVelocity);

        AudioSDKBridge.AudioSDK_SetListenerPosition(
            sdkListenerPos.x,
            sdkListenerPos.y,
            sdkListenerPos.z
        );

        AudioSDKBridge.AudioSDK_SetListenerVelocity(
            sdkListenerVel.x,
            sdkListenerVel.y,
            sdkListenerVel.z
        );

        Transform orientationSource = listenerVisualRoot != null ? listenerVisualRoot : listenerTransform;

        Vector3 sdkForward = AudioSDKBridge.UnityToSDKDirection(orientationSource.forward);
        Vector3 sdkUp = AudioSDKBridge.UnityToSDKDirection(orientationSource.up);

        AudioSDKBridge.AudioSDK_SetListenerOrientation(
            sdkForward.x,
            sdkForward.y,
            sdkForward.z,
            sdkUp.x,
            sdkUp.y,
            sdkUp.z
        );
    }

    private Vector3 GetEmitterPosition()
    {
        if (emitterPoint != null)
        {
            return emitterPoint.position;
        }

        if (droneRoot != null)
        {
            return droneRoot.position;
        }

        return Vector3.zero;
    }

    private void UpdateAutomaticOcclusion()
    {
        if (!sdkReady || !automaticOcclusion)
        {
            return;
        }

        float amount = ComputeAutomaticOcclusionAmount();

        AudioSDKBridge.AudioSDK_SetOcclusion(amount);

        occlusionEnabled = amount > 0.05f;
    }

    private float ComputeAutomaticOcclusionAmount()
    {
        currentOcclusionHitName = "None";
        currentOcclusionMaterialName = "None";
        currentOcclusionAmount = 0.0f;

        if (listenerTransform == null || emitterPoint == null)
        {
            return 0.0f;
        }

        Vector3 from = GetEmitterPosition();
        Vector3 to = listenerTransform.position;

        Vector3 direction = to - from;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
        {
            return 0.0f;
        }

        Ray ray = new Ray(from, direction.normalized);

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            distance,
            occlusionMask,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            if (!hit.collider.enabled)
            {
                continue;
            }

            if (!hit.collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (hit.collider.isTrigger)
            {
                continue;
            }

            AudioOcclusionMaterial material =
                hit.collider.GetComponentInParent<AudioOcclusionMaterial>();

            if (material != null)
            {
                float amount = material.GetOcclusionAmount();

                currentOcclusionHitName = hit.collider.gameObject.name;
                currentOcclusionMaterialName = material.GetMaterialName();
                currentOcclusionAmount = amount;

                if (debugOcclusionRay)
                {
                    Debug.DrawLine(from, to, Color.red);
                }

                return Mathf.Clamp01(amount);
            }

            // Fallback cho hệ cũ nếu vẫn dùng occlusionWall parent.
            if (IsOcclusionObject(hit.collider.transform))
            {
                currentOcclusionHitName = hit.collider.gameObject.name;
                currentOcclusionMaterialName = "Legacy";
                currentOcclusionAmount = maxOcclusionAmount;

                if (debugOcclusionRay)
                {
                    Debug.DrawLine(from, to, Color.red);
                }

                return Mathf.Clamp01(maxOcclusionAmount);
            }
        }

        if (debugOcclusionRay)
        {
            Debug.DrawLine(from, to, Color.green);
        }

        return 0.0f;
    }
    private bool IsOcclusionObject(Transform hitTransform)
    {
        if (hitTransform == null || occlusionWall == null)
        {
            return false;
        }

        Transform root = occlusionWall.transform;

        // Chỉ nhận object con, không nhận chính parent.
        // Tránh lỗi parent có collider vô hình gây occlusion giả.
        return hitTransform.IsChildOf(root) && hitTransform != root;
    }

    private void UpdateAutomaticReverb()
    {
        if (!sdkReady || !automaticReverb || caveReverbZone == null)
        {
            return;
        }

        if (listenerTransform == null || emitterPoint == null)
        {
            return;
        }

        bool listenerInside = IsPointInsideBoxCollider(caveReverbZone, listenerTransform.position);
        bool droneInside = IsPointInsideBoxCollider(caveReverbZone, GetEmitterPosition());

        bool shouldEnableReverb;

        if (requireBothListenerAndDroneInCave)
        {
            shouldEnableReverb = listenerInside && droneInside;
        }
        else
        {
            shouldEnableReverb = listenerInside || droneInside;
        }

        if (shouldEnableReverb == reverbEnabled)
        {
            return;
        }

        reverbEnabled = shouldEnableReverb;

        AudioSDKBridge.AudioSDK_SetReverb(reverbEnabled ? 1 : 0);

        Debug.Log("[DroneAudioSync] Auto Reverb: " + reverbEnabled);
    }

    private bool IsPointInsideBoxCollider(BoxCollider box, Vector3 worldPoint)
    {
        if (box == null)
        {
            return false;
        }

        Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint);
        localPoint -= box.center;

        Vector3 halfSize = box.size * 0.5f;

        bool insideX = Mathf.Abs(localPoint.x) <= halfSize.x;
        bool insideY = Mathf.Abs(localPoint.y) <= halfSize.y;
        bool insideZ = Mathf.Abs(localPoint.z) <= halfSize.z;

        return insideX && insideY && insideZ;
    }

    private void UpdateOptionalVisuals()
    {
        if (redLight != null)
        {
            redLight.enabled = occlusionEnabled;
        }

        if (blueLight != null)
        {
            blueLight.enabled = reverbEnabled;
        }

        if (propellers != null)
        {
            for (int i = 0; i < propellers.Length; i++)
            {
                if (propellers[i] != null)
                {
                    propellers[i].Rotate(Vector3.up, propellerSpinSpeed * Time.deltaTime, Space.Self);
                }
            }
        }
    }

    private void UpdateFollowCamera()
    {
        if (mainCamera == null || listenerTransform == null || droneRoot == null)
        {
            return;
        }

        Vector3 center = (listenerTransform.position + droneRoot.position) * 0.5f;

        float targetDistance = Vector3.Distance(listenerTransform.position, droneRoot.position) * cameraDistanceMultiplier;
        targetDistance = Mathf.Clamp(targetDistance, minCameraDistance, maxCameraDistance);

        Vector3 direction = cameraDirection.normalized;
        Vector3 targetPosition = center - direction * targetDistance;
        targetPosition.y += cameraLookHeight;

        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            targetPosition,
            cameraSmooth * Time.deltaTime
        );

        Vector3 lookTarget = center + Vector3.up * cameraLookHeight;

        Quaternion targetRotation = Quaternion.LookRotation(
            lookTarget - mainCamera.transform.position,
            Vector3.up
        );

        mainCamera.transform.rotation = Quaternion.Slerp(
            mainCamera.transform.rotation,
            targetRotation,
            cameraSmooth * Time.deltaTime
        );

        mainCamera.fieldOfView = cameraFOV;
    }

    public void PlayAudio()
    {
        if (!sdkReady)
        {
            return;
        }

        AudioSDKBridge.AudioSDK_Play();
        audioPlaying = true;

        Debug.Log("[DroneAudioSync] Audio Playing: True");
    }

    public void StopAudio()
    {
        if (!sdkReady)
        {
            return;
        }

        AudioSDKBridge.AudioSDK_Stop();
        audioPlaying = false;

        Debug.Log("[DroneAudioSync] Audio Playing: False");
    }

    private void ToggleAudio()
    {
        if (audioPlaying)
        {
            StopAudio();
        }
        else
        {
            PlayAudio();
        }
    }

    private void SetupGUIStyles()
    {
        // Hàm này chỉ được gọi trong OnGUI().
        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 18;
        labelStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 18;
    }

    private void OnGUI()
    {
        if (!showDebugUI)
        {
            return;
        }

        if (labelStyle == null || buttonStyle == null)
        {
            SetupGUIStyles();
        }

        GUILayout.BeginArea(new Rect(20, 20, 620, 430), GUI.skin.box);

        GUILayout.Label("HUST 3D Audio Game Engine SDK - Drone Sync Demo", labelStyle);
        GUILayout.Space(10);

        GUILayout.Label("Audio Playing: " + audioPlaying, labelStyle);
        GUILayout.Label("Reverb: " + (reverbEnabled ? "Auto ON" : "Auto OFF"), labelStyle);
        GUILayout.Label("Occlusion Hit: " + currentOcclusionHitName, labelStyle);
        GUILayout.Label("Occlusion Material: " + currentOcclusionMaterialName, labelStyle);
        GUILayout.Label("Occlusion Amount: " + currentOcclusionAmount.ToString("F2"), labelStyle);
        GUILayout.Label("Follow Camera: " + enableFollowCamera, labelStyle);
        GUILayout.Label("HRTF: " + (sdkReady ? hrtfEnabled.ToString() : "N/A"), labelStyle);

        GUILayout.Space(10);

        GUILayout.Label("Source Position: " + GetEmitterPosition().ToString("F2"), labelStyle);
        GUILayout.Label("Velocity: " + sourceVelocity.ToString("F2"), labelStyle);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(audioPlaying ? "Stop Audio" : "Play Audio", buttonStyle, GUILayout.Height(35)))
        {
            ToggleAudio();
        }

        if (GUILayout.Button("Camera", buttonStyle, GUILayout.Height(35)))
        {
            enableFollowCamera = !enableFollowCamera;
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label("Drone = WASD / Space modifier", labelStyle);
        GUILayout.Label("Human = Arrow Keys", labelStyle);
        GUILayout.Label("Head Listener = Q / E", labelStyle);
        GUILayout.Label("Audio = custom C++ OpenAL HUSTAudioSDK", labelStyle);

        GUILayout.EndArea();
    }

    private void OnApplicationQuit()
    {
        if (sdkReady)
        {
            AudioSDKBridge.AudioSDK_Stop();
            AudioSDKBridge.AudioSDK_Shutdown();
        }

        sdkReady = false;
    }
}