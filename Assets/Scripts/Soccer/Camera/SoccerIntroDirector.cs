using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

public class SoccerIntroDirector : MonoBehaviour
{
    [Header("Reference Settings")]
    public Transform envCenter; 
    public GameObject attacker; 
    public GameObject defender;

    [Header("Snappy Settings")]
    public float scaleDuration = 0.5f;
    public float rotationDuration = 0.6f; 
    public float zoomOutFOV = 75f;       
    public float impactFOV = 45f;        
    public Vector3 localOffset = new Vector3(0, 1.5f, 0);

    private Camera cam;
    private float defaultFOV;
    private int step = 0; 
    private bool isProcessing = false;

    struct PoseInfo { public Transform t; public Vector3 localPos; public Quaternion localRot; }
    private List<PoseInfo> attackerPose = new List<PoseInfo>();
    private List<PoseInfo> defenderPose = new List<PoseInfo>();

    void Awake()
    {
        cam = GetComponent<Camera>();
        defaultFOV = cam.fieldOfView;
    }

    void Start()
    {
        SaveInitialPose(attacker, attackerPose);
        SaveInitialPose(defender, defenderPose);
        ResetAgentState(attacker, attackerPose);
        ResetAgentState(defender, defenderPose);

        UpdateCameraPosition();
        transform.rotation = Quaternion.LookRotation(envCenter.forward); 
    }

    void SaveInitialPose(GameObject root, List<PoseInfo> list)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            list.Add(new PoseInfo { t = t, localPos = t.localPosition, localRot = t.localRotation });
    }

    void ResetAgentState(GameObject root, List<PoseInfo> poseList)
    {
        // 1. AI 및 결정 요청 중단
        if (root.TryGetComponent<Agent>(out var agent)) agent.enabled = false;

        root.SetActive(false);
        root.transform.localScale = Vector3.zero;

        // 2. 모든 관절 위치 강제 스냅
        foreach (var p in poseList) {
            p.t.localPosition = p.localPos;
            p.t.localRotation = p.localRot;
        }

        // 3. 물리 엔진 완전 봉쇄
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            // [중요] 충돌 감지 자체를 꺼서 조인트가 반응하지 못하게 함
            rb.detectCollisions = false; 
        }

        // 4. [수정] 조인트 .enabled 삭제 (컴파일 에러 해결)
        // 조인트를 건드리는 대신 리지드바디의 충돌과 키네마틱 설정만으로 충분합니다.
    }

    void UpdateCameraPosition() => transform.position = envCenter.position + localOffset;

    void Update()
    {
        UpdateCameraPosition();
        if (Input.GetKeyDown(KeyCode.T) && !isProcessing)
        {
            if (step == 0) StartCoroutine(Step1_AttackerPop());
            else if (step == 1) StartCoroutine(Step2_RotateAndDefenderPop());
            else ResetAll();
        }
    }

    IEnumerator Step1_AttackerPop()
    {
        isProcessing = true;
        StartCoroutine(PopIn(attacker));
        yield return StartCoroutine(CameraZoomKick());
        step = 1;
        isProcessing = false;
    }

    IEnumerator Step2_RotateAndDefenderPop()
    {
        isProcessing = true;
        
        Quaternion startRot = transform.rotation;
        Quaternion endRot = Quaternion.LookRotation(-envCenter.forward); 
        
        float elapsed = 0f;
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rotationDuration;
            float curve = Mathf.SmoothStep(0f, 1f, t);
            transform.rotation = Quaternion.Slerp(startRot, endRot, curve);

            float fovCurve = Mathf.Sin(t * Mathf.PI); 
            cam.fieldOfView = Mathf.Lerp(defaultFOV, zoomOutFOV, fovCurve);

            yield return null;
        }
        transform.rotation = endRot;

        StartCoroutine(PopIn(defender));
        yield return StartCoroutine(CameraZoomKick());

        step = 2;
        isProcessing = false;
    }

    IEnumerator CameraZoomKick()
    {
        float elapsed = 0f;
        float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float fovValue = Mathf.Sin(t * Mathf.PI);
            cam.fieldOfView = Mathf.Lerp(defaultFOV, impactFOV, fovValue);
            yield return null;
        }
        cam.fieldOfView = defaultFOV;
    }

    IEnumerator PopIn(GameObject agent)
    {
        agent.SetActive(true);
        float elapsed = 0f;
        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleDuration;
            float s = t - 1f;
            float curve = s * s * ((1.70158f + 1f) * s + 1.70158f) + 1f;
            agent.transform.localScale = Vector3.one * curve;
            yield return null;
        }
        agent.transform.localScale = Vector3.one;
    }

    void ResetAll()
    {
        ResetAgentState(attacker, attackerPose);
        ResetAgentState(defender, defenderPose);
        transform.rotation = Quaternion.LookRotation(envCenter.forward);
        cam.fieldOfView = defaultFOV;
        step = 0;
    }
}