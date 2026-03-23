using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class NupJukSnowBoardAgent : Agent
{
    public ConfigurableJoint hipL, calfL, hipR, calfR, spine2, shoulderL, shoulderR, handL, handR;
    public ConfigurableJoint FootL, FootR;
    public Rigidbody spine1Rb;
    public Rigidbody snowboardRb;
    public Rigidbody footLRb, footRRb;
    public Transform envTransform;

    [Header("Training Target")]
    public Transform target;

    [Range(0.01f, 1.0f)]
    public float angleSmooth = 0.2f;
    public float initialXVelocity = 5.0f;

    public ParticleSystem landingParticle;
    public GoalDetectionSnowboard goalDetector;

    private float[] curActions = new float[14];
    private bool isGrounded;

    private struct RBInit { public Rigidbody rb; public Vector3 pos; public Quaternion rot; }
    private List<RBInit> rbInits = new List<RBInit>();
    private List<Rigidbody> bodyParts = new List<Rigidbody>();

    // --- 변화량 측정을 위한 변수 추가 ---
    private float m_PrevLookDot;

    private float m_SurvivalReward;
    private float m_ApproachReward;
    private float m_LookPenalty;
    private float m_RotationReward;

    private float m_TotalStepReward;
    private float m_CumulatedReward;

    private float currentSpeed;
    private float currentTurnRadius;
    private float currentForce;
    private float m_CurrentRoll;

    private bool isFirstFrame;

    public override void Initialize()
    {
        SetupCollisionIgnoring();
        AttachFeetToBoard();
        InitializeRigidbodies();
    }

    public override void OnEpisodeBegin()
    {
        if (goalDetector != null) goalDetector.ResetGoal();
        ResetRigidbodies();
        ResetActions();
        isGrounded = false;
        m_CumulatedReward = 0f;
        isFirstFrame = true;
        currentTurnRadius = 0f;
        currentForce = 0f;

        // 초기 lookDot 설정 (-right 축 교정 적용)
        if (target != null)
        {
            Vector3 boardForward = -snowboardRb.transform.right;
            Vector3 toTargetDir = (target.position - snowboardRb.position).normalized;
            m_PrevLookDot = Vector3.Dot(boardForward, toTargetDir);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformPoint(snowboardRb.position));
        sensor.AddObservation(snowboardRb.transform.localRotation);
        sensor.AddObservation(transform.InverseTransformDirection(snowboardRb.linearVelocity));
        sensor.AddObservation(transform.InverseTransformDirection(snowboardRb.angularVelocity));

        if (target != null) sensor.AddObservation(transform.InverseTransformPoint(target.position));
        else sensor.AddObservation(Vector3.zero);

        foreach (var rb in bodyParts)
        {
            sensor.AddObservation(transform.InverseTransformPoint(rb.position));
            sensor.AddObservation(rb.transform.localRotation);
            sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));
            sensor.AddObservation(transform.InverseTransformDirection(rb.angularVelocity));
        }

        foreach (float a in curActions) sensor.AddObservation(a);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        Debug.DrawRay(snowboardRb.position, -snowboardRb.transform.right * 5f, Color.yellow);
        Vector3 debugToTarget = (target.position - snowboardRb.position).normalized;
        Debug.DrawRay(snowboardRb.position, debugToTarget * 5f, Color.magenta);

        m_SurvivalReward = 0f;
        m_ApproachReward = 0f;
        m_LookPenalty = 0f;
        m_RotationReward = 0f;

        if (snowboardRb.position.y < 0f)
        {
            AddReward(-5.0f);
            m_CumulatedReward += -5.0f;
            EndEpisode();
            return;
        }

        if (target != null)
        {
            if (snowboardRb.position.z > target.position.z)
            {
                AddReward(-5.0f);
                m_CumulatedReward += -5.0f;
                EndEpisode();
                return;
            }

            Vector3 toTarget = target.position - snowboardRb.position;
            toTarget.y = 0;
            Vector3 toTargetDir = toTarget.normalized;

            // 1. 축 교정 (-right)
            Vector3 boardForward = -snowboardRb.transform.right;
            boardForward.y = 0;
            boardForward.Normalize();

            float angleError = Vector3.SignedAngle(boardForward, toTargetDir, Vector3.up);
            float turningSpeed = snowboardRb.angularVelocity.y;

            // m_RotationReward = Mathf.Sign(angleError) * turningSpeed * 0.1f;
            m_RotationReward = 0f;


            m_SurvivalReward = 0f;
            float approachSpeed = Vector3.Dot(snowboardRb.linearVelocity, toTargetDir);
            m_ApproachReward = Mathf.Max(0, approachSpeed) * 0.001f;

            // 2. [수정 포인트] 변화량 기반 lookDot 보상 (lookDelta)
            float lookDot = Vector3.Dot(boardForward, toTargetDir);
            float lookDelta = lookDot - m_PrevLookDot;
            m_LookPenalty = lookDelta * 0.5f; // 좋아지면 보상(+), 나빠지면 페널티(-)
            m_PrevLookDot = lookDot;
        }

        UpdateActions(actions);
        ApplyJointRotations();

        m_TotalStepReward = m_SurvivalReward + m_ApproachReward + m_LookPenalty + m_RotationReward;
        AddReward(m_TotalStepReward);
        m_CumulatedReward += m_TotalStepReward;
    }

    public void HandleBoardCollision(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed > 4.0f && landingParticle != null)
            {
                Vector3 spawnPos = collision.contacts[0].point;
                ParticleSystem effect = Instantiate(landingParticle, spawnPos, Quaternion.Euler(-90, 0, 0));
                effect.Play();
                Destroy(effect.gameObject, 3.0f);
            }
        }
    }

    public void HandleBoardCollisionExit(Collision collision) { if (collision.gameObject.CompareTag("Ground")) isGrounded = false; }
    public void HandleHeadCollision() { AddReward(-5.0f); m_CumulatedReward += -5.0f; EndEpisode(); }

    private void AttachFeetToBoard()
    {
        if (snowboardRb == null || footLRb == null || footRRb == null) return;
        foreach (var joint in footLRb.GetComponents<FixedJoint>()) DestroyImmediate(joint);
        foreach (var joint in footRRb.GetComponents<FixedJoint>()) DestroyImmediate(joint);
        FixedJoint leftJoint = footLRb.gameObject.AddComponent<FixedJoint>();
        leftJoint.connectedBody = snowboardRb;
        FixedJoint rightJoint = footRRb.gameObject.AddComponent<FixedJoint>();
        rightJoint.connectedBody = snowboardRb;
    }

    private void InitializeRigidbodies()
    {
        rbInits.Clear(); bodyParts.Clear();
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            rbInits.Add(new RBInit { rb = rb, pos = rb.position, rot = rb.rotation });
            if (rb != spine1Rb) bodyParts.Add(rb);
        }
    }

    private void SetupCollisionIgnoring()
    {
        var colls = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colls.Length; i++)
            for (int j = i + 1; j < colls.Length; j++) Physics.IgnoreCollision(colls[i], colls[j], true);
        if (snowboardRb != null)
        {
            foreach (var bc in snowboardRb.GetComponentsInChildren<Collider>())
                foreach (var ac in colls) Physics.IgnoreCollision(bc, ac, true);
        }
    }

    private void ResetRigidbodies()
    {
        foreach (var s in rbInits)
        {
            s.rb.position = s.pos; s.rb.rotation = s.rot;
            s.rb.linearVelocity = Vector3.zero; s.rb.angularVelocity = Vector3.zero;
            s.rb.WakeUp();
        }
    }

    private void ResetActions() { System.Array.Clear(curActions, 0, curActions.Length); }
    private void UpdateActions(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        for (int i = 0; i < 14; i++) curActions[i] = Mathf.Lerp(curActions[i], Mathf.Clamp(a[i], -1f, 1f), angleSmooth);
    }

    private void ApplyJointRotations()
    {
        SetJointRotation(hipL, Map(curActions[0], -20f, 60f), Map(curActions[1], -20f, 20f), 0);
        SetJointRotation(hipR, Map(curActions[2], -20f, 60f), Map(curActions[3], -20f, 20f), 0);
        SetJointRotation(calfL, Map(curActions[6], -80f, 0f), 0, 0);
        SetJointRotation(calfR, Map(curActions[7], -80f, 0f), 0, 0);
        SetJointRotation(spine2, Map(curActions[4], -20f, 20f), Map(curActions[5], -10f, 10f), 0);
        SetJointRotation(shoulderL, Map(curActions[8], -10f, 30f), 0, 0);
        SetJointRotation(shoulderR, Map(curActions[9], -10f, 30f), 0, 0);
        SetJointRotation(handL, Map(curActions[10], 0f, 90f), 0, 0);
        SetJointRotation(handR, Map(curActions[11], 0f, 90f), 0, 0);
        SetJointRotation(FootL, Map(curActions[12], -30f, 30f), 0, 0);
        SetJointRotation(FootR, Map(curActions[13], -30f, 30f), 0, 0);
    }

    void FixedUpdate()
    {
        if (isFirstFrame)
        {
            float randomZ = Random.Range(3f, 5f); 

            foreach (var s in rbInits)
                s.rb.linearVelocity = new Vector3(initialXVelocity, 0f, randomZ);

            isFirstFrame = false;
        }
        if (Input.GetKeyDown(KeyCode.T)) { EndEpisode(); return; }

        if (snowboardRb != null && isGrounded)
        {
            Vector3 currentVel = snowboardRb.linearVelocity;
            Vector3 forwardDir = snowboardRb.transform.right;
            Vector3 forwardVel = Vector3.Project(currentVel, forwardDir);
            Vector3 sideVel = currentVel - forwardVel;
            snowboardRb.AddForce(-sideVel * 50f, ForceMode.Acceleration);

            Vector3 groundNormal = Vector3.up;
            int layerMask = ~(1 << LayerMask.NameToLayer("Agent"));
            if (Physics.Raycast(snowboardRb.position + (Vector3.up * 0.5f), Vector3.down, out RaycastHit hit, 2.5f, layerMask))
                groundNormal = hit.normal;

            float groundRelativeAngle = Vector3.SignedAngle(groundNormal, snowboardRb.transform.up, forwardDir);
            m_CurrentRoll = -(groundRelativeAngle + 90f);
            currentSpeed = forwardVel.magnitude;

            if (currentSpeed > 0.5f)
            {
                float sidecutRadius = 5.0f;
                currentTurnRadius = sidecutRadius / Mathf.Max(Mathf.Cos(m_CurrentRoll * Mathf.Deg2Rad), 0.1f);
                currentForce = (snowboardRb.mass * currentSpeed * currentSpeed) / currentTurnRadius;

                Vector3 sideEdgeDir = Vector3.Cross(groundNormal, forwardDir).normalized;
                Vector3 finalForceVector = 10.0f * sideEdgeDir * currentForce * Mathf.Sign(m_CurrentRoll);
                snowboardRb.AddForce(finalForceVector, ForceMode.Force);

                float cappedRollForTorque = Mathf.Clamp(m_CurrentRoll, -20f, 20f);
                snowboardRb.AddTorque(groundNormal * (4.0f * -cappedRollForTorque * (currentSpeed / 10f)), ForceMode.Acceleration);
            }
            else { currentTurnRadius = 0f; currentForce = 0f; }
        }
        else { m_CurrentRoll = 0f; currentTurnRadius = 0f; currentForce = 0f; }
    }

    private float Map(float val, float min, float max) => val >= 0 ? val * max : val * Mathf.Abs(min);
    private void SetJointRotation(ConfigurableJoint j, float x, float y, float z) { if (j != null) j.targetRotation = Quaternion.Euler(x, y, z); }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        for (int i = 0; i < 14; i++) cont[i] = 0f;
        float v = Input.GetAxisRaw("Vertical"); float h = Input.GetAxisRaw("Horizontal");
        cont[4] = -v * 0.8f; cont[12] = v * 0.5f; cont[13] = v * 0.5f;
        cont[5] = h * 0.7f; cont[1] = h * 0.5f; cont[3] = h * 0.5f;
    }

    // private void OnGUI()
    // {
    //     float boxWidth = 520; float boxHeight = 550; float padding = 20;
    //     Texture2D bgTexture = new Texture2D(1, 1); bgTexture.SetPixel(0, 0, new Color(0.02f, 0.05f, 0.1f, 0.95f)); bgTexture.Apply();
    //     GUIStyle boxStyle = new GUIStyle(GUI.skin.box); boxStyle.normal.background = bgTexture;
    //     GUIStyle labelStyle = new GUIStyle(); labelStyle.fontSize = 18; labelStyle.fontStyle = FontStyle.Bold; labelStyle.padding = new RectOffset(5, 5, 2, 2);

    //     GUI.Box(new Rect(10, 10, boxWidth, boxHeight), "", boxStyle);
    //     GUILayout.BeginArea(new Rect(10 + padding, 20, boxWidth - (padding * 2), boxHeight));
    //     {
    //         labelStyle.normal.textColor = Color.cyan; GUILayout.Label("▲ NUPJUK CARVING INTELLIGENCE", labelStyle); GUILayout.Space(10);

    //         labelStyle.normal.textColor = new Color(0.5f, 1.0f, 0.5f);
    //         GUILayout.Label($"SPEED  : {currentSpeed:F2} m/s", labelStyle);
    //         labelStyle.normal.textColor = Color.white;
    //         GUILayout.Label($"ROLL   : {m_CurrentRoll:F2}°", labelStyle);

    //         labelStyle.normal.textColor = Color.gray; GUILayout.Label("--------------------------------------", labelStyle);

    //         labelStyle.normal.textColor = new Color(1.0f, 0.7f, 0.3f); GUILayout.Label("[ TARGET REWARDS ]", labelStyle);

    //         labelStyle.normal.textColor = Color.white;
    //         GUILayout.Label($"Survival Reward    : {m_SurvivalReward:F5}", labelStyle);
    //         GUILayout.Label($"Approach Reward    : {m_ApproachReward:F5}", labelStyle);

    //         // lookDelta 기반 보상이 찍힘
    //         labelStyle.normal.textColor = m_LookPenalty < 0f ? Color.red : Color.white;
    //         GUILayout.Label($"Look Progress Rew  : {m_LookPenalty:F5}", labelStyle);

    //         labelStyle.normal.textColor = new Color(0.3f, 0.8f, 1.0f);
    //         GUILayout.Label($"Rotation Reward    : {m_RotationReward:F5}", labelStyle);

    //         labelStyle.normal.textColor = Color.gray; GUILayout.Label("--------------------------------------", labelStyle);

    //         labelStyle.normal.textColor = Color.yellow;
    //         GUILayout.Label($"STEP TOTAL REWARD  : {m_TotalStepReward:F5}", labelStyle);
    //         GUILayout.Space(10);

    //         labelStyle.normal.textColor = Color.green;
    //         labelStyle.fontSize = 24;
    //         GUILayout.Label($"EPISODE CUMULATED  : {m_CumulatedReward:F4}", labelStyle);
    //     }
    //     GUILayout.EndArea();
    // }
}