using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class NupJukSnowBoardAgent : Agent
{
    #region Inspector Fields
    [Header("Body Joints")]
    public ConfigurableJoint hipL, calfL, hipR, calfR, spine2, shoulderL, shoulderR, handL, handR;
    public ConfigurableJoint FootL, FootR; // [추가] 발목 관절
    public Rigidbody spine1Rb;

    [Header("Snowboard Integration")]
    public Rigidbody snowboardRb;
    public Rigidbody footLRb, footRRb;

    [Header("Settings")]
    [Range(0.01f, 1.0f)]
    public float angleSmooth = 0.2f;

    [Header("Visual Effects")]
    public ParticleSystem landingParticle;
    #endregion

    #region Private Fields
    private float[] curActions = new float[14]; // [수정] 12 -> 14로 확장

    private struct RBInit
    {
        public Rigidbody rb;
        public Vector3 pos;
        public Quaternion rot;
    }
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
        UpdateActions(actions);
        ApplyJointRotations();
    }
    #endregion

    #region Joint & Physics Logic
    private void AttachFeetToBoard()
    {
        if (snowboardRb == null || footLRb == null || footRRb == null) return;

        // 이미 조인트가 있다면 제거하고 새로 만듦 (중복 방지)
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
    #endregion

    public void HandleBoardCollision(Collision collision)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;

        if (collision.gameObject.CompareTag("Ground") && impactSpeed > 3.0f)
        {
            if (landingParticle != null)
            {
                Vector3 spawnPos = collision.contacts[0].point;
                ParticleSystem effect = Instantiate(landingParticle, spawnPos, Quaternion.Euler(-90, 0, 0));

                var main = effect.main;

                float sizeMultiplier = impactSpeed * 10f;
                Debug.Log($"{sizeMultiplier}");

                main.startSizeMultiplier = Mathf.Clamp(sizeMultiplier, 5.0f, 100f);

                effect.Play();
                Destroy(effect.gameObject, 3.0f);
            }
        }
    }

    #region Physics Update
    void FixedUpdate()
    {
        if (snowboardRb == null) return;

        Vector3 currentVel = snowboardRb.linearVelocity;
        // 사용자 설정에 따른 전진 방향 (Right)
        Vector3 forwardDir = snowboardRb.transform.right;

        Vector3 forwardVel = Vector3.Project(currentVel, forwardDir);
        Vector3 sideVel = currentVel - forwardVel;

        snowboardRb.AddForce(-sideVel * 10f, ForceMode.Acceleration);

        float roll = snowboardRb.transform.localEulerAngles.z;
        if (roll > 180) roll -= 360;

        // GUI 출력을 위한 데이터 저장
        currentSpeed = forwardVel.magnitude;

        if (currentSpeed > 0.1f && Mathf.Abs(roll) > 1.0f)
        {
            float sidecutRadius = 5.0f;
            // 순수 물리 카빙 반지름 공식: R = R_sidecut / cos(theta)
            currentTurnRadius = sidecutRadius / Mathf.Max(Mathf.Cos(roll * Mathf.Deg2Rad), 0.05f);

            float speed = forwardVel.magnitude;
            float centripetalForce = (snowboardRb.mass * speed * speed) / sidecutRadius;
            currentForce = centripetalForce * 0.5f;

            snowboardRb.AddForce(snowboardRb.transform.right * centripetalForce * Mathf.Sign(roll), ForceMode.Force);
        }
        else
        {
            currentTurnRadius = 0f;
            currentForce = 0f;
        }

        Debug.DrawRay(snowboardRb.position, forwardVel, Color.blue);
        Debug.DrawRay(snowboardRb.position, -sideVel, Color.red);
    }
    #endregion

    #region Utility Methods
    private float Map(float val, float min, float max)
    {
        return val >= 0 ? val * max : val * Mathf.Abs(min);
    }

    private void SetJointRotation(ConfigurableJoint j, float x, float y, float z)
    {
        if (j != null) j.targetRotation = Quaternion.Euler(x, y, z);
    }
    #endregion

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            EndEpisode();
            return;
        }
        var continuousActionsOut = actionsOut.ContinuousActions;
        for (int i = 0; i < 14; i++) continuousActionsOut[i] = 0f;

        float horizontal = 0f;
        if (Input.GetKey(KeyCode.J)) horizontal = -1f;
        else if (Input.GetKey(KeyCode.L)) horizontal = 1f;

        continuousActionsOut[5] = horizontal;
        continuousActionsOut[1] = horizontal * 0.5f;
        continuousActionsOut[3] = horizontal * 0.5f;

        float vertical = 0f;
        if (Input.GetKey(KeyCode.I)) vertical = 1f;
        else if (Input.GetKey(KeyCode.K)) vertical = -1f;

        float moveStrength = vertical * 0.3f;

        continuousActionsOut[4] = -moveStrength;
        continuousActionsOut[0] = moveStrength;
        continuousActionsOut[2] = moveStrength;
        continuousActionsOut[6] = moveStrength;
        continuousActionsOut[7] = moveStrength;
        continuousActionsOut[12] = moveStrength;
        continuousActionsOut[13] = moveStrength;
    }

    private void OnGUI()
    {
        float boxWidth = 600;
        float boxHeight = 180;
        float padding = 20;

        Texture2D bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0.05f, 0.1f, 0.22f, 0.85f));
        bgTexture.Apply();

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = bgTexture;

        GUIStyle labelStyle = new GUIStyle();
        labelStyle.fontSize = 26;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.padding = new RectOffset(10, 10, 5, 5);

        GUI.Box(new Rect(10, 10, boxWidth, boxHeight), "", boxStyle);

        GUILayout.BeginArea(new Rect(10 + padding, 20, boxWidth - (padding * 2), boxHeight));
        {
            labelStyle.normal.textColor = Color.cyan;
            GUILayout.Label("▲ NUPJUK PHYSICS MONITOR", labelStyle);
            GUILayout.Space(12);

            labelStyle.normal.textColor = new Color(0.5f, 1.0f, 0.5f);
            GUILayout.Label($"SPEED : {currentSpeed:F2} m/s", labelStyle);

            labelStyle.normal.textColor = new Color(1.0f, 0.9f, 0.3f);
            string radiusText = currentTurnRadius > 0 ? $"{currentTurnRadius:F2} m" : "---";
            GUILayout.Label($"RADIUS: {radiusText}", labelStyle);

            labelStyle.normal.textColor = new Color(1.0f, 0.4f, 0.4f);
            GUILayout.Label($"FORCE : {currentForce:F1} N", labelStyle);
        }
        GUILayout.EndArea();
    }
}