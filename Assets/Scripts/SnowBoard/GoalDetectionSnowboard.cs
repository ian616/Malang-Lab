using UnityEngine;
using Unity.MLAgents;
using System.Collections;

public class GoalDetectionSnowboard : MonoBehaviour
{
    [Header("Materials")]
    public Material matGoalInactive;
    public Material matGoalActive;

    [Header("Reward Settings")]
    public float successReward = 10.0f;

    [Header("Random Spawn Settings")]
    public float rangeX = 5.0f;
    public float rangeZ = 5.0f;
    public LayerMask groundLayer; 

    private MeshRenderer _meshRenderer;
    private Vector3 _centerPos; 
    private bool _isTriggered = false;

    void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        _centerPos = transform.position; 
    }

    public void ResetGoal()
    {
        _isTriggered = false;

        float randomX = Random.Range(-rangeX, rangeX);
        float randomZ = Random.Range(-rangeZ, rangeZ);
        
        Vector3 spawnOrigin = _centerPos;
        spawnOrigin.x += randomX;
        spawnOrigin.z += randomZ;
        spawnOrigin.y += 10f; // 레이 높이 살짝 조정

        if (Physics.Raycast(spawnOrigin, Vector3.down, out RaycastHit hit, 50f, groundLayer))
        {
            transform.position = hit.point + (hit.normal * 0.01f);
            transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        }
        else
        {
            // 레이 실패 시 기본 위치로
            Vector3 fallbackPos = spawnOrigin;
            fallbackPos.y = _centerPos.y;
            transform.position = fallbackPos;
        }

        if (matGoalInactive != null)
        {
            _meshRenderer.material = new Material(matGoalInactive);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        var agent = collision.collider.GetComponentInParent<NupJukSnowBoardAgent>();
        if (_isTriggered || agent == null) return;

        if (collision.collider.name.ToLower().Contains("board"))
        {
            _isTriggered = true;
            agent.AddReward(successReward);
            Debug.Log($"[GOAL] 성공! +{successReward}점");

            // 애니메이션 없이 머티리얼만 바꾸고 종료 로직 실행
            StartCoroutine(ChangeMatAndEnd(agent));
        }
    }

    IEnumerator ChangeMatAndEnd(NupJukSnowBoardAgent agent)
    {
        // 1. 머티리얼 즉시 교체
        if (matGoalActive != null)
        {
            _meshRenderer.material = new Material(matGoalActive);
            _meshRenderer.material.EnableKeyword("_EMISSION");
        }
        yield return new WaitForSeconds(0.5f);

        // 3. 에피소드 종료
        agent.EndEpisode();
    }
}