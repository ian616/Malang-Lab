using UnityEngine;

public class MiddleGoalDetectionJump : MonoBehaviour
{
    public GameObject particleEffect;

    private void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponentInParent<NupJukEJumpAgent>();
        if (agent == null || agent.target == null || agent.spine1Rb == null) return;

        string partName = other.name.ToLower();

        bool isExcluded = partName.Contains("head") || 
                          partName.Contains("hand") || 
                          partName.Contains("arm");

        if (isExcluded) return; 

        float currentUpDot = Vector3.Dot(agent.spine1Rb.transform.up, Vector3.up);

        if (currentUpDot <= 0.8f)
        {
            agent.AddReward(-0.1f);
            return;
        }

        Vector3 toTarget = (agent.target.transform.position - agent.spine1Rb.position).normalized;
        toTarget.y = 0;
        float lookAtDot = Vector3.Dot(agent.spine1Rb.transform.forward, toTarget.normalized);
        float lookWeight = Mathf.Clamp01(lookAtDot);

        if (lookWeight <= 0.6f) return;

        float velTowards = Vector3.Dot(agent.spine1Rb.linearVelocity, toTarget);
        float speedFactor = Mathf.Max(0, velTowards);

        float finalReward = 3 * speedFactor * currentUpDot * lookWeight;

        agent.AddReward(finalReward);
        Debug.Log($"[중간골] 성공! 부위:{partName} | 속도:{speedFactor:F2} * 자세:{currentUpDot:F2} * 방향:{lookWeight:F2} = 보상:{finalReward:F2}");

        if (particleEffect != null)
        {
            Instantiate(particleEffect, transform.position, Quaternion.identity);
        }

        gameObject.SetActive(false);
    }
}