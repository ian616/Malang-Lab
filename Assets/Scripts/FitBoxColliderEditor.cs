using UnityEngine;
using UnityEditor;

public class FitBoxColliderEditor : Editor
{
    [MenuItem("Tools/Add Fitted Box Colliders")]
    static void AddFittedBoxColliders()
    {
        // 현재 선택된 모든 게임 오브젝트를 가져옴
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("오브젝트를 먼저 선택해주세요!");
            return;
        }

        foreach (GameObject obj in selectedObjects)
        {
            // 1. 이미 박스 콜라이더가 있다면 제거 (중복 방지, 원치 않으면 이 줄 삭제)
            BoxCollider oldCollider = obj.GetComponent<BoxCollider>();
            if (oldCollider != null) Undo.DestroyObjectImmediate(oldCollider);

            // 2. 렌더러 확인 (크기 계산용)
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"{obj.name}에 MeshRenderer가 없어 스킵합니다.");
                continue;
            }

            // 3. 박스 콜라이더 추가 및 언두(Undo) 등록
            BoxCollider newCollider = Undo.AddComponent<BoxCollider>(obj);

            // 4. 로컬 바운드 기준으로 크기 맞추기
            // 로컬 좌표계를 기준으로 계산해야 정확하게 들어갑니다.
            MeshFilter filter = obj.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                newCollider.center = filter.sharedMesh.bounds.center;
                newCollider.size = filter.sharedMesh.bounds.size;
            }
            else
            {
                // MeshFilter가 없는 경우 렌더러 바운드 사용 (약간 부정확할 수 있음)
                newCollider.center = renderer.localBounds.center;
                newCollider.size = renderer.localBounds.size;
            }
        }

        Debug.Log($"{selectedObjects.Length}개의 오브젝트에 맞춤형 콜라이더 추가 완료!");
    }
}