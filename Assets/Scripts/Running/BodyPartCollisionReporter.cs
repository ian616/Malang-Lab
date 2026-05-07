using UnityEngine;

public class BodyPartCollisionReporter : MonoBehaviour
{
    public RunningAgent agent;

    void OnCollisionEnter(Collision collision)
    {
        string tag = collision.collider.tag;
        int layer = collision.collider.gameObject.layer;
        if (tag != "Ground" && tag != "Goal" && layer != LayerMask.NameToLayer("Agent") && layer != LayerMask.NameToLayer("Obstacle"))
            agent.OnBodyPartHitObstacle();
    }
}
