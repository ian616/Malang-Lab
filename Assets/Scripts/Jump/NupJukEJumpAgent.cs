using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class NupJukEJumpAgent : Agent
{
    public GoalDetectionJump target;
    public ConfigurableJoint hipL, hipR, spine2;
    public Rigidbody spine1Rb;
    public Collider headCol;
    public Transform middleGoal;

    public int maxSteps = 2000;
    public float angleLimitX = 40f;
    public float angleLimitY = 60f;
    public float angleSmooth = 0.15f;

    float[] curAngles = new float[5];
    public bool reachedMiddleGoal;
    
    private bool isHeadTouching;

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

        isHeadTouching = false;

        if (target != null) target.ResetGoal();

        if (middleGoal && target)
        {
            middleGoal.gameObject.SetActive(true);
            reachedMiddleGoal = false;

            Vector3 targetPos = target.transform.localPosition;
            Vector3 midPos = Vector3.Lerp(Vector3.zero, targetPos, 0.65f);
            midPos.y = 1.7f;
            middleGoal.localPosition = midPos;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 tempTarget = target.transform.position;
        tempTarget.y = 0.5f;
        Vector3 toTarget = tempTarget - spine1Rb.position;

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

        Vector3 dir = target.transform.position - spine1Rb.position;
        dir.y = 0;
        Vector3 toTargetXZ = dir.normalized;

        float velTowards = Vector3.Dot(spine1Rb.linearVelocity, toTargetXZ);
        float upDot = Vector3.Dot(spine1Rb.transform.up, Vector3.up);

        if (upDot > 0.8f) AddReward(0.001f);
        if (velTowards > 0.05f) AddReward(velTowards * Mathf.Clamp01(upDot) * 0.05f);
        if (upDot > 0.9f) AddReward(0.002f);

        if (upDot < 0.5f || isHeadTouching)
        {
            SetReward(-5.0f);
            EndEpisode();
            return;
        }

        if (StepCount >= maxSteps)
        {
            EndEpisode();
            return;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ground"))
        {
            foreach (var contact in collision.contacts)
            {
                if (contact.thisCollider == headCol)
                {
                    isHeadTouching = true;
                    break;
                }
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Ground")) isHeadTouching = false;
    }

    void SetJointRotation(ConfigurableJoint j, float x, float y, float z)
        => j.targetRotation = Quaternion.Euler(x, y, z);
}