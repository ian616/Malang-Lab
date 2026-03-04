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

    [Range(0.01f, 1.0f)]
    public float angleSmooth = 0.2f;
    public float initialXVelocity = 5.0f;

    public ParticleSystem landingParticle;
    public ParticleSystem snowDustParticle;
    public TrailRenderer boardTrail;

    [Header("Snow Particle Settings")]
    public float particleSpeedThreshold = 3.0f;

    private float[] curActions = new float[14];
    private bool isGrounded;
    private bool isJumping;
    private bool wasJumping;
    private float maxJumpHeight;
    private float envLocalY;
    private float envLocalZ;

    private struct RBInit { public Rigidbody rb; public Vector3 pos; public Quaternion rot; }
    private List<RBInit> rbInits = new List<RBInit>();
    private List<Rigidbody> bodyParts = new List<Rigidbody>();

    private float m_SurvivalReward;
    private float m_JumpReward;
    private float m_LandingReward;
    private float m_HeightReward;
    private float m_TotalStepReward;
    private float m_CumulatedReward;

    private float currentSpeed;
    private float currentTurnRadius;
    private float currentForce;

    private bool isFirstFrame;
    private float lastSpawnTime;

    // --- 추가된 파티클 관리 변수 ---
    private ParticleSystem activeDust;
    private bool isParticleActive;

    public override void Initialize()
    {
        SetupCollisionIgnoring();
        AttachFeetToBoard();
        InitializeRigidbodies();
    }

    public override void OnEpisodeBegin()
    {
        ResetRigidbodies();
        ResetActions();
        isGrounded = false;
        isJumping = false;
        wasJumping = false;
        maxJumpHeight = 0f;
        m_CumulatedReward = 0f;
        isFirstFrame = true;

        // 에피소드 시작 시 파티클 초기화
        if (activeDust != null) Destroy(activeDust.gameObject);
        isParticleActive = false;

        if (snowDustParticle != null)
        {
            snowDustParticle.Stop();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformPoint(snowboardRb.position));
        sensor.AddObservation(snowboardRb.transform.localRotation);
        sensor.AddObservation(transform.InverseTransformDirection(snowboardRb.linearVelocity));
        foreach (var rb in bodyParts)
        {
            sensor.AddObservation(transform.InverseTransformPoint(rb.position));
            sensor.AddObservation(rb.transform.localRotation);
            sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));
        }
        foreach (float a in curActions) sensor.AddObservation(a);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        m_SurvivalReward = 0f;
        m_JumpReward = 0f;
        m_LandingReward = 0f;
        m_HeightReward = 0f;

        UpdateJumpState();

        if (snowboardRb.position.y < 0f)
        {
            AddReward(-5.0f);
            m_CumulatedReward += -5.0f;
            EndEpisode();
            return;
        }

        if (!wasJumping && isJumping)
        {
            m_JumpReward = 0.1f;
            AddReward(m_JumpReward);
            maxJumpHeight = snowboardRb.position.y;
            Debug.Log("점프 상태 진입!");
        }
        else if (wasJumping && isGrounded)
        {
            m_LandingReward = 1.0f;
            AddReward(m_LandingReward);
            m_HeightReward = Mathf.Max(0, maxJumpHeight - 35f) * 0.1f;
            AddReward(m_HeightReward);

            isJumping = false;
            maxJumpHeight = 0f;
            Debug.Log("착지 성공, Stable 전환!");
        }

        if (isJumping) maxJumpHeight = Mathf.Max(maxJumpHeight, snowboardRb.position.y);

        wasJumping = isJumping;

        UpdateActions(actions);
        ApplyJointRotations();

        m_SurvivalReward = 0.001f;
        AddReward(m_SurvivalReward);

        m_TotalStepReward = m_SurvivalReward + m_JumpReward + m_LandingReward + m_HeightReward;
        m_CumulatedReward += m_TotalStepReward;
    }

    private void UpdateJumpState()
    {
        if (envTransform == null) return;
        Vector3 localPos = envTransform.InverseTransformPoint(snowboardRb.position);
        envLocalY = localPos.y;
        envLocalZ = localPos.z;
        isJumping = !isGrounded && (Mathf.Abs(envLocalZ) > 15 || envLocalY > 35);
    }

    public void HandleBoardCollision(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed > 3.0f && landingParticle != null)
            {
                Vector3 spawnPos = collision.contacts[0].point;
                ParticleSystem effect = Instantiate(landingParticle, spawnPos, Quaternion.Euler(-90, 0, 0));
                var main = effect.main;
                main.startSizeMultiplier = Mathf.Clamp(impactSpeed * 20f, 15f, 100f);
                effect.Play();
                Destroy(effect.gameObject, 3.0f);
            }
        }
    }

    public void HandleBoardCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }

    public void HandleHeadCollision()
    {
        AddReward(-5.0f);
        m_CumulatedReward += -5.0f;
        EndEpisode();
    }

    private void AttachFeetToBoard()
    {
        if (snowboardRb == null || footLRb == null || footRRb == null) return;
        foreach (var joint in footLRb.GetComponents<FixedJoint>()) DestroyImmediate(joint);
        foreach (var joint in footRRb.GetComponents<FixedJoint>()) DestroyImmediate(joint);
        FixedJoint leftJoint = footLRb.gameObject.AddComponent<FixedJoint>();
        leftJoint.connectedBody = snowboardRb;
        leftJoint.autoConfigureConnectedAnchor = true;
        leftJoint.enableCollision = false;
        leftJoint.breakForce = Mathf.Infinity;
        FixedJoint rightJoint = footRRb.gameObject.AddComponent<FixedJoint>();
        rightJoint.connectedBody = snowboardRb;
        rightJoint.autoConfigureConnectedAnchor = true;
        rightJoint.enableCollision = false;
        rightJoint.breakForce = Mathf.Infinity;
    }

    private void InitializeRigidbodies()
    {
        rbInits.Clear();
        bodyParts.Clear();
        var allRbs = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in allRbs)
        {
            rbInits.Add(new RBInit { rb = rb, pos = rb.position, rot = rb.rotation });
            if (rb != spine1Rb) bodyParts.Add(rb);
        }
    }

    private void SetupCollisionIgnoring()
    {
        var bodyColliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < bodyColliders.Length; i++)
        {
            for (int j = i + 1; j < bodyColliders.Length; j++)
            {
                Physics.IgnoreCollision(bodyColliders[i], bodyColliders[j], true);
            }
        }
        if (snowboardRb != null)
        {
            var boardColliders = snowboardRb.GetComponentsInChildren<Collider>();
            foreach (var bc in boardColliders)
            {
                foreach (var ac in bodyColliders) Physics.IgnoreCollision(bc, ac, true);
            }
        }
    }

    private void ResetRigidbodies()
    {
        foreach (var s in rbInits)
        {
            s.rb.position = s.pos;
            s.rb.rotation = s.rot;
            s.rb.linearVelocity = Vector3.zero;
            s.rb.angularVelocity = Vector3.zero;
            s.rb.WakeUp();
        }
    }

    private void ResetActions()
    {
        System.Array.Clear(curActions, 0, curActions.Length);
    }

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
            foreach (var s in rbInits) s.rb.linearVelocity = new Vector3(initialXVelocity, 0f, 0f);
            isFirstFrame = false;
        }
        if (Input.GetKeyDown(KeyCode.T)) { EndEpisode(); return; }

        if (snowboardRb != null && isGrounded)
        {
            Vector3 currentVel = snowboardRb.linearVelocity;
            Vector3 forwardDir = snowboardRb.transform.right;
            Vector3 forwardVel = Vector3.Project(currentVel, forwardDir);
            Vector3 sideVel = currentVel - forwardVel;
            snowboardRb.AddForce(-sideVel * 10f, ForceMode.Acceleration);
            float roll = snowboardRb.transform.localEulerAngles.z;
            if (roll > 180) roll -= 360;
            currentSpeed = forwardVel.magnitude;

            if (currentSpeed > 0.1f && Mathf.Abs(roll) > 1.0f)
            {
                float sidecutRadius = 5.0f;
                currentTurnRadius = sidecutRadius / Mathf.Max(Mathf.Cos(roll * Mathf.Deg2Rad), 0.05f);
                float centripetalForce = (snowboardRb.mass * currentSpeed * currentSpeed) / sidecutRadius;
                snowboardRb.AddForce(snowboardRb.transform.right * centripetalForce * Mathf.Sign(roll), ForceMode.Force);
            }

            if (!isParticleActive && snowDustParticle != null && currentSpeed > particleSpeedThreshold)
            {
                Vector3 spawnPos = snowboardRb.position + (snowboardRb.transform.right * 2.5f);
                Quaternion tilt = Quaternion.Euler(0, 0, -45f);
                Quaternion spawnRot = snowboardRb.rotation * tilt;

                activeDust = Instantiate(snowDustParticle, spawnPos, spawnRot);
                activeDust.transform.SetParent(snowboardRb.transform);
                activeDust.Play();
                isParticleActive = true;
            }

            if (boardTrail != null)
            {
                boardTrail.emitting = currentSpeed > 1.0f;
            }
        }
        else
        {
            if (isParticleActive)
            {
                if (activeDust != null)
                {
                    Destroy(activeDust.gameObject);
                    activeDust = null;
                }
                isParticleActive = false;
            }

            if (boardTrail != null)
            {
                boardTrail.emitting = false;
            }
        }
    }

    private float Map(float val, float min, float max) => val >= 0 ? val * max : val * Mathf.Abs(min);
    private void SetJointRotation(ConfigurableJoint j, float x, float y, float z) { if (j != null) j.targetRotation = Quaternion.Euler(x, y, z); }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        for (int i = 0; i < 14; i++) cont[i] = 0f;

        if (Input.GetKey(KeyCode.Space))
        {
            cont[2] = 0.7f; cont[7] = -1f; cont[0] = 0.7f; cont[6] = -1f;
        }

        if (Input.GetKey(KeyCode.I))
        {
            cont[12] = 0.5f;
            cont[13] = 0.5f;
            cont[4] = -0.5f;
            cont[0] = 0.6f;
            cont[2] = 0.6f;
        }

        if (Input.GetKey(KeyCode.K))
        {
            cont[12] = -0.5f;
            cont[13] = -0.5f;
            cont[4] = 0.5f;
            cont[0] = -0.5f;
            cont[2] = -0.5f;
        }
    }

    private void OnGUI()
    {
        float boxWidth = 500; float boxHeight = 550; float padding = 20;
        Texture2D bgTexture = new Texture2D(1, 1); bgTexture.SetPixel(0, 0, new Color(0.02f, 0.05f, 0.1f, 0.9f)); bgTexture.Apply();
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box); boxStyle.normal.background = bgTexture;
        GUIStyle labelStyle = new GUIStyle(); labelStyle.fontSize = 20; labelStyle.fontStyle = FontStyle.Bold; labelStyle.padding = new RectOffset(5, 5, 2, 2);
        GUI.Box(new Rect(10, 10, boxWidth, boxHeight), "", boxStyle);
        GUILayout.BeginArea(new Rect(10 + padding, 20, boxWidth - (padding * 2), boxHeight));
        {
            labelStyle.normal.textColor = Color.cyan; GUILayout.Label("▲ NUPJUK JUMP & PHYSICS MONITOR", labelStyle); GUILayout.Space(10);
            labelStyle.normal.textColor = new Color(0.5f, 1.0f, 0.5f); GUILayout.Label($"SPEED  : {currentSpeed:F2} m/s", labelStyle);
            string radiusText = currentTurnRadius > 0 ? $"{currentTurnRadius:F2} m" : "---"; GUILayout.Label($"RADIUS : {radiusText}", labelStyle);
            labelStyle.normal.textColor = Color.gray; GUILayout.Label("--------------------------------------", labelStyle);
            labelStyle.normal.textColor = Color.white; GUILayout.Label($"Env Local Y : {envLocalY:F2}", labelStyle); GUILayout.Label($"Env Local Z : {envLocalZ:F2}", labelStyle);
            labelStyle.normal.textColor = Color.gray; GUILayout.Label("--------------------------------------", labelStyle);
            labelStyle.normal.textColor = isJumping ? Color.red : Color.gray;
            string jumpStatus = isJumping ? "● JUMPING" : "○ STABLE"; GUILayout.Label($"STATE : {jumpStatus}", labelStyle);
            labelStyle.normal.textColor = new Color(1.0f, 0.9f, 0.3f); GUILayout.Label($"MAX JUMP HEIGHT : {maxJumpHeight:F2} m", labelStyle);
            labelStyle.normal.textColor = Color.gray; GUILayout.Label("--------------------------------------", labelStyle);
            labelStyle.normal.textColor = new Color(1.0f, 0.7f, 0.3f); GUILayout.Label("[ REWARDS MONITORING ]", labelStyle);
            labelStyle.normal.textColor = Color.white; GUILayout.Label($"Step Reward : {m_TotalStepReward:F4}", labelStyle);
            GUILayout.Space(5); labelStyle.normal.textColor = Color.yellow; GUILayout.Label($"EPISODE TOTAL : {m_CumulatedReward:F4}", labelStyle);
            labelStyle.normal.textColor = Color.gray; GUILayout.Label("--------------------------------------", labelStyle);
            labelStyle.fontSize = 16; labelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            GUILayout.Label($"Last Landing : +{m_LandingReward:F1} | Height Bonus : +{m_HeightReward:F2}", labelStyle);
        }
        GUILayout.EndArea();
    }
}