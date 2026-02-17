using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public enum AgentRole { Attacker, Defender }

public class NupJukESoccerAgent : Agent
{
    #region Inspector Fields

    [Header("Role Settings")]
    public AgentRole role;
    public NupJukESoccerAgent opponent;

    [Header("Target & Goal")]
    public GameObject ball;
    public Transform goalpost;

    [Header("Body Joints")]
    public ConfigurableJoint hipL, calfL, hipR, calfR, spine2, shoulderL, shoulderR, handL, handR;
    public Rigidbody spine1Rb, hipLRb, calfLRb, hipRRb, calfRRb, spine2Rb, shL_Rb, shR_Rb, handLRb, handRRb;
    public Collider headCol;
    public Transform footL, footR;

    [Header("Settings")]
    public float angleSmooth = 0.2f;

    #endregion

    #region Private Fields

    // Actions
    private float[] curActions = new float[12];

    // Distance Tracking
    private float m_DistanceAtLastStep;

    // Rewards
    private float m_RewardDist;
    private float m_RewardUpright;
    private float m_RewardFace;
    private float m_RewardVel;
    private float m_RewardLowVel;
    private float m_RewardTotal;
    private float m_RewardBallToGoal;
    private float m_RewardSpinePenalty;
    private float m_RewardBounce;

    // Display Values
    private float m_DispDist;
    private float m_DispUpright;
    private float m_DispFace;
    private float m_DispVelRew;
    private float m_DispLowVel;
    private float m_DispTotal;
    private float m_DispVel;
    private float m_DispBallVel;
    private float m_DispSpine;
    private float m_DispBounce;
    private float m_DispBallGoalRew;
    private float m_GuiTimer;
    private const float GUI_UPDATE_INTERVAL = 0.1f;

    // State Tracking
    private bool isHeadTouching;
    public bool hasTouchedBall;  // Ball 스크립트에서 접근할 수 있도록 public으로 유지

    // Rigidbody Management
    private struct RBInit
    {
        public Rigidbody rb;
        public Vector3 pos;
        public Quaternion rot;
    }

    private List<RBInit> rbInits = new List<RBInit>();
    private List<Rigidbody> bodyParts = new List<Rigidbody>();
    private Rigidbody ballRb;
    private Ball ballScript;

    #endregion

    #region Unity ML-Agents Lifecycle

    public override void Initialize()
    {
        InitializeRigidbodies();
        InitializeBall();
    }

    public override void OnEpisodeBegin()
    {
        ResetRigidbodies();
        ResetBallState();
        ResetActions();
        ResetTouchState();
        UpdateInitialDistance();
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.005f;
        FollowingCam camScript = Camera.main.GetComponent<FollowingCam>();
        if (camScript != null) camScript.ResetCamera();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        AddBallObservations(sensor);
        AddAgentObservations(sensor);
        AddActionObservations(sensor);
        AddBodyPartObservations(sensor);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        UpdateActions(actions);
        ApplyJointRotations();
        CalculateRewards();
        AddReward(m_RewardTotal);
        CheckTerminationConditions();
    }

    #endregion

    #region Initialization Methods

