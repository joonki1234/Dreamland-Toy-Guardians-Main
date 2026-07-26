using UnityEngine;

public class DirtProjectile : MonoBehaviour
{
    [Header("충돌 시 생성할 진흙 바닥 프리팹 (MudSplat)")]
    public GameObject mudSplatPrefab;

    [Header("진흙 자국 유지 시간 (초)")]
    public float destroyDelay = 10f;

    private void OnCollisionEnter(Collision collision)
    {
        // 1. 충돌 지점 정보 받아오기
        ContactPoint contact = collision.contacts[0];

        // 2. 바닥 수평에 맞춰 Quad(네모판)를 완전히 바닥에 눕히는 회전 계산
        // LookRotation으로 표면 방향을 잡고, Euler(90, 0, 0)을 곱해 직각으로 눕혀줍니다.
        Quaternion splatRotation = Quaternion.LookRotation(contact.normal) * Quaternion.Euler(90, 0, 0);

        // 3. 진흙 자국 생성
        if (mudSplatPrefab != null)
        {
            // 바닥과 자국 메쉬가 완전히 겹치면 깜빡거리는 현상(Z-Fighting)이 나므로
            // 바닥 법선(Normal) 방향으로 0.005m 아주 살짝 띄워서 생성합니다.
            Vector3 spawnPos = contact.point + (contact.normal * 0.005f);

            GameObject splat = Instantiate(mudSplatPrefab, spawnPos, splatRotation);

            // 지정된 시간 뒤 진흙 자국 삭제
            Destroy(splat, destroyDelay);
        }

        // 4. [중요] DestroyImmediate 대신 안전한 Destroy 사용! (에러 해결)
        Destroy(gameObject);
    }
}