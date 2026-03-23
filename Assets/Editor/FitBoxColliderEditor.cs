using UnityEngine;
using UnityEditor;

public class FitBoxColliderEditor : Editor
{
    [MenuItem("Tools/Add Mesh Colliders")]
    static void AddMeshColliders()
    {
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("오브젝트를 먼저 선택해주세요!");
            return;
        }

        foreach (GameObject obj in selectedObjects)
        {
            // 기존 BoxCollider 제거
            BoxCollider oldCollider = obj.GetComponent<BoxCollider>();
            if (oldCollider != null)
                Undo.DestroyObjectImmediate(oldCollider);

            // MeshFilter 확인
            MeshFilter filter = obj.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogWarning($"{obj.name}에 MeshFilter 또는 Mesh가 없어 스킵합니다.");
                continue;
            }

            // MeshCollider 추가
            MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(obj);
            meshCollider.sharedMesh = filter.sharedMesh;
        }

        Debug.Log($"{selectedObjects.Length}개의 오브젝트에 MeshCollider 추가 완료!");
    }
}