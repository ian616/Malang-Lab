using UnityEngine;
using System.Collections;

public class BellygomFaceHandler : MonoBehaviour
{
    [Header("Agent & Physics")]
    private RunningAgent _agent; // 제공해주신 에이전트 클래스

    [Header("Face Visuals")]
    public SkinnedMeshRenderer mouthRenderer;
    public Material normalMouth;
    public Material surprisedMouth;

    void Start()
    {
        // 부모 오브젝트에서 에이전트 스크립트를 찾아옵니다.
        _agent = GetComponentInParent<RunningAgent>();

        // 시작할 때 입 모양을 기본으로 세팅
        if (mouthRenderer != null && normalMouth != null)
            mouthRenderer.material = normalMouth;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 2. 에이전트 로직 실행 (HandleHeadCollision 호출로 학습 페널티 부여)
        if (_agent != null)
        {
            Debug.Log($"[CRASH] {_agent.name}의 머리가 바닥에 닿음!");
        }

        // 3. 시각적 연출 (입 모양 변경 코루틴 실행)
        if (mouthRenderer != null && surprisedMouth != null)
        {
            StopAllCoroutines(); // 이전 표정 변화가 있다면 중지
            StartCoroutine(ChangeFaceRoutine());
        }
    }

    IEnumerator ChangeFaceRoutine()
    {
        // 입 모양을 놀란 표정으로 변경
        mouthRenderer.material = surprisedMouth;

        // 에피소드가 리셋되기 전 찰나의 순간 혹은 촬영용으로 1초 유지
        // (학습 중에는 EndEpisode가 호출되면 즉시 리셋되니 참고하세요!)
        yield return new WaitForSeconds(5.0f);

        // 다시 평소 표정으로 복구
        mouthRenderer.material = normalMouth;
    }
}