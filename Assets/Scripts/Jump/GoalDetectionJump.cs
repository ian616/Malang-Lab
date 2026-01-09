using UnityEngine;
using Unity.MLAgents;
using System.Collections;

public class GoalDetectionJump : MonoBehaviour
{
    [Header("Materials")]
    public Material matGoalInactive; 
    public Material matGoalActive;   
    
    [Header("Animation Settings")]
    public float animationDuration = 0.15f;
    public float pressedDepth = 0.08f;

    [Header("Reward Settings")]
    public float successReward = 10.0f; 

    private MeshRenderer _meshRenderer;
    private Vector3 _originalPos;
    private bool _isTriggered = false;

    void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        _originalPos = transform.localPosition;
    }

    public void ResetGoal()
    {
        _isTriggered = false;
        transform.localPosition = _originalPos;
        if (matGoalInactive != null) _meshRenderer.material = matGoalInactive;
    }

    private void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponentInParent<NupJukEJumpAgent>();

        if (!_isTriggered && agent != null)
        {
            _isTriggered = true;
            agent.AddReward(successReward);
            
            StartCoroutine(AnimateAndEndEpisode(agent));
        }
    }

    IEnumerator AnimateAndEndEpisode(NupJukEJumpAgent agent)
    {
        float elapsed = 0f;
        Vector3 targetPos = _originalPos + new Vector3(0, -pressedDepth, 0);

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;

            transform.localPosition = Vector3.Lerp(_originalPos, targetPos, t);

            if (matGoalInactive != null && matGoalActive != null)
            {
                _meshRenderer.material.Lerp(matGoalInactive, matGoalActive, t);
            }

            yield return null;
        }

        transform.localPosition = targetPos;
        _meshRenderer.material = matGoalActive;

        yield return new WaitForSeconds(0.05f);
        agent.EndEpisode();
    }
}