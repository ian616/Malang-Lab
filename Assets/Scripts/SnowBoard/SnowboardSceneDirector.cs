using UnityEngine;
using Unity.Cinemachine;

public class NupjukSimpleDirector : MonoBehaviour
{
    private CinemachineBrain brain;
    private bool isActive = false;
    private float defaultFOV;

    [Header("애니메이션 설정")]
    public float lerpSpeed = 5f;      // 애니메이션 속도 (높을수록 빠름)
    public float zoomFOV = 25f;       // 목표 확대 FOV
    public float slowMoScale = 0.2f;  // 목표 슬로우 모션

    private float targetFOV;
    private float targetTimeScale = 1f;

    void Start()
    {
        brain = GetComponent<CinemachineBrain>();
        
        // 시작할 때의 FOV를 기본값으로 저장
        var vcam = brain.ActiveVirtualCamera as CinemachineCamera;
        if (vcam != null)
        {
            defaultFOV = vcam.Lens.FieldOfView;
            targetFOV = defaultFOV;
        }
    }

    void Update()
    {
        // J키 토글
        if (Input.GetKeyDown(KeyCode.J))
        {
            isActive = !isActive;
            targetFOV = isActive ? zoomFOV : defaultFOV;
            targetTimeScale = isActive ? slowMoScale : 1f;
        }

        // --- 부드러운 애니메이션 처리 (Lerp) ---
        var vcam = brain.ActiveVirtualCamera as CinemachineCamera;
        if (vcam == null) return;

        // 1. FOV 부드럽게 변경
        // Time.unscaledDeltaTime을 써야 슬로우 모션 중에도 애니메이션 속도가 일정함
        vcam.Lens.FieldOfView = Mathf.Lerp(vcam.Lens.FieldOfView, targetFOV, Time.unscaledDeltaTime * lerpSpeed);

        // 2. TimeScale 부드럽게 변경
        Time.timeScale = Mathf.Lerp(Time.timeScale, targetTimeScale, Time.unscaledDeltaTime * lerpSpeed);
    }
}