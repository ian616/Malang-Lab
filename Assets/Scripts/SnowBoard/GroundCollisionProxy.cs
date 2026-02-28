using UnityEngine;

public class GroundCollisionProxy : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Head"))
        {
            var agent = collision.gameObject.GetComponentInParent<NupJukSnowBoardAgent>();
            
            if (agent != null)
            {
                agent.HandleHeadCollision();
            }
        }
    }
}