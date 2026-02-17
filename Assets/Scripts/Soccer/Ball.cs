using UnityEngine;

public class Ball : MonoBehaviour
{
    [Header("Shoot Settings")]
    public float shootForce = 2f;
    public float upwardFactor = 0.5f;

    private Rigidbody rb;
    private Vector3 initialPos;

    // [추가] 이번 에피소드에서 보상을 이미 줬는지 확인하는 플래그
    private bool isTouchedThisEpisode = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        initialPos = transform.localPosition;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L)) ResetBall();
        if (Input.GetKeyDown(KeyCode.Space)) Shoot();
    }

    private void Shoot()
    {
        Vector3 direction = (Vector3.back + Vector3.up * upwardFactor).normalized;
        rb.AddForce(direction * shootForce, ForceMode.Impulse);
    }

    public void ResetBall()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.localPosition = initialPos;

        // [중요] 리셋 시 보상 플래그도 초기화
        isTouchedThisEpisode = false;

        Debug.Log("<color=cyan>[Ball]</color> 위치 및 보상 락 리셋 완료");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isTouchedThisEpisode) return;

        string partName = collision.gameObject.name.ToLower();
        var agent = collision.gameObject.GetComponentInParent<NupJukESoccerAgent>();

        if (agent != null)
        {
            if (partName.Contains("foot") || partName.Contains("spine1") ||
                partName.Contains("hip") || partName.Contains("thigh") || partName.Contains("calf"))
            {
                agent.AddReward(5.0f);

                agent.hasTouchedBall = true;

                FollowingCam camScript = Camera.main.GetComponent<FollowingCam>();
                if (camScript != null)
                {
                    camScript.StartImpactEffect();
                }

                isTouchedThisEpisode = true;

                Debug.Log($"<color=lime>[Touch]</color> {partName} 첫 터치! 보상 지급 및 락 설정");
            }
        }
    }
}