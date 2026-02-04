using UnityEngine;

public class Ball_Shoot : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 initialPos;
    private bool rewarded = false;

    [Header("Test Shoot Settings")]
    public float shootForce = 0.2f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        initialPos = transform.position;
    }

    void Update()
    {
        // 스페이스바를 누르면 -Z 방향으로 공을 쏨 (테스트용)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TestShootDirectional(Vector3.back); // Vector3.back == (0, 0, -1)
        }
    }

    public void ResetBall()
    {
        transform.position = initialPos;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rewarded = false;
    }

    private void TestShootDirectional(Vector3 dir)
    {
        rb.linearVelocity = Vector3.zero; 
        rb.AddForce(dir * shootForce, ForceMode.Impulse);
        var agent = FindObjectOfType<NupJukESoccerAgent>();
        if (agent != null)
        {
            Debug.Log("<color=orange>[Test Shoot]</color> Shooting towards -Z direction!");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        string name = collision.gameObject.name.ToLower();
        float impulse = collision.impulse.magnitude;

        bool isLeg = name.Contains("foot") || name.Contains("calf") || name.Contains("thigh");

        if (isLeg)
        {
            Debug.Log($"[Hit] Part: {collision.gameObject.name} | Impulse: {impulse:F2}");

            if (!rewarded)
            {
                var agent = collision.transform.root.GetComponentInChildren<NupJukESoccerAgent>();
                if (agent != null)
                {
                    agent.AddReward(impulse * 0.1f);
                    rewarded = true;
                    Debug.Log($"<color=green>[Reward]</color> {collision.gameObject.name} | {impulse * 0.1f:F4}");
                }
            }
        }
    }
}