using UnityEngine;

public class TagAssigner : MonoBehaviour
{
    public string targetTag = "Agent";

    [ContextMenu("Set Tags Recursively")]
    public void SetTags()
    {
        // 자기 자신을 포함하여 모든 자식의 Transform을 가져옴 (비활성 객체 포함)
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            child.gameObject.tag = targetTag;
        }

        Debug.Log($"{gameObject.name}을 포함한 {allChildren.Length}개 오브젝트의 태그를 '{targetTag}'로 변경했습니다.");
    }
}