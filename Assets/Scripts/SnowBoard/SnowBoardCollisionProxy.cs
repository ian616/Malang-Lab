using UnityEngine;

public class SnowboardCollisionProxy : MonoBehaviour
{
    public NupJukSnowBoardAgent agent;

    private void OnCollisionEnter(Collision collision)
    {
        if (agent != null) agent.HandleBoardCollision(collision);
    }
}