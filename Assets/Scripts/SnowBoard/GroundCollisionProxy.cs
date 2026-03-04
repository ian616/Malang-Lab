using UnityEngine;

public class GroundCollisionProxy : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name == "Head")
        {
            var agent = collision.gameObject.GetComponentInParent<NupJukSnowBoardAgent>();
            
            if (agent != null)
            {
                agent.HandleHeadCollision();
                Debug.Log("아우취! 머리 박았다!");
            }
        }
    }
}