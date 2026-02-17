using UnityEngine;

public class GoalDetector : MonoBehaviour
{
    [Header("에이전트 연결")]
    public NupJukESoccerAgent attacker; 
    public NupJukESoccerAgent defender;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Debug.Log("<color=red>⚽ GOAL!</color>");

            // 1. 공 리셋 (Ball 스크립트 참조)
            Ball ball = other.GetComponent<Ball>();
            // if (ball != null) ball.ResetBall();

            // 2. 공격수(Red) 득점 보상
            if (attacker != null) attacker.AddReward(10.0f);

            // 3. 수비수(Blue) 실점 감점
            if (defender != null) defender.AddReward(-10.0f);

            // 4. 양쪽 모두 에피소드 리셋
            // if (attacker != null) attacker.EndEpisode();
            // if (defender != null) defender.EndEpisode();
        }
    }
}