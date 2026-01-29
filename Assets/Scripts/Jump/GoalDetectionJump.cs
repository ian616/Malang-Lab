using UnityEngine;
using Unity.MLAgents;
using System.Collections;

public class GoalDetectionJump : MonoBehaviour
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
        _isTriggered = false;
        transform.localPosition = _originalPos;
        if (matGoalInactive != null)
        {
            _meshRenderer.material = new Material(matGoalInactive);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponentInParent<NupJukEJumpAgent>();
        if (_isTriggered || agent == null) return;

        string name = other.name.ToLower();
        bool isValidPart = name.Contains("foot");

        if (isValidPart)
        {
            _isTriggered = true;
            agent.AddReward(successReward);
            Debug.Log($"Goal!!! +10점 parts: {other.name}");
            StartCoroutine(AnimateAndEndEpisode(agent));
        }
    }

    IEnumerator AnimateAndEndEpisode(NupJukEJumpAgent agent)
    {
        _isTriggered = true;

        float elapsed = 0f;
        Vector3 startPos = transform.localPosition;
        Vector3 targetPos = startPos + new Vector3(0, -pressedDepth, 0);

        _meshRenderer.material = new Material(matGoalActive);
        _meshRenderer.material.EnableKeyword("_EMISSION");

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            transform.localPosition = Vector3.Lerp(startPos, targetPos, t * (2 - t));
            yield return null;
        }

        agent.EndEpisode();
    }
}