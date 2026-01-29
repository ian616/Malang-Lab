using UnityEngine;

public class BallController : MonoBehaviour
{
    [Header("슛 설정")]
    public float shootPower = 0.5f;
    // -z 방향(뒤쪽)으로 날아가도록 수정
    public Vector3 shootDirection = new Vector3(0, 0.3f, -1f); 

    private Rigidbody rb;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // 리셋을 위해 처음 위치와 회전값 저장
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    void Update()
    {
        // L 키: 슛 (현재 정한 -z 방향)
        if (Input.GetKeyDown(KeyCode.K))
        {
            Shoot();
        }

        // Space 키: 공을 처음 위치로 리셋
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ResetBall();
        }
    }

    void Shoot()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            // -z 방향으로 힘 가하기
            rb.AddForce(shootDirection.normalized * shootPower, ForceMode.Impulse);
        }
    }

    void ResetBall()
    {
        // 위치와 회전 복구
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        // 물리력 초기화 (움직이던 힘 제거)
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}