using UnityEngine;

public class GoalDetectionWalk : MonoBehaviour 
{
    private void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponentInParent<NupJukEWalkAgent>();

        if (agent != null)
        {
            string partName = other.name.ToLower();

            if (partName.Contains("hip") || partName.Contains("leg"))
            {
                Debug.Log($"하체 (+10점): {other.name}");
                agent.SetReward(10f);
                agent.EndEpisode();
            }
            else if (partName.Contains("spine1"))
            {
                Debug.Log($"몸통 (+5점): {other.name}");
                agent.SetReward(10f);
                agent.EndEpisode();
            }
            else
            {
                Debug.Log("???");
            }
        }
    }
}