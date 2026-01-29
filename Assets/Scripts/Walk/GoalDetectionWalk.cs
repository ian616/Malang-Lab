using UnityEngine;
using Unity.MLAgents;
using System.Collections;

public class GoalDetectionWalk : MonoBehaviour
{
    [Header("Materials")]
    public Material matGoalInactive;
    public Material matGoalActive;

    [Header("Animation Settings")]
    public float animationDuration = 0.4f;
    public float pressedDepth = 0.08f;
    public float targetEmissionIntensity = 5.0f;

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
        StopAllCoroutines();
        _isTriggered = false;
        transform.localPosition = _originalPos;

        if (matGoalInactive != null)
        {
            _meshRenderer.material = new Material(matGoalInactive);
            _meshRenderer.material.DisableKeyword("_EMISSION");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponentInParent<NupJukEWalkAgent>();
        if (_isTriggered || agent == null || agent.spine1Rb == null) return;

        string name = other.name.ToLower();
        if (!name.Contains("foot")) return;

        float upDot = Vector3.Dot(agent.spine1Rb.transform.up, Vector3.up);
        if (upDot < 0.8f) return;

        Vector3 toGoal = (transform.position - agent.spine1Rb.position).normalized;
        toGoal.y = 0;
        float faceDot = Vector3.Dot(agent.spine1Rb.transform.forward, toGoal);
        if (faceDot < 0.8f) return;

        _isTriggered = true;
        agent.AddReward(successReward);
        Debug.Log($"[최종골] 성공! 자세:{upDot:F2} 방향:{faceDot:F2} 부위:{other.name}");
        StartCoroutine(AnimateAndEndEpisode(agent));
    }

    IEnumerator AnimateAndEndEpisode(NupJukEWalkAgent agent)
    {
        float elapsed = 0f;
        
        Vector3 startPos = _originalPos;
        Vector3 targetPos = _originalPos + new Vector3(0, -pressedDepth, 0);

        _meshRenderer.material = new Material(matGoalActive);
        _meshRenderer.material.EnableKeyword("_EMISSION");

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            transform.localPosition = Vector3.Lerp(startPos, targetPos, t * (2 - t));
            yield return null;
        }

        transform.localPosition = targetPos;
        agent.EndEpisode();
    }
}