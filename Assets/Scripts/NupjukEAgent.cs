using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class NupjukEAgent : Agent
{
    // ========= References (Scene Objects) =========
    public Transform target;

    // ========= References (Joints / Rigidbodies) =========
    public ConfigurableJoint hipL, hipR, spine2;
    public Rigidbody spine1Rb;

    // ========= References (Colliders) =========
    public Collider footColL, footColR;
    public Collider headCol;

    // ========= Episode / Termination =========
    public int maxSteps = 2000;
    int headTouchFrames;

    // ========= Ground Check =========
    public float groundCheckDist = 0.08f;
    bool wasL, wasR;

    // ========= Joint Control (Actions -> TargetRotation) =========
    public float angleLimitX = 40f;
    public float angleLimitY = 60f;
    public float angleSmooth = 0.15f;
    float[] targetAngles = new float[5];
    float[] curAngles = new float[5];

    // ========= Reward Weights =========
    public float uprightK = 0.002f;
    public float uprightMin = 0.7f;

    // ========= Step / Gait Reward =========
    int lastStepFoot = 0;          // 0 = none, 1 = left, 2 = right
    float stepReward = 0.02f;
    int stepCount;
    public float maxStepMul = 5f;

    // ========= Distance Progress Reward =========
    float lastDist;

    struct RBInit
    {
        public Rigidbody rb;
        public Vector3 pos;
        public Quaternion rot;
    }
    List<RBInit> rbInits = new List<RBInit>();

    public override void Initialize()
    {
        rbInits.Clear();
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
            rbInits.Add(new RBInit { rb = rb, pos = rb.position, rot = rb.rotation });
    }

    public override void OnEpisodeBegin()
    {
        headTouchFrames = 0;
        stepCount = 0;
        lastStepFoot = 0;

        foreach (var s in rbInits)
        {
            s.rb.position = s.pos;
            s.rb.rotation = s.rot;
            s.rb.linearVelocity = Vector3.zero;
            s.rb.angularVelocity = Vector3.zero;
            s.rb.Sleep();
            s.rb.WakeUp();
        }

        for (int i = 0; i < 5; i++) { targetAngles[i] = 0f; curAngles[i] = 0f; }

        if (target)
            target.localPosition = new Vector3(Random.Range(-2f, 2f), 0.5f, Random.Range(7f, 12f));

        lastDist = target ? Vector3.Distance(spine1Rb.position, target.position) : 0f;
        wasL = IsGrounded(footColL);
        wasR = IsGrounded(footColR);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (target)
        {
            Vector3 toTarget = target.position - spine1Rb.position;
            sensor.AddObservation(transform.InverseTransformDirection(toTarget.normalized));
            sensor.AddObservation(toTarget.magnitude);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
        }

        sensor.AddObservation(transform.InverseTransformDirection(spine1Rb.linearVelocity));
        sensor.AddObservation(transform.InverseTransformDirection(spine1Rb.angularVelocity));

        sensor.AddObservation(IsGrounded(footColL) ? 1f : 0f);
        sensor.AddObservation(IsGrounded(footColR) ? 1f : 0f);

        for (int i = 0; i < 5; i++) sensor.AddObservation(curAngles[i]);
        sensor.AddObservation(Vector3.Dot(spine1Rb.transform.up, Vector3.up));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // ========= Reward (Alive) =========
        AddReward(0.0001f);

        // ========= Actions -> Smoothed Targets =========
        var a = actions.ContinuousActions;

        float actSq = 0f;
        for (int i = 0; i < 5; i++)
        {
            float v = Mathf.Clamp(a[i], -1f, 1f);
            actSq += v * v;

            targetAngles[i] = v;
            curAngles[i] = Mathf.Lerp(curAngles[i], targetAngles[i], angleSmooth);
        }

        // ========= Apply Joint Rotations =========
        float hipLX = curAngles[0] * angleLimitX;
        float hipLY = curAngles[1] * angleLimitY;
        float hipRX = curAngles[2] * angleLimitX;
        float hipRY = curAngles[3] * angleLimitY;
        float spineX = curAngles[4] * angleLimitX;

        SetJointRotation(hipL, hipLX, hipLY, 0f);
        SetJointRotation(hipR, hipRX, hipRY, 0f);
        SetJointRotation(spine2, spineX, 0f, 0f);

        // ========= Reward (Stationary Penalty) =========
        Vector3 vel = spine1Rb.linearVelocity;
        float up = Mathf.Clamp01(Vector3.Dot(spine1Rb.transform.up, Vector3.up));
        float planarSpeed = new Vector2(vel.x, vel.z).magnitude;

        if (planarSpeed < 0.1f && up > uprightMin)
        {
            AddReward(-0.002f);
        }

        // ========= Reward (Distance Progress) =========
        if (target)
        {
            float d = Vector3.Distance(spine1Rb.position, target.position);
            float delta = lastDist - d;
            AddReward(delta * 0.25f);
            lastDist = d;
        }

        // ========= Ground States =========
        bool gL = IsGrounded(footColL);
        bool gR = IsGrounded(footColR);

        // ========= Step Alternation (Lift -> Land) =========
        if (!gL && wasL) lastStepFoot = 1; // left lifted
        if (!gR && wasR) lastStepFoot = 2; // right lifted

        if (gL && !wasL && lastStepFoot == 2)
        {
            stepCount++;
            float mul = Mathf.Min(stepCount, maxStepMul);
            AddReward(stepReward * mul);
            lastStepFoot = 0;
        }
        else if (gR && !wasR && lastStepFoot == 1)
        {
            stepCount++;
            float mul = Mathf.Min(stepCount, maxStepMul);
            AddReward(stepReward * mul);
            lastStepFoot = 0;
        }

        wasL = gL;
        wasR = gR;

        // ========= Reward (Upright / Balance) =========
        if (up > uprightMin)
            AddReward((up - uprightMin) * uprightK);

        // ========= Reward (Forward Velocity When 1 Foot Airborne) =========
        int airborne = (gL ? 0 : 1) + (gR ? 0 : 1);
        if (airborne == 1 && target && up > uprightMin)
        {
            Vector3 to = (target.position - spine1Rb.position).normalized;
            float v = Vector3.Dot(spine1Rb.linearVelocity, to);
            v = Mathf.Clamp(v, 0f, 2f);
            AddReward(v * 0.005f);
        }

        // ========= Termination (Head Touch Ground) =========
        headTouchFrames = HeadTouchingGround() ? (headTouchFrames + 1) : 0;

        if (headTouchFrames >= 10)
        {
            SetReward(-1f);
            EndEpisode();
            return;
        }

        // ========= Termination (Max Steps) =========
        if (maxSteps > 0 && StepCount >= maxSteps)
        {
            EndEpisode();
            return;
        }
    }

    void OnCollisionEnter(Collision c)
    {
        if (c.gameObject.CompareTag("Goal")) { SetReward(10f); EndEpisode(); }
        else if (c.gameObject.CompareTag("Wall")) { SetReward(-1f); EndEpisode(); }
    }

    void SetJointRotation(ConfigurableJoint j, float xDeg, float yDeg, float zDeg)
        => j.targetRotation = Quaternion.Euler(xDeg, yDeg, zDeg);

    bool IsGrounded(Collider col)
    {
        if (!col) return false;

        RaycastHit hit;
        Vector3 o = col.bounds.center;
        o.y = col.bounds.min.y + 0.01f;

        if (Physics.Raycast(o, Vector3.down, out hit, groundCheckDist, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.CompareTag("Ground");

        return false;
    }

    bool HeadTouchingGround()
    {
        if (!headCol) return false;

        Collider[] hits = Physics.OverlapBox(
            headCol.bounds.center,
            headCol.bounds.extents * 0.95f,
            headCol.transform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == headCol) continue;
            if (hits[i].CompareTag("Ground")) return true;
        }
        return false;
    }
}