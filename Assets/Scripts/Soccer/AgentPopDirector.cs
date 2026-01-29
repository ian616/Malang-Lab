using System.Collections;
using UnityEngine;

public class AgentSequentialPopDirector : MonoBehaviour
{
    public GameObject[] agents = new GameObject[2];
    public float scaleDuration = 0.5f;

    private int currentIndex = 0;
    private Vector3[] originalScales;
    private bool isTransitioning = false;

    void Start()
    {
        originalScales = new Vector3[agents.Length];

        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i] == null) continue;

            originalScales[i] = agents[i].transform.localScale;
            
            // 물리 엔진 완전 비활성화 유지
            Rigidbody[] rbs = agents[i].GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rbs)
            {
                rb.isKinematic = true; 
            }

            // 초기 상태: 꺼짐 & 스케일 0
            agents[i].transform.localScale = Vector3.zero;
            agents[i].SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T) && !isTransitioning)
        {
            // 1. 아직 나타날 에이전트가 남은 경우
            if (currentIndex < agents.Length)
            {
                if (agents[currentIndex] != null)
                {
                    StartCoroutine(PopIn(agents[currentIndex], originalScales[currentIndex]));
                    currentIndex++;
                }
            }
            // 2. 모든 에이전트가 나온 상태에서 T를 누르면 초기화
            else
            {
                ResetAgents();
            }
        }
    }

    IEnumerator PopIn(GameObject agent, Vector3 targetScale)
    {
        isTransitioning = true;
        agent.SetActive(true);
        float elapsed = 0f;

        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleDuration;
            
            // 뿅! 하고 튀어나오는 탄성 커브 (Back Out 효과)
            float s = t - 1f;
            float curve = s * s * ((1.70158f + 1f) * s + 1.70158f) + 1f;
            
            agent.transform.localScale = targetScale * curve;
            yield return null;
        }

        agent.transform.localScale = targetScale;
        isTransitioning = false;
    }

    void ResetAgents()
    {
        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i] == null) continue;
            
            agents[i].transform.localScale = Vector3.zero;
            agents[i].SetActive(false);
        }
        
        currentIndex = 0;
    }
}