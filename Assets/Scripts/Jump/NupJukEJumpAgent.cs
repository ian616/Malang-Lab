using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class NupJukEJumpAgent : Agent
{
    public Transform target;
    public ConfigurableJoint hipL, hipR, spine2;
    public Rigidbody spine1Rb;
    public Collider headCol;

    public int maxSteps = 3000;
    public float angleLimitX = 40f;
    public float angleLimitY = 60f;
    public float angleSmooth = 0.8f;

    float[] curAngles = new float[5];

    struct RBInit { public Rigidbody rb; public Vector3 pos; public Quaternion rot; }
    List<RBInit> rbInits = new List<RBInit>();

    public override void Initialize()
    {
        rbInits.Clear();
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
            rbInits.Add(new RBInit { rb = rb, pos = rb.position, rot = rb.rotation });
    }

    public override void OnEpisodeBegin()
    {
        foreach (var s in rbInits)
        {
            s.rb.position = s.pos;
            s.rb.rotation = s.rot;
            s.rb.linearVelocity = Vector3.zero;
            s.rb.angularVelocity = Vector3.zero;
            s.rb.Sleep();
            s.rb.WakeUp();
        }

        for (int i = 0; i < 5; i++) curAngles[i] = 0f;

        if (target)
            target.localPosition = new Vector3(Random.Range(-2f, 2f), 0.5f, Random.Range(8f, 10f));
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toTarget = target.position - spine1Rb.position;

        sensor.AddObservation(transform.InverseTransformDirection(toTarget.normalized));
        sensor.AddObservation(toTarget.magnitude);
        sensor.AddObservation(transform.InverseTransformDirection(spine1Rb.linearVelocity));
        sensor.AddObservation(transform.InverseTransformDirection(spine1Rb.angularVelocity));
        sensor.AddObservation(Vector3.Dot(spine1Rb.transform.up, Vector3.up));

        for (int i = 0; i < 5; i++) sensor.AddObservation(curAngles[i]);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;

        for (int i = 0; i < 5; i++)
        {
            float targetVal = Mathf.Clamp(a[i], -1f, 1f);
            curAngles[i] = Mathf.Lerp(curAngles[i], targetVal, angleSmooth);
        }

        SetJointRotation(hipL, curAngles[0] * angleLimitX, curAngles[1] * angleLimitY, 0f);
        SetJointRotation(hipR, curAngles[2] * angleLimitX, curAngles[3] * angleLimitY, 0f);
        SetJointRotation(spine2, curAngles[4] * angleLimitX, 0f, 0f);

        Vector3 toTarget = (target.position - spine1Rb.position).normalized;
        float velTowards = Vector3.Dot(spine1Rb.linearVelocity, toTarget);
        float upDot = Vector3.Dot(spine1Rb.transform.up, Vector3.up);

        if (upDot > 0.8f)
            AddReward(0.001f);

        if (velTowards > 0.05f)
            AddReward(velTowards * Mathf.Clamp01(upDot) * 0.05f);

        if (upDot > 0.9f)
            AddReward(0.002f);

        // if (upDot < 0.3f || HeadTouchingGround() || spine1Rb.position.y < 0.1f) 
        // {
        //     Debug.Log("Hmm");
        //     SetReward(-1.0f);
        //     EndEpisode();
        //     return;
        // }

        if (StepCount >= maxSteps)
        {
            // 보상을 주지 않거나, 상황에 따라 약간의 페널티/보상을 줄 수 있습니다.
            // 여기서는 단순히 다음 기회를 위해 에피소드만 종료합니다.
            EndEpisode();
        }
    }

    void SetJointRotation(ConfigurableJoint j, float x, float y, float z)
        => j.targetRotation = Quaternion.Euler(x, y, z);

    bool HeadTouchingGround()
    {
        if (!headCol) return false;
        Collider[] hits = Physics.OverlapBox(headCol.bounds.center, headCol.bounds.extents, headCol.transform.rotation);
        foreach (var h in hits)
        {
            if (h != headCol && h.CompareTag("Ground")) return true;
        }
        return false;
    }

    public void ResetByButton()
    {
        Debug.Log("Reset Button Clicked!");
        EndEpisode();
    }
}