    private void InitializeRigidbodies()
    {
        rbInits.Clear();
        bodyParts.Clear();

        var allRbs = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in allRbs)
        {
            rbInits.Add(new RBInit { rb = rb, pos = rb.position, rot = rb.rotation });
            if (rb != spine1Rb)
            {
                bodyParts.Add(rb);
            }
        }
    }

    private void InitializeBall()
    {
        if (ball != null)
        {
            ballScript = ball.GetComponent<Ball>();
            ballRb = ball.GetComponent<Rigidbody>();
        }
    }

    void Start()
    {
        SetupCollisionIgnoring();
    }

    private void SetupCollisionIgnoring()
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

    #endregion

    #region Reset Methods

    private void ResetRigidbodies()
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
    }

    private void ResetBallState()
    {
        if (ballScript != null)
        {
            ballScript.ResetBall();
        }
    }

    private void ResetActions()
    {
        for (int i = 0; i < 12; i++)
        {
            curActions[i] = 0f;
        }
    }

    private void ResetTouchState()
    {
        isHeadTouching = false;
        hasTouchedBall = false;
    }

    private void UpdateInitialDistance()
    {
        if (ball != null)
        {
            m_DistanceAtLastStep = Vector3.Distance(spine1Rb.position, ball.transform.position);
        }
    }

    public void ResetMatch()
    {
        if (ballScript != null)
        {
            ballScript.ResetBall();
        }

        if (opponent != null)
        {
            opponent.EndEpisode();
        }

        EndEpisode();
    }

    #endregion

    #region Observation Methods

    private void AddBallObservations(VectorSensor sensor)
    {
        Vector3 toBall = ball.transform.position - spine1Rb.position;
        toBall.y = 0;

        sensor.AddObservation(transform.InverseTransformDirection(toBall.normalized));
        sensor.AddObservation(toBall.magnitude);
    }

    private void AddAgentObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformDirection(spine1Rb.linearVelocity));
        sensor.AddObservation(transform.InverseTransformDirection(spine1Rb.angularVelocity));
        sensor.AddObservation(Vector3.Dot(spine1Rb.transform.up, Vector3.up));
    }

    private void AddActionObservations(VectorSensor sensor)
    {
        foreach (float a in curActions)
        {
            sensor.AddObservation(a);
        }
    }

    private void AddBodyPartObservations(VectorSensor sensor)
    {
        foreach (var rb in bodyParts)
        {
            sensor.AddObservation(transform.InverseTransformPoint(rb.position));
            sensor.AddObservation(rb.transform.localRotation);
            sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));
            sensor.AddObservation(transform.InverseTransformDirection(rb.angularVelocity));
        }
    }

    #endregion

    #region Action Methods

    private void UpdateActions(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        for (int i = 0; i < 12; i++)
        {
            curActions[i] = Mathf.Lerp(curActions[i], Mathf.Clamp(a[i], -1f, 1f), angleSmooth);
        }
    }

    private void ApplyJointRotations()
    {
        // Leg Joints
        SetJointRotation(hipL, Map(curActions[0], -20f, 60f), Map(curActions[1], -20f, 20f), 0);
        SetJointRotation(hipR, Map(curActions[2], -20f, 60f), Map(curActions[3], -20f, 20f), 0);
        SetJointRotation(calfL, Map(curActions[6], -80f, 0f), 0, 0);
        SetJointRotation(calfR, Map(curActions[7], -80f, 0f), 0, 0);

        // Spine Joint
        SetJointRotation(spine2, Map(curActions[4], -20f, 20f), Map(curActions[5], -10f, 10f), 0);

        // Arm Joints
        SetJointRotation(shoulderL, Map(curActions[8], -10f, 30f), 0, 0);
        SetJointRotation(shoulderR, Map(curActions[9], -10f, 30f), 0, 0);
        SetJointRotation(handL, Map(curActions[10], 0f, 90f), 0, 0);
        SetJointRotation(handR, Map(curActions[11], 0f, 90f), 0, 0);
    }

    #endregion

    #region Reward Calculation

    private void CalculateRewards()
    {
        CalculateDistanceReward();
        CalculateMovementRewards();
        CalculateOrientationRewards();
        CalculatePenalties();
        CalculateBallToGoalReward();

        m_RewardTotal = m_RewardDist + m_RewardUpright + m_RewardFace +
                        m_RewardSpinePenalty + m_RewardBounce + m_RewardVel +
                        m_RewardLowVel + m_RewardBallToGoal;
    }

    private void CalculateDistanceReward()
    {
        float currentDistance = Vector3.Distance(spine1Rb.position, ball.transform.position);
        m_RewardDist = (m_DistanceAtLastStep - currentDistance) * 0.5f;
        m_DistanceAtLastStep = currentDistance;
    }

    private void CalculateMovementRewards()
    {
        float agentVelMag = spine1Rb.linearVelocity.magnitude;

        Vector3 toBallDir = (ball.transform.position - spine1Rb.position).normalized;
        toBallDir.y = 0;

        float upDot = Vector3.Dot(spine1Rb.transform.up, Vector3.up);
        float velDot = Vector3.Dot(spine1Rb.linearVelocity, toBallDir);

        m_RewardVel = Mathf.Max(0, velDot) * upDot * 0.005f;
        m_RewardLowVel = (agentVelMag < 0.5f) ? -0.01f : 0f;
    }

    private void CalculateOrientationRewards()
    {
        Vector3 toBallDir = (ball.transform.position - spine1Rb.position).normalized;
        toBallDir.y = 0;

        float upDot = Vector3.Dot(spine1Rb.transform.up, Vector3.up);

        m_RewardFace = (Vector3.Dot(spine1Rb.transform.forward, toBallDir) - 1.0f) * 0.04f;
        m_RewardUpright = (upDot < 0.8f) ? -0.005f : (upDot - 0.8f) * 0.01f;
    }

    private void CalculatePenalties()
    {
        m_RewardSpinePenalty = -Mathf.Abs(curActions[4]) * 0.02f;
        m_RewardBounce = Mathf.Abs(spine1Rb.linearVelocity.y) * 0.003f;
    }

    private void CalculateBallToGoalReward()
    {
        m_RewardBallToGoal = 0f;

        if (!hasTouchedBall || ballRb == null || goalpost == null)
        {
            return;
        }

        Vector3 dirToGoal = (goalpost.position - ball.transform.position).normalized;
        float speedTowardsGoal = Vector3.Dot(ballRb.linearVelocity, dirToGoal);

        if (role == AgentRole.Attacker)
        {
            m_RewardBallToGoal = Mathf.Max(0, speedTowardsGoal) * 0.005f;
        }
        else
        {
            m_RewardBallToGoal = Mathf.Max(0, -speedTowardsGoal) * 0.005f;
        }
    }

    #endregion

    #region Termination Conditions

    private void CheckTerminationConditions()
    {
        float upDot = Vector3.Dot(spine1Rb.transform.up, Vector3.up);

        if (upDot < 0.55f || isHeadTouching)
        {
            bool isHeadingGoal = IsBallHeadingToGoal();

            if (role == AgentRole.Attacker && isHeadingGoal)
            {
                return;
            }

            SetReward(-5.0f);
            // ResetMatch();
        }
    }

    private bool IsBallHeadingToGoal()
    {
        if (ballRb == null || goalpost == null)
        {
            return false;
        }

        Vector3 dirToGoal = (goalpost.position - ball.transform.position).normalized;
        return Vector3.Dot(ballRb.linearVelocity, dirToGoal) > 0.05f;
    }

    #endregion

    #region Collision Handling

    private void OnCollisionEnter(Collision collision)
    {
        HandleGroundCollision(collision);
        HandleBallCollision(collision);
        HandleAgentCollision(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Ground"))
        {
            isHeadTouching = false;
        }
    }

    private void HandleGroundCollision(Collision collision)
    {
        if (collision.collider.CompareTag("Ground"))
        {
            foreach (var contact in collision.contacts)
            {
                if (contact.thisCollider == headCol)
                {
                    isHeadTouching = true;
                }
            }
        }
    }

    private void HandleBallCollision(Collision collision)
    {
        if (collision.collider.CompareTag("Ball"))
        {
            hasTouchedBall = true;
        }
    }

    private void HandleAgentCollision(Collision collision)
    {
        if (collision.collider.CompareTag("Agent"))
        {
            AddReward(-3.0f);
            Debug.Log("💥 에이전트 충돌 페널티 발생!");
        }
    }

    #endregion

    #region Update & GUI

    void Update()
    {
        UpdateDisplayValues();
        HandleDebugInput();
    }

    private void UpdateDisplayValues()
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
    }

    private void HandleDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            ResetMatch();
        }
    }

    // private void OnGUI()
    // {
    //     if (!ShouldDisplayGUI())
    //     {
    //         return;
    //     }

    //     GUIStyle style = CreateGUIStyle();
    //     Rect rect = CreateGUIRect();

    //     GUI.backgroundColor = new Color(0, 0, 0, 0.85f);
    //     GUI.Box(rect, "");

    //     string debugText = BuildDebugText();
    //     GUI.Label(new Rect(rect.x + 20, rect.y + 15, rect.width - 40, rect.height - 30), debugText, style);
    // }

    private bool ShouldDisplayGUI()
    {
        if (Camera.main == null || spine1Rb == null)
        {
            return false;
        }

        return Vector3.Distance(Camera.main.transform.position, spine1Rb.position) <= 25f;
    }

    private GUIStyle CreateGUIStyle()
    {
        GUIStyle style = new GUIStyle
        {
            fontSize = 24,
            richText = true
        };
        style.normal.textColor = Color.white;
        return style;
    }

    private Rect CreateGUIRect()
    {
        float xPosition = role == AgentRole.Attacker ? 30 : Screen.width - 530;
        return new Rect(xPosition, 30, 500, 750);
    }

    private string BuildDebugText()
    {
        string roleTag = role == AgentRole.Attacker ?
            "<color=#FF4444>ATTACKER</color>" :
            "<color=#4444FF>DEFENDER</color>";

        string touchColor = hasTouchedBall ? "#00FF00" : "#FFFFFF";

        return $"<b><size=28>[ {roleTag} MONITOR ]</size></b>\n" +
               $"----------------------------------\n" +
               $"Ball Touched : <color={touchColor}>{hasTouchedBall}</color>\n" +
               $"Agent Speed : {m_DispVel:F2} m/s\n" +
               $"----------------------------------\n" +
               $"Distance Rew : {m_DispDist:F4}\n" +
               $"Ball Goal Rew: <color=#FFD700>{m_DispBallGoalRew:F4}</color>\n" +
               $"Spine Penalty: {m_DispSpine:F4}\n" +
               $"Upright Rew  : {m_DispUpright:F4}\n" +
               $"----------------------------------\n" +
               $"<size=30><b>TOTAL REW : {m_DispTotal:F2}</b></size>";
    }

    #endregion

    #region Utility Methods

    private float Map(float val, float min, float max)
    {
        return val >= 0 ? val * max : val * Mathf.Abs(min);
    }

    private void SetJointRotation(ConfigurableJoint j, float x, float y, float z)
    {
        if (j != null)
        {
            j.targetRotation = Quaternion.Euler(x, y, z);
        }
    }

    #endregion
}