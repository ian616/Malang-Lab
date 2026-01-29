using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneDirector : MonoBehaviour
{
    public Transform mainCamera;
    public GameObject[] otherEnvs; 
    public Transform closeUpMarker;
    public Transform farMarker;
    
    public float zoomDuration = 0.8f;
    public float scaleDuration = 0.3f;

    private List<GameObject> agentList = new List<GameObject>();
    private bool isFullView = false; // 현재 전체 화면인지 체크
    private bool isTransitioning = false; // 애니메이션 중인지 체크

    void Start()
    {
        for (int i = 0; i < otherEnvs.Length; i++)
        {
            if (otherEnvs[i] == null) continue;

            GameObject agentObj = FindAgentInEnv(otherEnvs[i].transform);
            agentList.Add(agentObj);

            if (i == 0) continue; 

            if (agentObj != null) agentObj.SetActive(false);
            otherEnvs[i].transform.localScale = Vector3.zero;
            otherEnvs[i].SetActive(false);
        }

        // 시작 시 카메라 위치 초기화
        if (closeUpMarker != null)
        {
            mainCamera.position = closeUpMarker.position;
            mainCamera.rotation = closeUpMarker.rotation;
        }
    }

    void Update()
    {
        // 애니메이션 중이 아닐 때만 T 키 입력 허용
        if (Input.GetKeyDown(KeyCode.T) && !isTransitioning)
        {
            if (!isFullView) StartCoroutine(ZoomOutAndShowAll()); // 전체 보기 실행
            else StartCoroutine(ZoomInAndHideAll()); // 처음 상태로 복귀 실행
        }
    }

    // [연출 1] 슉! 빠지면서 전체 나타나기
    IEnumerator ZoomOutAndShowAll()
    {
        isTransitioning = true;
        float elapsed = 0f;

        // 카메라 이동
        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / zoomDuration;
            mainCamera.position = Vector3.Lerp(closeUpMarker.position, farMarker.position, t * t);
            mainCamera.rotation = Quaternion.Lerp(closeUpMarker.rotation, farMarker.rotation, t * t);
            yield return null;
        }

        // 환경들 등장 (0번은 이미 있으니 1번부터)
        for (int i = 1; i < otherEnvs.Length; i++)
        {
            if (otherEnvs[i] != null)
            {
                otherEnvs[i].SetActive(true);
                GameObject targetAgent = (i < agentList.Count) ? agentList[i] : null;
                StartCoroutine(ScaleEnvAndThenLoadAgent(otherEnvs[i].transform, targetAgent));
                yield return new WaitForSeconds(0.05f);
            }
        }

        isFullView = true;
        isTransitioning = false;
    }

    // [연출 2] 다시 슉! 들어가면서 0번만 남기기
    IEnumerator ZoomInAndHideAll()
    {
        isTransitioning = true;

        // 1. 넙죽이들 먼저 끄고 환경들 스케일 0으로 줄이기 (역순)
        for (int i = otherEnvs.Length - 1; i >= 1; i--)
        {
            if (otherEnvs[i] != null)
            {
                // 넙죽이 먼저 비활성화 (물리 버그 방지)
                if (i < agentList.Count && agentList[i] != null) 
                    agentList[i].SetActive(false);
                
                StartCoroutine(ScaleDownAndHide(otherEnvs[i]));
            }
        }

        // 2. 카메라 다시 줌인
        float elapsed = 0f;
        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / zoomDuration;
            float curve = 1f - Mathf.Pow(1f - t, 2); // 들어올 때는 다른 느낌의 커브
            mainCamera.position = Vector3.Lerp(farMarker.position, closeUpMarker.position, curve);
            mainCamera.rotation = Quaternion.Lerp(farMarker.rotation, closeUpMarker.rotation, curve);
            yield return null;
        }

        isFullView = false;
        isTransitioning = false;
    }

    IEnumerator ScaleEnvAndThenLoadAgent(Transform envTf, GameObject agent)
    {
        float elapsed = 0f;
        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            envTf.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, elapsed / scaleDuration);
            yield return null;
        }
        envTf.localScale = Vector3.one;
        if (agent != null) agent.SetActive(true);
    }

    IEnumerator ScaleDownAndHide(GameObject env)
    {
        float elapsed = 0f;
        Transform t = env.transform;
        Vector3 initialScale = t.localScale;

        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            t.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsed / scaleDuration);
            yield return null;
        }
        t.localScale = Vector3.zero;
        env.SetActive(false);
    }

    GameObject FindAgentInEnv(Transform parent)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "NupjukE_Soccer") return child.gameObject;
        }
        return null;
    }
}