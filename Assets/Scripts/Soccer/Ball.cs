using UnityEngine;

public class Ball : MonoBehaviour
{
    [Header("Shoot Settings")]
    public float shootForce = 2f;
    public float upwardFactor = 0.5f;

    [Header("Side & Back Spawn Settings")]
    public float minRadius = 8f;
    public float maxRadius = 12f;
    public float yOffset = 0.29f;

    [Header("Spawn Angle Settings")]
    [Range(0f, 360f)] public float minAngle = 90f;
    [Range(0f, 360f)] public float maxAngle = 270f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
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

        float randomAngle = Random.Range(minAngle, maxAngle);
        float radius = Random.Range(minRadius, maxRadius);
        float rad = randomAngle * Mathf.Deg2Rad;

        float x = Mathf.Sin(rad) * radius;
        float z = Mathf.Cos(rad) * radius;

        transform.localPosition = new Vector3(x, yOffset, z);
    }

    private void OnCollisionEnter(Collision collision)
    {
        string partName = collision.gameObject.name.ToLower();
        var agent = collision.gameObject.GetComponentInParent<NupJukESoccerAgent>();

        if (agent != null)
        {
            if (partName.Contains("foot") || partName.Contains("spine") || partName.Contains("hip") || partName.Contains("thigh") || partName.Contains("calf"))
            {
                Debug.Log($"<color=lime>[Success]</color> {partName}으로 터치! +10점");
                agent.AddReward(10.0f);
            }
            agent.EndEpisode();
        }
    }
}