using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class RunningAgent : Agent
{
    public GameObject target;
    public Rigidbody spine1Rb;
    public ConfigurableJoint hipL, calfL, hipR, calfR, spine2, shoulderL, shoulderR, handL, handR;
    public Collider headCol;
    public Transform footL, footR;
    public List<GameObject> obstacleList;

    public float angleSmooth = 0.2f;

    private float[] curActions = new float[12];
    private float m_PreviousDistance, m_StartingZ;
    private float m_RewardDist, m_RewardUpright, m_RewardFace, m_RewardSide, m_RewardMove, m_RewardTotal, m_DistDelta;

    private float m_DispDist, m_DispUpright, m_DispFace, m_DispSide, m_DispMove, m_DispTotal, m_DispVel, m_DispActualDist;
    private float m_GuiTimer;
    private const float GUI_UPDATE_INTERVAL = 0.3f;

    struct RBInit { public Rigidbody rb; public Vector3 pos; public Quaternion rot; }
    List<RBInit> rbInits = new List<RBInit>();
    List<Rigidbody> bodyParts = new List<Rigidbody>();
    private bool isHeadTouching;
    private Transform targetTf;

    private int m_ObstacleIndex = 0;
    private float m_ObsRelX, m_ObsRelZ, m_ObsH;

    public override void Initialize()
    {
        rbInits.Clear();
        bodyParts.Clear();
        if (target != null) targetTf = target.transform;

        Rigidbody[] allRbs = GetComponentsInChildren<Rigidbody>();

        foreach (var rb in allRbs)
        {
            rbInits.Add(new RBInit { rb = rb, pos = rb.position, rot = rb.rotation });
            if (rb != spine1Rb) bodyParts.Add(rb);
        }

        for (int i = 0; i < allRbs.Length; i++)
        {
            for (int j = i + 1; j < allRbs.Length; j++)
            {
                Collider colA = allRbs[i].GetComponent<Collider>();
                Collider colB = allRbs[j].GetComponent<Collider>();

                if (colA != null && colB != null)
                {
                    Physics.IgnoreCollision(colA, colB);
                }
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

        for (int i = 0; i < 12; i++) curActions[i] = 0f;
        isHeadTouching = false;
        m_ObstacleIndex = 0;

        if (targetTf != null) m_PreviousDistance = Vector3.Distance(spine1Rb.position, targetTf.position);
        // 시작 시점의 Z축(좌우) 위치 저장
        m_StartingZ = spine1Rb.position.z;
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

        if (obstacleList != null && m_ObstacleIndex < obstacleList.Count)
        {
            GameObject curObs = obstacleList[m_ObstacleIndex];
            Vector3 relPos = transform.InverseTransformPoint(curObs.transform.position);
            // X: 거리(전방), Z: 좌우 편차
            sensor.AddObservation(relPos.x);
            sensor.AddObservation(relPos.z);
            sensor.AddObservation(curObs.transform.localScale.y);
            sensor.AddObservation(curObs.transform.localScale.x);
            m_ObsRelX = relPos.x; m_ObsRelZ = relPos.z; m_ObsH = curObs.transform.localScale.y;
        }
        else
        {
            sensor.AddObservation(50f); sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f);
            m_ObsRelX = 50f; m_ObsRelZ = 0f; m_ObsH = 0f;
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
        SetJointRotation(shoulderL, Map(curActions[8], -10f, 70f), 0, 0);
        SetJointRotation(shoulderR, Map(curActions[9], -10f, 70f), 0, 0);

        float currentDistance = Vector3.Distance(spine1Rb.position, targetTf.position);
        m_DistDelta = m_PreviousDistance - currentDistance;
        float upDot = Vector3.Dot(spine1Rb.transform.up, Vector3.up);

        m_RewardDist = m_DistDelta * 0.5f;
        m_RewardUpright = (upDot < 0.7f) ? 0f : (upDot - 0.7f) / 0.3f * 0.005f;
        // X축 방향 정렬 보상
        m_RewardFace = -(1f - Mathf.Clamp(Vector3.Dot(spine1Rb.transform.forward, Vector3.right), -1f, 1f)) * 0.03f;
        // 사이드 보상: 시작 Z 위치와의 차이 계산
        m_RewardSide = -Mathf.Pow(spine1Rb.position.z - m_StartingZ, 2) * 0.03f;
        m_RewardMove = (spine1Rb.linearVelocity.magnitude < 0.2f) ? -0.05f : 0f;

        m_RewardTotal = m_RewardDist + m_RewardUpright + m_RewardFace + m_RewardSide + m_RewardMove;
        AddReward(m_RewardTotal);

        if (upDot < 0.6f || isHeadTouching) { SetReward(-5.0f); EndEpisode(); }
        m_PreviousDistance = currentDistance;

        // 장애물 통과 업데이트 (X축 기준)
        if (obstacleList != null && m_ObstacleIndex < obstacleList.Count)
        {
            if (obstacleList[m_ObstacleIndex].transform.position.x - spine1Rb.position.x < -0.6f) m_ObstacleIndex++;
        }
    }

    void Update()
    {
        m_GuiTimer += Time.deltaTime;
        if (m_GuiTimer >= GUI_UPDATE_INTERVAL)
        {
            m_DispDist = m_RewardDist; m_DispUpright = m_RewardUpright; m_DispFace = m_RewardFace;
            m_DispSide = m_RewardSide; m_DispMove = m_RewardMove; m_DispTotal = m_RewardTotal;
            m_DispVel = spine1Rb.linearVelocity.magnitude;
            m_DispActualDist = Vector3.Distance(spine1Rb.position, targetTf.position);
            m_GuiTimer = 0f;
        }
    }

    private void OnGUI()
    {
        if (Camera.main == null || spine1Rb == null || targetTf == null) return;
        GUIStyle style = new GUIStyle { fontSize = 28, richText = true };
        style.normal.textColor = Color.white;
        GUI.backgroundColor = new Color(0, 0, 0, 0.9f);
        Rect rect = new Rect(30, 30, 500, 580);
        GUI.Box(rect, "");
        string debugText = $"<b><size=32>[ NUPJUK MONITOR ]</size></b>\n" +
                           $"----------------------------------\n" +
                           $"Distance : {m_DispActualDist:F2}m\n" +
                           $"Velocity : {m_DispVel:F2}m/s\n" +
                           $"----------------------------------\n" +
                           $"<b>[ OBSTACLE INFO ]</b>\n" +
                           $"Index    : {m_ObstacleIndex} / {obstacleList?.Count ?? 0}\n" +
                           $"Rel Dist(X): {m_ObsRelX:F2}m\n" +
                           $"Rel Side(Z): {m_ObsRelZ:F2}m\n" +
                           $"----------------------------------\n" +
                           $"<color=yellow>Forward  : {m_DispDist:F4}</color>\n" +
                           $"<color=cyan>Upright  : {m_DispUpright:F4}</color>\n" +
                           $"<color=#FF8C00>Side Pen : {m_DispSide:F4}</color>\n" +
                           $"----------------------------------\n" +
                           $"<b>TOTAL    : {m_DispTotal:F4}</b>";
        GUI.Label(new Rect(rect.x + 20, rect.y + 15, rect.width - 40, rect.height - 30), debugText, style);
    }

    float Map(float val, float min, float max) => val >= 0 ? val * max : val * Mathf.Abs(min);
    void SetJointRotation(ConfigurableJoint j, float x, float y, float z) { if (j != null) j.targetRotation = Quaternion.Euler(x, y, z); }
    private void OnCollisionEnter(Collision collision) { if (collision.collider.CompareTag("Ground")) foreach (var contact in collision.contacts) if (contact.thisCollider == headCol) { isHeadTouching = true; break; } }
    private void OnCollisionExit(Collision collision) { if (collision.collider.CompareTag("Ground")) isHeadTouching = false; }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions; float v = -Input.GetAxis("Vertical"); float t = Time.time * 11f;
        for (int i = 0; i < 12; i++) ca[i] = 0f;
        if (v != 0)
        {
            ca[0] = Mathf.Sin(t) * 1.8f * v; ca[2] = Mathf.Sin(t + Mathf.PI) * 1.8f * v;
            ca[1] = -Mathf.Max(0, Mathf.Cos(t)) * 4.5f * v; ca[3] = -Mathf.Max(0, Mathf.Cos(t + Mathf.PI)) * 4.5f * v;
            ca[6] = -Mathf.Max(0, Mathf.Sin(t + 0.5f)) * 3.5f * v; ca[7] = -Mathf.Max(0, Mathf.Sin(t + Mathf.PI + 0.5f)) * 3.5f * v;
            ca[4] = Mathf.Cos(t) * 0.7f * v; ca[5] = 1.0f * v;
            ca[8] = Mathf.Sin(t + Mathf.PI) * 1.5f; ca[9] = Mathf.Sin(t) * 1.5f;
            ca[10] = Mathf.Sin(t + Mathf.PI) * 1.0f; ca[11] = Mathf.Sin(t) * 1.0f;
        }
    }
}