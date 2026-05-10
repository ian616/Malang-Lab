using UnityEngine;

public class BodyPartCollisionReporter : MonoBehaviour
{
    public RunningAgent agent;

    void OnCollisionEnter(Collision collision)
    {
        string tag = collision.collider.tag;
        int layer = collision.collider.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Obstacle"))
        {
            agent.OnBodyPartHitHurdle(gameObject.name);
            return;
        }

        if (tag != "Ground" && tag != "Goal" && layer != LayerMask.NameToLayer("Agent"))
            agent.OnBodyPartHitObstacle();
    }
}
