using UnityEngine;
using System.Collections;

public class FollowingCam : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 5, -7);

    [Header("Smooth Settings")]
    public float smoothSpeed = 0.125f;
    public bool lookAtTarget = true;

    [Header("1. Impact Slow")]
    public float zoomFOV = 30f;
    public float zoomSmooth = 5f;
    public float slowTimeScale = 0.2f;
    public float slowDuration = 0.5f; 

    [Header("2. Post-Slow Orbit")]
    public float rotationDelay = 1.0f;    
    public float rotationDuration = 1.2f; 

    private Camera cam;
    private float defaultFOV;
    private float targetFOV;
    private float currentAngle = 0f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        defaultFOV = cam.fieldOfView;
        targetFOV = defaultFOV;
        Time.fixedDeltaTime = 0.005f;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 현재 각도 반영
        Vector3 rotatedOffset = Quaternion.Euler(0, currentAngle, 0) * offset;
        Vector3 desiredPosition = target.position + rotatedOffset;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.unscaledDeltaTime * zoomSmooth);

        if (lookAtTarget) transform.LookAt(target);
    }

    public void StartImpactEffect()
    {
        StopAllCoroutines();
        StartCoroutine(ImpactRoutine());
    }

    // 리셋 시 호출 (각도를 0으로 되돌림)
    public void ResetCamera()
    {
        StopAllCoroutines();
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.005f;
        targetFOV = defaultFOV;
        currentAngle = 0f; // 0도로 복구
        if (cam != null) cam.fieldOfView = defaultFOV;
    }

    private IEnumerator ImpactRoutine()
    {
        // STEP 1: 임팩트 슬로우
        targetFOV = zoomFOV;
        Time.timeScale = slowTimeScale;
        Time.fixedDeltaTime = 0.005f;
        yield return new WaitForSecondsRealtime(slowDuration);

        // STEP 2: 슬로우 해제
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.005f;
        targetFOV = defaultFOV;

        // STEP 3: 정상 속도에서 1초 대기
        yield return new WaitForSeconds(rotationDelay);

        // STEP 4: 180도 회전 (끝나고 돌아가지 않음)
        float elapsed = 0f;
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            currentAngle = Mathf.Lerp(0f, 180f, elapsed / rotationDuration);
            yield return null;
        }
        currentAngle = 180f; // 180도 고정
    }
}