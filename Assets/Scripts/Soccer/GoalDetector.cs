using UnityEngine;

public class GoalDetector : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private string ballTag = "Ball"; 

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(ballTag))
        {
            Debug.Log("<color=green>⚽ GOAL! 공이 골대를 통과했습니다!</color>");
            
            NupJukESoccerAgent agent = FindObjectOfType<NupJukESoccerAgent>();

            if (agent != null)
            {
                agent.AddReward(10.0f);
                
                agent.EndEpisode();
                
                Debug.Log("<color=yellow>에이전트에게 보상 10점 지급 및 에피소드 리셋</color>");
            }
            else
            {
                Debug.LogWarning("NupJukESoccerAgent를 찾을 수 없습니다!");
            }
        }
    }
}