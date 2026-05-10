using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
using Unity.Cinemachine;

public class RunningAgent : Agent
{
    [Header("Target & Body Parts")]
    public GameObject target;
    public Rigidbody spine1Rb;
    public ConfigurableJoint hipL, calfL, hipR, calfR, spine2, shoulderL, shoulderR, handL, handR;
    public Collider headCol;
    public Transform footL, footR;
    public List<GameObject> obstacleList;

    [Header("Movement Settings")]
    public float angleSmooth = 0.2f;

    [Header("Stagnation Settings")]
    public float stagnationTime = 1.5f;
    public float stagnationMinDist = 1f;

    [Header("GUI Settings")]
    public int agentIndex = 0;

    [Header("Cinematic Settings (J Key Toggle)")]
    public CinemachineCamera vCam;      // 인스펙터에서 시네머신 가상 카메라 연결
    public float zoomFOV = 15f;         // 줌인 목표 화각 (낮을수록 가까움)
    public float slowMoScale = 0.2f;    // 슬로우 배속 (0.2 = 5배 느림)
    public float zoomSpeed = 5f;        // 줌 부드러움 (높을수록 빠름)

    // 내부 상태 변수 (보상 및 디스플레이)
    private float[] curActions = new float[12];
    private float m_PreviousDistance, m_StartingZ;
    private float m_RewardDist, m_RewardUpright, m_RewardFace, m_RewardSide, m_RewardJump, m_RewardTotal;
    private float m_DispDist, m_DispUpright, m_DispFace, m_DispSide, m_DispJump, m_DispTotal, m_DispVel, m_DispActualDist;
    private float m_DispHeightDiff;
    private float m_DispCumulative;
    private int m_DispObstacleIndex;
    private float m_DispObstacleDist;
    private float m_GuiTimer;
    private const float GUI_UPDATE_INTERVAL = 0.3f;
    private float m_ObsRelX, m_ObsRelZ, m_ObsH;

    // 시네마틱 상태 관리 변수
    private float m_OriginalFOV;
    private Transform m_OriginalFollow;
    private Transform m_OriginalLookAt;
    private bool m_IsCinematicActive = false;
    private Coroutine m_CinematicCoroutine;

    struct RBInit { public Rigidbody rb; public Vector3 pos; public Quaternion rot; }
    List<RBInit> rbInits = new List<RBInit>();
    List<Rigidbody> bodyParts = new List<Rigidbody>();
    private bool isHeadTouching;
    private Transform targetTf;
    private int m_ObstacleIndex = 0;
    private float m_StagnationTimer;
    private float m_StagnationCheckX;
    private float m_DispStagnationTimer;

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

