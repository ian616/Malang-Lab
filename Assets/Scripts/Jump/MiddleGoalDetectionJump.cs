using UnityEngine;

public class MiddleGoalDetectionJump : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponentInParent<NupJukEJumpAgent>();

        if (agent != null && !agent.reachedMiddleGoal)
        {
            string partName = other.name.ToLower();


            float currentUpDot = Vector3.Dot(agent.spine1Rb.transform.up, Vector3.up);

            if (currentUpDot > 0.9f)
            {
                agent.AddReward(currentUpDot * 5f);
                Debug.Log($"중간 골 획득! 보상: {currentUpDot * 5f:F2}");

            }

            gameObject.SetActive(false);
        }
    }
}