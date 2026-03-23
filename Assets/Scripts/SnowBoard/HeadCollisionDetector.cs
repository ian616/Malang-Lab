using UnityEngine;

public class HeadCollisionDetector : MonoBehaviour
{
    private NupJukSnowBoardAgent _agent;

    void Start()
    {
        _agent = GetComponentInParent<NupJukSnowBoardAgent>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (_agent != null)
            {
                _agent.HandleHeadCollision();
                Debug.Log($"[CRASH] {_agent.name}의 머리가 바닥에 닿음!");
            }
        }
    }
}