            var reporter = rb.gameObject.GetComponent<BodyPartCollisionReporter>();
            if (reporter == null) reporter = rb.gameObject.AddComponent<BodyPartCollisionReporter>();
            reporter.agent = this;
        }

        // 초기 카메라 상태 저장
        if (vCam != null)
        {
            m_OriginalFOV = vCam.Lens.FieldOfView;
            m_OriginalFollow = vCam.Follow;
            m_OriginalLookAt = vCam.LookAt;
        }

        // 자체 충돌 무시
        for (int i = 0; i < allRbs.Length; i++)
        {
            for (int j = i + 1; j < allRbs.Length; j++)
            {
                Collider colA = allRbs[i].GetComponent<Collider>();
                Collider colB = allRbs[j].GetComponent<Collider>();
                if (colA != null && colB != null) Physics.IgnoreCollision(colA, colB);
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        // 에피소드 시작 시 시네마틱 효과 강제 종료 및 복구
        StopCinematicImmediate();

        foreach (var s in rbInits)
        {
            s.rb.position = s.pos; s.rb.rotation = s.rot;
            s.rb.linearVelocity = Vector3.zero; s.rb.angularVelocity = Vector3.zero;
            s.rb.Sleep(); s.rb.WakeUp();
        }
        for (int i = 0; i < 12; i++) curActions[i] = 0f;
        isHeadTouching = false;
        m_ObstacleIndex = 0;
        m_StartingZ = spine1Rb.position.z;
        if (targetTf != null) m_PreviousDistance = Vector3.Distance(spine1Rb.position, targetTf.position);
        m_StagnationTimer = 0f;
        m_StagnationCheckX = spine1Rb.position.x;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toTarget = (targetTf != null) ? targetTf.position - spine1Rb.position : Vector3.zero;
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

        // if (obstacleList != null && m_ObstacleIndex < obstacleList.Count)
        // {
        //     GameObject curObs = obstacleList[m_ObstacleIndex];
        //     Vector3 relPos = transform.InverseTransformPoint(curObs.transform.position);
        //     sensor.AddObservation(-relPos.x);
        //     sensor.AddObservation(relPos.y);
        //     sensor.AddObservation(curObs.transform.localScale.y); sensor.AddObservation(curObs.transform.localScale.x);
        //     m_ObsRelX = relPos.x; m_ObsRelZ = relPos.z; m_ObsH = curObs.transform.localScale.y;
        // }
        // else
        {
            sensor.AddObservation(50f); sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f);
            m_ObsRelX = 50f; m_ObsRelZ = 0f; m_ObsH = 0f;
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        for (int i = 0; i < 12; i++)
            curActions[i] = Mathf.Lerp(curActions[i], Mathf.Clamp(a[i], -1f, 1f), angleSmooth);

        SetJointRotation(hipL, Map(curActions[0], -20f, 60f), Map(curActions[1], -20f, 20f), 0);
        SetJointRotation(hipR, Map(curActions[2], -20f, 60f), Map(curActions[3], -20f, 20f), 0);
        SetJointRotation(spine2, Map(curActions[4], -20f, 20f), Map(curActions[5], -10f, 10f), 0);
        SetJointRotation(calfL, Map(curActions[6], -80f, 0f), 0, 0);
        SetJointRotation(calfR, Map(curActions[7], -80f, 0f), 0, 0);
        SetJointRotation(shoulderL, Map(curActions[8], -10f, 70f), 0, 0);
        SetJointRotation(shoulderR, Map(curActions[9], -10f, 70f), 0, 0);

        float velForward = Vector3.Dot(spine1Rb.linearVelocity, spine1Rb.transform.forward);
        m_RewardDist = velForward * 0.01f;
        float upDot = Vector3.Dot(spine1Rb.transform.up, Vector3.up);
        m_RewardUpright = (upDot > 0.8f) ? 0.001f : 0f;
        float faceDot = Vector3.Dot(spine1Rb.transform.forward, Vector3.left);
        m_RewardFace = faceDot >= 0.94f ? faceDot * 0.001f : -0.005f;
        float sideError = Mathf.Abs(spine1Rb.position.z - m_StartingZ);
        m_RewardSide = -sideError * 0.01f;

        m_RewardTotal = m_RewardDist + m_RewardUpright + m_RewardFace + m_RewardSide;
        AddReward(m_RewardTotal);

        if (upDot < 0.3f || isHeadTouching)
        {
            SetReward(-30.0f);
            EndEpisode();
        }

        m_StagnationTimer += Time.fixedDeltaTime;
        float movedX = Mathf.Abs(spine1Rb.position.x - m_StagnationCheckX);
        if (movedX >= stagnationMinDist)
        {
            m_StagnationTimer = 0f;
            m_StagnationCheckX = spine1Rb.position.x;
        }
        else if (m_StagnationTimer >= stagnationTime)
        {
            SetReward(-10f);
            EndEpisode();
        }

        // if (targetTf != null && spine1Rb.position.x <= targetTf.position.x)
        // {
        //     AddReward(50f);
        //     EndEpisode();
        // }

        if (obstacleList != null && m_ObstacleIndex < obstacleList.Count)
        {
            if (spine1Rb.position.x - obstacleList[m_ObstacleIndex].transform.position.x < -0.6f)
                m_ObstacleIndex++;


            if (m_DispObstacleDist < 3.5f && m_DispObstacleDist > -0.5f)
            {
                float jumpThreshold = 0.5f;

                if (m_DispHeightDiff > jumpThreshold)
                {
                    float jumpConstant = 0.1f;
                    m_RewardJump = m_DispHeightDiff * upDot * jumpConstant;
                    AddReward(m_RewardJump);
                }

            }
            else
            {
                m_RewardJump = 0f;
            }

        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T)) EndEpisode();

        // J 키 토글 기능
        if (Input.GetKeyDown(KeyCode.J))
        {
            if (m_IsCinematicActive) StopCinematicWithSmoothReturn();
            else m_CinematicCoroutine = StartCoroutine(CinematicImpactRoutine());
        }

        Vector3 pos = spine1Rb.position;
        Debug.DrawRay(pos, spine1Rb.linearVelocity, Color.cyan);
        Debug.DrawRay(pos, spine1Rb.transform.forward * 2f, Color.yellow);
        Debug.DrawRay(pos, Vector3.right * 2f, Color.red);

        m_GuiTimer += Time.deltaTime;
        if (m_GuiTimer >= GUI_UPDATE_INTERVAL)
        {
            m_DispDist = m_RewardDist; m_DispUpright = m_RewardUpright; m_DispFace = m_RewardFace;
            m_DispSide = m_RewardSide; m_DispJump = m_RewardJump; m_DispTotal = m_RewardTotal;
            if (obstacleList != null && m_ObstacleIndex < obstacleList.Count)
                m_DispHeightDiff = spine1Rb.position.y - obstacleList[m_ObstacleIndex].transform.position.y + 1;
            else
                m_DispHeightDiff = float.NaN;
            m_DispVel = spine1Rb.linearVelocity.magnitude;
            m_DispActualDist = (targetTf != null) ? Vector3.Distance(spine1Rb.position, targetTf.position) : 0;
            m_DispCumulative = GetCumulativeReward();
            m_DispStagnationTimer = m_StagnationTimer;
            m_DispObstacleIndex = m_ObstacleIndex;
            if (obstacleList != null && m_ObstacleIndex < obstacleList.Count)
                m_DispObstacleDist = spine1Rb.position.x - obstacleList[m_ObstacleIndex].transform.position.x;
            else
                m_DispObstacleDist = float.NaN;
            m_GuiTimer = 0f;
        }
    }

    void OnGUI()
    {
        int w = 340, lineH = 36, pad = 8;
        int x = agentIndex == 0 ? 10 : Screen.width - w - 10;
        int y = 10;

        string obstacleLabel = obstacleList != null && m_DispObstacleIndex < obstacleList.Count
            ? $"Obstacle  {m_DispObstacleIndex + 1}/{obstacleList.Count}"
            : "Obstacle  -/-";
        string obstacleDistStr = float.IsNaN(m_DispObstacleDist) ? "   done" : $"{m_DispObstacleDist:+0.00;-0.00} m";

        string heightDiffStr = float.IsNaN(m_DispHeightDiff) ? "   -" : $"{m_DispHeightDiff:+0.00;-0.00} m";

        string stagnationStr = $"{m_DispStagnationTimer:0.0} / {stagnationTime:0.0} s";
        Color stagnationColor = m_DispStagnationTimer > stagnationTime * 0.7f ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.75f, 0.3f);

        string[] labels = { "Velocity", "Upright", "Facing", "Side", "───────────", "Step Total", "Cumulative", "───────────", obstacleLabel, "Obs Dist", "Height Diff", "Jump Reward", "───────────", "Stagnation" };
        float[] values = { m_DispDist, m_DispUpright, m_DispFace, m_DispSide, float.NaN, m_DispTotal, m_DispCumulative, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN };
        string[] overrides = { null, null, null, null, null, null, null, null, "", obstacleDistStr, heightDiffStr, $"{m_DispJump:+0.00000;-0.00000}", null, stagnationStr };
        Color[] colors = {
            new Color(0.4f, 1f, 0.4f), new Color(0.4f, 0.8f, 1f),
            new Color(1f, 1f, 0.4f), new Color(1f, 0.6f, 0.4f),
            Color.gray,
            Color.white, new Color(1f, 0.9f, 0.3f),
            Color.gray,
            new Color(0.8f, 0.6f, 1f), new Color(0.8f, 0.6f, 1f),
            new Color(0.4f, 1f, 0.9f), new Color(0.4f, 1f, 0.9f),
            Color.gray,
            stagnationColor
        };

        string header = $"Agent {agentIndex}";
        int rows = labels.Length;
        float bgH = rows * lineH + lineH + pad * 3;

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(x - 6, y - 6, w + 12, bgH + 12), Texture2D.whiteTexture);

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Normal };
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Normal };

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y + pad, w, lineH), header, headerStyle);

        for (int i = 0; i < rows; i++)
        {
            GUI.color = colors[i];
            float ry = y + pad + lineH + i * lineH;
            string valStr = overrides[i] ?? (float.IsNaN(values[i]) ? null : $"{values[i]:+0.00000;-0.00000}");
            if (valStr == null)
            {
                GUI.Label(new Rect(x, ry, w, lineH), labels[i], style);
            }
            else
            {
                GUI.Label(new Rect(x, ry, 170, lineH), labels[i], style);
                GUI.Label(new Rect(x + 170, ry, 170, lineH), valStr, style);
            }
        }
        GUI.color = Color.white;
    }

    // --- 시네마틱 로직 섹션 ---

    IEnumerator CinematicImpactRoutine()
    {
        m_IsCinematicActive = true;

        if (vCam != null && headCol != null)
        {
            // 타겟을 머리로 즉시 고정
            vCam.Follow = headCol.transform;
            vCam.LookAt = headCol.transform;

            // 슬로우 모션 적용
            Time.timeScale = slowMoScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            Debug.Log("<color=cyan>[CINEMATIC] 줌인 + 슬로우 모션 활성화!</color>");

            // 부드러운 줌인 (Time.unscaledDeltaTime 사용으로 슬로우 영향 안받음)
            while (vCam.Lens.FieldOfView > zoomFOV + 0.1f)
            {
                vCam.Lens.FieldOfView = Mathf.Lerp(vCam.Lens.FieldOfView, zoomFOV, Time.unscaledDeltaTime * zoomSpeed);
                yield return null;
            }
            vCam.Lens.FieldOfView = zoomFOV;
        }
    }

    void StopCinematicWithSmoothReturn()
    {
        if (!m_IsCinematicActive) return;
        if (m_CinematicCoroutine != null) StopCoroutine(m_CinematicCoroutine);
        StartCoroutine(ResetCameraRoutine());
    }

    IEnumerator ResetCameraRoutine()
    {
        Debug.Log("<color=white>[CINEMATIC] 부드러운 복귀 중...</color>");

        // 시간 속도 정상화
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        // 부드러운 줌 아웃
        while (vCam.Lens.FieldOfView < m_OriginalFOV - 0.1f)
        {
            vCam.Lens.FieldOfView = Mathf.Lerp(vCam.Lens.FieldOfView, m_OriginalFOV, Time.deltaTime * zoomSpeed);
            yield return null;
        }

        vCam.Lens.FieldOfView = m_OriginalFOV;
        vCam.Follow = m_OriginalFollow;
        vCam.LookAt = m_OriginalLookAt;
        m_IsCinematicActive = false;
    }

    void StopCinematicImmediate()
    {
        if (!m_IsCinematicActive) return;

        if (m_CinematicCoroutine != null) StopCoroutine(m_CinematicCoroutine);

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        if (vCam != null)
        {
            vCam.Lens.FieldOfView = m_OriginalFOV;
            vCam.Follow = m_OriginalFollow;
            vCam.LookAt = m_OriginalLookAt;
        }
        m_IsCinematicActive = false;
    }

    // --- 기타 헬퍼 함수 ---
    float Map(float val, float min, float max) => val >= 0 ? val * max : val * Mathf.Abs(min);
    void SetJointRotation(ConfigurableJoint j, float x, float y, float z) { if (j != null) j.targetRotation = Quaternion.Euler(x, y, z); }
    public void OnBodyPartHitHurdle(string bodyPartName)
    {
        Debug.Log($"[Agent {agentIndex}] {bodyPartName} hit hurdle!");
        AddReward(-30f);
        EndEpisode();
    }

    public void OnBodyPartHitObstacle()
    {
        SetReward(-30f);
        // EndEpisode();
    }

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