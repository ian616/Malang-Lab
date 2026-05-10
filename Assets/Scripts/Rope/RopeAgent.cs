using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class RopeAgent : Agent
{
    [Header("Target & Body Parts")]
    public GameObject target;
    public Rigidbody spine1Rb;
    public ConfigurableJoint hipL, calfL, hipR, calfR, spine2, shoulderL, shoulderR, handL, handR;
    public Collider headCol;
    public Transform footL, footR;

    [Header("Movement Settings")]
    public float angleSmooth = 0.2f;
    public float fallThreshold = 5f;

    [Header("Rope Observation")]
    public int observeSegmentCount = 5;

    private float[] curActions = new float[12];
    private bool isHeadTouching;
    private Transform targetTf;
    private float m_UpDot;
    private float m_StepReward;
    private float m_StartingY;
    private int m_NearestIdx = 0;

    struct RBInit { public Rigidbody rb; public Vector3 pos; public Quaternion rot; }
    List<RBInit> rbInits = new List<RBInit>();
    List<Rigidbody> bodyParts = new List<Rigidbody>();
    List<GameObject> m_Segments = new List<GameObject>();

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
            for (int j = i + 1; j < allRbs.Length; j++)
            {
                Collider colA = allRbs[i].GetComponent<Collider>();
                Collider colB = allRbs[j].GetComponent<Collider>();
                if (colA != null && colB != null) Physics.IgnoreCollision(colA, colB);
            }

        m_Segments.Clear();
        m_Segments.AddRange(GameObject.FindGameObjectsWithTag("RopeSegment"));
        m_Segments.Sort((a, b) => int.Parse(a.name).CompareTo(int.Parse(b.name)));
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

        int skip = 30;
        if (m_Segments.Count > skip * 2)
        {
            int idx = Random.Range(skip, m_Segments.Count - skip);
            Vector3 spawnPos = m_Segments[idx].transform.position + Vector3.up * 1.0f;
            Vector3 offset = spawnPos - spine1Rb.position;
            foreach (var s in rbInits)
                s.rb.position += offset;
        }

        m_StartingY = spine1Rb.position.y;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toTarget = (targetTf != null) ? targetTf.position - spine1Rb.position : Vector3.zero;
        toTarget.y = 0;
        sensor.AddObservation(transform.InverseTransformDirection(toTarget.normalized));
        sensor.AddObservation(toTarget.magnitude * 0.01f);
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

        ObserveNearestRopeSegments(sensor);
    }

    private void ObserveNearestRopeSegments(VectorSensor sensor)
    {
        // 가장 가까운 세그먼트 인덱스 탐색 (정렬 없이 선형 탐색)
        m_NearestIdx = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < m_Segments.Count; i++)
        {
            float d = Vector3.Distance(spine1Rb.position, m_Segments[i].transform.position);
            if (d < minDist) { minDist = d; m_NearestIdx = i; }
        }

        // m_NearestIdx 기준으로 앞뒤 윈도우 [N - half ... N + half]
        int half = observeSegmentCount / 2;
        for (int i = 0; i < observeSegmentCount; i++)
        {
            int idx = m_NearestIdx - half + i;
            if (idx >= 0 && idx < m_Segments.Count)
            {
                Transform seg = m_Segments[idx].transform;
                Rigidbody segRb = seg.GetComponent<Rigidbody>();
                sensor.AddObservation(transform.InverseTransformPoint(seg.position));
                sensor.AddObservation(seg.localRotation);
                sensor.AddObservation(transform.InverseTransformDirection(segRb.linearVelocity));
                sensor.AddObservation(transform.InverseTransformDirection(segRb.angularVelocity));
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
                sensor.AddObservation(Quaternion.identity);
                sensor.AddObservation(Vector3.zero);
                sensor.AddObservation(Vector3.zero);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        for (int i = 0; i < 12; i++)
            curActions[i] = Mathf.Lerp(curActions[i], Mathf.Clamp(a[i], -1f, 1f), angleSmooth);

        SetJointRotation(hipL,      Map(curActions[0], -20f, 60f),  Map(curActions[1], -20f, 20f), 0);
        SetJointRotation(hipR,      Map(curActions[2], -20f, 60f),  Map(curActions[3], -20f, 20f), 0);
        SetJointRotation(spine2,    Map(curActions[4], -20f, 20f),  Map(curActions[5], -10f, 10f), 0);
        SetJointRotation(calfL,     Map(curActions[6], -80f, 0f),   0, 0);
        SetJointRotation(calfR,     Map(curActions[7], -80f, 0f),   0, 0);
        SetJointRotation(shoulderL, Map(curActions[8], -10f, 70f),  0, 0);
        SetJointRotation(shoulderR, Map(curActions[9], -10f, 70f),  0, 0);

        m_UpDot = Vector3.Dot(spine1Rb.transform.up, Vector3.up);
        m_StepReward = m_UpDot * 0.001f;
        AddReward(m_StepReward);

        if (m_UpDot < 0.3f || spine1Rb.position.y < m_StartingY - fallThreshold)
        {
            SetReward(-5f);
            EndEpisode();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T)) EndEpisode();

        Color rayColor = Color.Lerp(Color.red, Color.green, m_UpDot);
        Debug.DrawRay(spine1Rb.position, spine1Rb.transform.up * 2f, rayColor);

    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 24 };
        int x = 10, y = 10, lineH = 30;

        GUI.color = Color.Lerp(Color.red, Color.green, m_UpDot);
        GUI.Label(new Rect(x, y, 300, lineH), $"UpDot       {m_UpDot:0.000}", style);

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y + lineH, 300, lineH), $"Reward      {m_StepReward:+0.00000;-0.00000}", style);
        GUI.Label(new Rect(x, y + lineH * 2, 300, lineH), $"Cumulative  {GetCumulativeReward():+0.000;-0.000}", style);

        GUI.color = Color.white;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ground"))
            foreach (var contact in collision.contacts)
                if (contact.thisCollider == headCol) { isHeadTouching = true; break; }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Ground")) isHeadTouching = false;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        float v = -Input.GetAxis("Vertical");
        float t = Time.time * 11f;
        for (int i = 0; i < 12; i++) ca[i] = 0f;
        if (v != 0)
        {
            ca[0] = Mathf.Sin(t) * 1.8f * v;                ca[2] = Mathf.Sin(t + Mathf.PI) * 1.8f * v;
            ca[1] = -Mathf.Max(0, Mathf.Cos(t)) * 4.5f * v; ca[3] = -Mathf.Max(0, Mathf.Cos(t + Mathf.PI)) * 4.5f * v;
            ca[6] = -Mathf.Max(0, Mathf.Sin(t + 0.5f)) * 3.5f * v; ca[7] = -Mathf.Max(0, Mathf.Sin(t + Mathf.PI + 0.5f)) * 3.5f * v;
            ca[4] = Mathf.Cos(t) * 0.7f * v;                ca[5] = 1.0f * v;
            ca[8] = Mathf.Sin(t + Mathf.PI) * 1.5f;         ca[9] = Mathf.Sin(t) * 1.5f;
            ca[10] = Mathf.Sin(t + Mathf.PI) * 1.0f;        ca[11] = Mathf.Sin(t) * 1.0f;
        }
    }

    float Map(float val, float min, float max) => val >= 0 ? val * max : val * Mathf.Abs(min);
    void SetJointRotation(ConfigurableJoint j, float x, float y, float z) { if (j != null) j.targetRotation = Quaternion.Euler(x, y, z); }
}
