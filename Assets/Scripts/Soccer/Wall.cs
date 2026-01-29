using UnityEngine;

public class Wall : MonoBehaviour
{
    [Header("Reward Settings")]
    public float wallPenalty = -3.0f;

    private void OnCollisionEnter(Collision collision)
    {
        var agent = collision.gameObject.GetComponentInParent<NupJukESoccerAgent>();

        if (agent != null)
        {
            agent.AddReward(wallPenalty);
            Debug.Log($"<color=red>[Wall]</color> {collision.gameObject.name}가 벽에 충돌! 에피소드 종료.");
            
            agent.EndEpisode();
        }
    }
}