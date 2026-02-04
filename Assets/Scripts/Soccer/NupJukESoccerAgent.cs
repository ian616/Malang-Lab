using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class NupJukESoccerAgent : Agent
{
    [Header("Target & Goal")]
    public GameObject target;
    public Transform goalpost;

    [Header("Body Joints")]
    public ConfigurableJoint hipL, calfL, hipR, calfR, spine2, shoulderL, shoulderR, handL, handR;
    public Rigidbody spine1Rb, hipLRb, calfLRb, hipRRb, calfRRb, spine2Rb, shL_Rb, shR_Rb, handLRb, handRRb;
    public Collider headCol;
    public Transform footL, footR;

    [Header("Settings")]
    public float angleSmooth = 0.2f;

    private float[] curActions = new float[12];
    private float m_DistanceAtLastStep;

    private float m_RewardDist, m_RewardUpright, m_RewardFace, m_RewardVel, m_RewardLowVel, m_RewardTotal, m_RewardBallToGoal;
    private float m_RewardSpinePenalty, m_RewardBounce;

    private float m_DispDist, m_DispUpright, m_DispFace, m_DispVelRew, m_DispLowVel, m_DispTotal, m_DispVel, m_DispBallVel, m_DispSpine, m_DispBounce, m_DispBallGoalRew;
    private float m_GuiTimer;
    private const float GUI_UPDATE_INTERVAL = 0.1f;

    private bool isHeadTouching;
    private bool m_HasTouchedBall;

    struct RBInit { public Rigidbody rb; public Vector3 pos; public Quaternion rot; }
    List<RBInit> rbInits = new List<RBInit>();
    List<Rigidbody> bodyParts = new List<Rigidbody>();
    private Transform targetTf;
    private Rigidbody ballRb;
    private Ball_Shoot ballScript;

    public override void Initialize()
    {
        rbInits.Clear();
        bodyParts.Clear();
        if (target != null)
        {
            targetTf = target.transform;
            ballScript = target.GetComponent<Ball_Shoot>();
            ballRb = target.GetComponent<Rigidbody>();
        }
        var allRbs = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in allRbs)
        {
            rbInits.Add(new RBInit { rb = rb, pos = rb.position, rot = rb.rotation });
            if (rb != spine1Rb) bodyParts.Add(rb);
        }
    }

    void Start()
    {
        var colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            for (int j = i + 1; j < colliders.Length; j++)
            {
                Physics.IgnoreCollision(colliders[i], colliders[j], true);
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        foreach (var s in rbInits)
        {
            s.rb.position = s.pos; s.rb.rotation = s.rot;
            s.rb.linearVelocity = Vector3.zero; s.rb.angularVelocity = Vector3.zero;
            s.rb.Sleep(); s.rb.WakeUp();
        }
        if (ballScript != null) ballScript.ResetBall();
        for (int i = 0; i < 12; i++) curActions[i] = 0f;
        isHeadTouching = false;
        m_HasTouchedBall = false;
        if (targetTf != null) m_DistanceAtLastStep = Vector3.Distance(spine1Rb.position, targetTf.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toTarget = targetTf.position - spine1Rb.position;
        toTarget.y = 0;
        sensor.AddObservation(transform.InverseTransformDirection(toTarget.normalized));
        sensor.AddObservation(toTarget.magnitude);
        sensor.AddObservation(transform.InverseTransformDirection(spine1Rb.linearVelocity));
        sensor.AddObservation(transform.InverseTransformDirection(spine1Rb.angularVelocity));
        sensor.AddObservation(Vector3.Dot(spine1Rb.transform.up, Vector3.up));
        foreach (float a in curActions) sensor.AddObservation(a);
        foreach (var rb in bodyParts)
        {
            sensor.AddObservation(transform.InverseTransformPoint(rb.position));
            sensor.AddObservation(rb.transform.localRotation);
            sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));
            sensor.AddObservation(transform.InverseTransformDirection(rb.angularVelocity));
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        for (int i = 0; i < 12; i++) curActions[i] = Mathf.Lerp(curActions[i], Mathf.Clamp(a[i], -1f, 1f), angleSmooth);

        SetJointRotation(hipL, Map(curActions[0], -20f, 60f), Map(curActions[1], -20f, 20f), 0);
        SetJointRotation(hipR, Map(curActions[2], -20f, 60f), Map(curActions[3], -20f, 20f), 0);
        SetJointRotation(spine2, Map(curActions[4], -20f, 20f), Map(curActions[5], -10f, 10f), 0);
        SetJointRotation(calfL, Map(curActions[6], -80f, 0f), 0, 0);
        SetJointRotation(calfR, Map(curActions[7], -80f, 0f), 0, 0);
        SetJointRotation(shoulderL, Map(curActions[8], -10f, 30f), 0, 0);
        SetJointRotation(shoulderR, Map(curActions[9], -10f, 30f), 0, 0);
        SetJointRotation(handL, Map(curActions[10], 0f, 90f), 0, 0);
        SetJointRotation(handR, Map(curActions[11], 0f, 90f), 0, 0);

        float currentDistance = Vector3.Distance(spine1Rb.position, targetTf.position);
        float upDot = Vector3.Dot(spine1Rb.transform.up, Vector3.up);
        float agentVelMag = spine1Rb.linearVelocity.magnitude;
        Vector3 toTargetDir = (targetTf.position - spine1Rb.position).normalized;
        toTargetDir.y = 0;

        m_RewardDist = (m_DistanceAtLastStep - currentDistance) * 0.5f;
        m_DistanceAtLastStep = currentDistance;

        float velDot = Vector3.Dot(spine1Rb.linearVelocity, toTargetDir);
        m_RewardVel = Mathf.Max(0, velDot) * upDot * 0.005f;
        m_RewardLowVel = (agentVelMag < 0.5f) ? -0.01f : 0f;
        m_RewardFace = (Vector3.Dot(spine1Rb.transform.forward, toTargetDir) - 1.0f) * 0.04f;
        m_RewardSpinePenalty = -Mathf.Abs(curActions[4]) * 0.02f;
        m_RewardBounce = Mathf.Abs(spine1Rb.linearVelocity.y) * 0.003f;
        m_RewardUpright = (upDot < 0.8f) ? -0.005f : (upDot - 0.8f) * 0.01f;

        m_RewardBallToGoal = 0f;
        if (ballRb != null && goalpost != null)
        {
            Vector3 dirToGoal = (goalpost.position - targetTf.position).normalized;

            float speedTowardsGoal = Vector3.Dot(ballRb.linearVelocity, dirToGoal);

            if (speedTowardsGoal > 0)
            {
                m_RewardBallToGoal = speedTowardsGoal * 0.05f;
            }
        }

        m_RewardTotal = m_RewardDist + m_RewardUpright + m_RewardFace +
                         m_RewardSpinePenalty + m_RewardBounce + m_RewardVel + m_RewardLowVel + m_RewardBallToGoal;

        AddReward(m_RewardTotal);

        // if (upDot < 0.65f || isHeadTouching) { SetReward(-5.0f); EndEpisode(); }
    }

    void Update()
    {
        m_GuiTimer += Time.deltaTime;
        if (m_GuiTimer >= GUI_UPDATE_INTERVAL)
        {
            m_DispDist = m_RewardDist;
            m_DispUpright = m_RewardUpright;
            m_DispFace = m_RewardFace;
            m_DispVelRew = m_RewardVel;
            m_DispLowVel = m_RewardLowVel;
            m_DispSpine = m_RewardSpinePenalty;
            m_DispBounce = m_RewardBounce;
            m_DispBallGoalRew = m_RewardBallToGoal;
            m_DispTotal = GetCumulativeReward();
            m_DispVel = spine1Rb.linearVelocity.magnitude;
            m_DispBallVel = ballRb != null ? ballRb.linearVelocity.magnitude : 0f;
            m_GuiTimer = 0f;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            EndEpisode();
        }
    }

    private void OnGUI()
    {
        if (Camera.main == null || spine1Rb == null || targetTf == null) return;
        if (Vector3.Distance(Camera.main.transform.position, spine1Rb.position) > 25f) return;

        GUIStyle style = new GUIStyle { fontSize = 26, richText = true };
        style.normal.textColor = Color.white;
        GUI.backgroundColor = new Color(0, 0, 0, 0.85f);
        Rect rect = new Rect(30, 30, 500, 780);
        GUI.Box(rect, "");

        string statusColor = m_HasTouchedBall ? "#00FF00" : "#FFFFFF";
        string ballRewColor = m_DispBallGoalRew > 0 ? "#FFD700" : "#FFFFFF";

        string debugText = $"<b><size=30>[ SOCCER AGENT MONITOR ]</size></b>\n" +
                           $"----------------------------------\n" +
                           $"Ball Touched : <color={statusColor}>{m_HasTouchedBall}</color>\n" +
                           $"Agent Speed : {m_DispVel:F2} m/s\n" +
                           $"Ball Speed  : {m_DispBallVel:F2} m/s\n" +
                           $"----------------------------------\n" +
                           $"Distance Rew : {m_DispDist:F4}\n" +
                           $"Face Rew     : {m_DispFace:F4}\n" +
                           $"Bounce Rew   : <color=#00FFFF>{m_DispBounce:F4}</color>\n" +
                           $"Ball Goal Rew: <color={ballRewColor}>{m_DispBallGoalRew:F4}</color>\n" +
                           $"Spine Penalty: {m_DispSpine:F4}\n" +
                           $"Upright Rew  : {m_DispUpright:F4}\n" +
                           $"----------------------------------\n" +
                           $"<size=32><b>TOTAL REW : {m_DispTotal:F2}</b></size>";

        GUI.Label(new Rect(rect.x + 20, rect.y + 15, rect.width - 40, rect.height - 30), debugText, style);
    }

    float Map(float val, float min, float max) => val >= 0 ? val * max : val * Mathf.Abs(min);
    void SetJointRotation(ConfigurableJoint j, float x, float y, float z) { if (j != null) j.targetRotation = Quaternion.Euler(x, y, z); }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ground"))
        {
            foreach (var contact in collision.contacts)
                if (contact.thisCollider == headCol) isHeadTouching = true;
        }
        if (collision.collider.CompareTag("Ball")) m_HasTouchedBall = true;
    }

    private void OnCollisionExit(Collision collision) { if (collision.collider.CompareTag("Ground")) isHeadTouching = false; }
}