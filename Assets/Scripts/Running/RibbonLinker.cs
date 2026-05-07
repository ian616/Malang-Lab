using UnityEngine;
using System.Collections.Generic;

public class RibbonLinker : MonoBehaviour
{
    public Transform anchorL; 
    public Transform anchorR;
    public Transform[] agents; 

    [Header("시각적 설정")]
    [Range(0.1f, 1.5f)]
    public float lengthMultiplier = 1.0f;

    [Header("충돌 설정")]
    public float collisionRadius = 0.5f; 
    public Vector3 offset; 

    [Header("지지대 물리 반응")]
    public float pullForce = 50f;      // 안쪽으로 당기는 힘 (장력)
    public float forwardForce = 30f;   // 앞으로 밀리는 힘 (충격)
    public float detectionRange = 1.2f; // 에이전트가 이 거리 안에 오면 지지대가 반응함

    private float _nativeMeshLength; 
    private List<GameObject> _ghosts = new List<GameObject>();
    private Rigidbody _rbL, _rbR;

    void Start()
    {
        if (anchorL == null || anchorR == null) return;

        // 지지대 Rigidbody 캐싱 (부모나 자신에게서 찾음)
        _rbL = anchorL.GetComponentInParent<Rigidbody>();
        _rbR = anchorR.GetComponentInParent<Rigidbody>();

        Mesh mesh = null;
        if (GetComponent<MeshFilter>()) mesh = GetComponent<MeshFilter>().sharedMesh;
        else if (GetComponent<SkinnedMeshRenderer>()) mesh = GetComponent<SkinnedMeshRenderer>().sharedMesh;
        if (mesh != null) _nativeMeshLength = mesh.bounds.size.z;
        if (_nativeMeshLength <= 0) _nativeMeshLength = 1.0f;

        Cloth cloth = GetComponent<Cloth>();
        if (cloth == null) return;

        List<ClothSphereColliderPair> colliderPairs = new List<ClothSphereColliderPair>();

        foreach (Transform agent in agents)
        {
            if (agent == null) continue;

            GameObject ghost = new GameObject($"Ghost_{agent.name}");
            ghost.isStatic = false; // [해결] 스테틱 강제 해제

            SphereCollider sc = ghost.AddComponent<SphereCollider>();
            sc.radius = collisionRadius;
            sc.isTrigger = true;

            _ghosts.Add(ghost);
            colliderPairs.Add(new ClothSphereColliderPair(sc));
        }

        cloth.sphereColliders = colliderPairs.ToArray();
    }

    void LateUpdate() 
    {
        if (anchorL == null || anchorR == null) return;

        // 1. 리본 위치/회전/스케일 (자석처럼 붙이기)
        Vector3 centerPos = (anchorL.position + anchorR.position) / 2f;
        transform.position = centerPos;
        
        Vector3 dir = anchorR.position - anchorL.position;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);

        float distance = Vector3.Distance(anchorL.position, anchorR.position);
        Vector3 currentScale = transform.localScale;
        currentScale.z = (distance / _nativeMeshLength) * lengthMultiplier;
        transform.localScale = currentScale;

        // 2. 유령 콜라이더 추적 및 지지대 물리 전달
        for (int i = 0; i < agents.Length; i++)
        {
            if (i < _ghosts.Count && agents[i] != null)
            {
                // 유령 콜라이더 위치 업데이트 (회전 고려)
                Vector3 targetPos = agents[i].position + (agents[i].rotation * offset);
                _ghosts[i].transform.position = targetPos;

                // --- 지지대 물리 로직 추가 ---
                float distToRibbon = Vector3.Distance(targetPos, centerPos);
                
                // 에이전트가 리본 지점을 통과하려고 하면 지지대를 당김
                if (distToRibbon < detectionRange)
                {
                    ApplyForceToPillars(agents[i]);
                }
            }
        }
    }

    private void ApplyForceToPillars(Transform pusher)
    {
        if (_rbL == null || _rbR == null) return;

        // 1. 장력: 두 지지대를 서로의 방향(안쪽)으로 당김
        Vector3 toRight = (anchorR.position - anchorL.position).normalized;
        Vector3 toLeft = -toRight;

        _rbL.AddForce(toRight * pullForce, ForceMode.Acceleration);
        _rbR.AddForce(toLeft * pullForce, ForceMode.Acceleration);

        // 2. 충격: 에이전트가 나가는 방향으로 지지대를 밀어버림
        Vector3 pushDir = pusher.forward;
        _rbL.AddForce(pushDir * forwardForce, ForceMode.Acceleration);
        _rbR.AddForce(pushDir * forwardForce, ForceMode.Acceleration);
    }
}