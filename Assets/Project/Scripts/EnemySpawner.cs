using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Settings")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnSpacing = 1.2f;
    [SerializeField] private float spawnInterval = 0.35f;

    private bool isSpawning;

    public void SpawnEnemy()
    {
        SpawnWave(1);
    }

    public void SpawnWave(int enemyCount)
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("Enemy Prefab이 연결되지 않았습니다.");
            return;
        }

        if (enemyCount <= 0)
        {
            Debug.LogWarning("생성할 적의 수는 1마리 이상이어야 합니다.");
            return;
        }

        if (isSpawning)
        {
            Debug.LogWarning("이미 적을 생성하고 있습니다.");
            return;
        }

        StartCoroutine(SpawnWaveRoutine(enemyCount));
    }

    private IEnumerator SpawnWaveRoutine(int enemyCount)
    {
        isSpawning = true;

        // 적 무리의 중심이 EnemySpawnPoint에 오도록 시작 위치 계산
        float startOffset = -(enemyCount - 1) * spawnSpacing * 0.5f;

        for (int i = 0; i < enemyCount; i++)
        {
            float currentOffset = startOffset + i * spawnSpacing;

            Vector3 spawnPosition =
                transform.position + transform.right * currentOffset;

            Instantiate(
                enemyPrefab,
                spawnPosition,
                transform.rotation
            );

            Debug.Log($"적 생성: {i + 1} / {enemyCount}");

            yield return new WaitForSeconds(spawnInterval);
        }

        isSpawning = false;

        Debug.Log($"적 {enemyCount}마리 생성 완료");
    }

    [ContextMenu("Test Spawn 1 Enemy")]
    private void TestSpawnOneEnemy()
    {
        SpawnWave(1);
    }

    [ContextMenu("Test Spawn 3 Enemies")]
    private void TestSpawnThreeEnemies()
    {
        SpawnWave(3);
    }

    [ContextMenu("Test Spawn 6 Enemies")]
    private void TestSpawnSixEnemies()
    {
        SpawnWave(6);
    }
}