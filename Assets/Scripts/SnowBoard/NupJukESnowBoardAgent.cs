using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class NupJukSnowBoardAgent : Agent
{
    #region Inspector Fields (기존 유지)
    [Header("Body Joints")]
    public ConfigurableJoint hipL, calfL, hipR, calfR, spine2, shoulderL, shoulderR, handL, handR;
    public ConfigurableJoint FootL, FootR;
    public Rigidbody spine1Rb;

    [Header("Snowboard Integration")]
    public Rigidbody snowboardRb;
    public Rigidbody footLRb, footRRb;

    [Header("Environment Settings")]
    public Transform envTransform;

    [Header("Settings")]
    [Range(0.01f, 1.0f)]
    public float angleSmooth = 0.2f;

    [Header("Visual Effects")]
    public ParticleSystem landingParticle;
    #endregion

    #region Private Fields
    private float[] curActions = new float[14];
    private bool isGrounded;
    private bool isJumping;
    private bool wasJumping;        // 이전 프레임의 점프 상태
    private float maxJumpHeight;    // 점프 중 도달한 최고 높이
    private float envLocalY;
    private float envLocalZ;

    private struct RBInit { public Rigidbody rb; public Vector3 pos; public Quaternion rot; }
    private List<RBInit> rbInits = new List<RBInit>();
    private List<Rigidbody> bodyParts = new List<Rigidbody>();
    #endregion

    #region Debug Fields
    private float currentSpeed;
    private float currentTurnRadius;
    private float currentForce;
    #endregion

    #region ML-Agents Lifecycle
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

        foreach (float a in curActions)
        {
            sensor.AddObservation(a);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        UpdateJumpState();

        if (snowboardRb.position.y < 0f)
        {
            AddReward(-5.0f);
            EndEpisode();
            return;
        }

        // --- 1. 상태 변화에 따른 보상 (Transitions) ---
        if (!wasJumping && isJumping) // Stable -> Jump 시작
        {
            AddReward(0.1f); // 점프 시도 격려
            maxJumpHeight = snowboardRb.position.y; // 최고 높이 기록 시작
            Debug.Log("점프시작!!");
        }
        else if (wasJumping && !isJumping) // Jump -> Stable 착지 성공
        {
            AddReward(1.0f); // 착지 성공 보상 (훨씬 크게 부여)
            // (예: 높이 1m당 0.1점 추가 보상, 맵 스케일에 맞춰 조절하세요)
            float heightBonus = Mathf.Max(0, maxJumpHeight - 35f) * 0.1f;
            AddReward(heightBonus);

            Debug.Log($"착지성공!! Max Height: {maxJumpHeight}");

            maxJumpHeight = 0f; // 기록 초기화

        }

        // 점프 중일 때 실시간으로 최고 높이 갱신
        if (isJumping)
        {
            maxJumpHeight = Mathf.Max(maxJumpHeight, snowboardRb.position.y);
        }

        wasJumping = isJumping; // 상태 업데이트

        UpdateActions(actions);
        ApplyJointRotations();

        // --- 2. 속도 보상 (Forward Speed) ---
        AddReward(0.001f); // 생존 보상

        Vector3 forwardDir = snowboardRb.transform.right;
        float forwardSpeed = Vector3.Dot(snowboardRb.linearVelocity, forwardDir);

        if (forwardSpeed > 0)
        {
            AddReward(forwardSpeed * 0.01f); // 전진 속도 보상
        }
    }

    private void UpdateJumpState()
    {
        if (envTransform == null) return;

        Vector3 localPos = envTransform.InverseTransformPoint(snowboardRb.position);
        envLocalY = localPos.y;
        envLocalZ = localPos.z;

        isJumping = (Mathf.Abs(envLocalZ) > 15) || (envLocalY > 35);
    }
    #endregion

    #region Physics & Utility (기존 로직 유지)
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
                foreach (var ac in bodyColliders)
                {
                    Physics.IgnoreCollision(bc, ac, true);
                }
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
            s.rb.Sleep();
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
        for (int i = 0; i < 14; i++)
        {
            curActions[i] = Mathf.Lerp(curActions[i], Mathf.Clamp(a[i], -1f, 1f), angleSmooth);
        }
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

    public void HandleBoardCollision(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground")) isGrounded = true;
        float impactSpeed = collision.relativeVelocity.magnitude;
        if (collision.gameObject.CompareTag("Ground") && impactSpeed > 3.0f)
        {
            if (landingParticle != null)
            {
                Vector3 spawnPos = collision.contacts[0].point;
                ParticleSystem effect = Instantiate(landingParticle, spawnPos, Quaternion.Euler(-90, 0, 0));
                effect.Play();
                Destroy(effect.gameObject, 3.0f);
            }
        }
    }

    public void HandleHeadCollision()
    {
        AddReward(-5.0f);
        EndEpisode();
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground")) isGrounded = false;
    }

    void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.T)) { EndEpisode(); return; }
        if (snowboardRb == null || !isGrounded) return;

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
    }

    private float Map(float val, float min, float max)
    {
        return val >= 0 ? val * max : val * Mathf.Abs(min);
    }

    private void SetJointRotation(ConfigurableJoint j, float x, float y, float z)
    {
        if (j != null) j.targetRotation = Quaternion.Euler(x, y, z);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        for (int i = 0; i < 14; i++) continuousActionsOut[i] = 0f;
        if (Input.GetKey(KeyCode.Space))
        {
            float time = Time.time * 10f;
            float backWave = (Mathf.Sin(time) + 1f) / 2f;
            float frontWave = (Mathf.Sin(time - 1.5f) + 1f) / 2f;
            continuousActionsOut[2] = backWave * 0.7f;
            continuousActionsOut[7] = backWave * -1.0f;
            continuousActionsOut[13] = backWave * 0.8f;
            continuousActionsOut[0] = frontWave * 0.7f;
            continuousActionsOut[6] = frontWave * -1.0f;
            continuousActionsOut[12] = frontWave * 0.8f;
            continuousActionsOut[4] = backWave * -0.8f;
        }
    }
    #endregion

    private void OnGUI()
    {
        float boxWidth = 500;
        float boxHeight = 350;
        float padding = 20;

        Texture2D bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0.02f, 0.05f, 0.1f, 0.9f));
        bgTexture.Apply();

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = bgTexture;

        GUIStyle labelStyle = new GUIStyle();
        labelStyle.fontSize = 20;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.padding = new RectOffset(5, 5, 2, 2);

        GUI.Box(new Rect(10, 10, boxWidth, boxHeight), "", boxStyle);

        GUILayout.BeginArea(new Rect(10 + padding, 20, boxWidth - (padding * 2), boxHeight));
        {
            labelStyle.normal.textColor = Color.cyan;
            GUILayout.Label("▲ NUPJUK JUMP & PHYSICS MONITOR", labelStyle);
            GUILayout.Space(10);

            labelStyle.normal.textColor = new Color(0.5f, 1.0f, 0.5f);
            GUILayout.Label($"SPEED  : {currentSpeed:F2} m/s", labelStyle);

            string radiusText = currentTurnRadius > 0 ? $"{currentTurnRadius:F2} m" : "---";
            GUILayout.Label($"RADIUS : {radiusText}", labelStyle);

            labelStyle.normal.textColor = Color.gray;
            GUILayout.Label("--------------------------------------", labelStyle);

            labelStyle.normal.textColor = Color.white;
            GUILayout.Label($"Env Local Y : {envLocalY:F2}", labelStyle);
            GUILayout.Label($"Env Local Z : {envLocalZ:F2}", labelStyle);

            labelStyle.normal.textColor = Color.gray;
            GUILayout.Label("--------------------------------------", labelStyle);

            labelStyle.normal.textColor = isJumping ? Color.red : Color.gray;
            string jumpStatus = isJumping ? "● JUMPING" : "○ STABLE";
            GUILayout.Label($"STATE : {jumpStatus}", labelStyle);

            labelStyle.normal.textColor = new Color(1.0f, 0.9f, 0.3f);
            GUILayout.Label($"MAX JUMP HEIGHT : {maxJumpHeight:F2} m", labelStyle);
        }
        GUILayout.EndArea();
    }
}