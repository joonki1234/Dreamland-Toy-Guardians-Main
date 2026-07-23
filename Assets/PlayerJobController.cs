using UnityEngine;
using System.Collections;

public enum PlayerJob
{
    Police,
    Firefighter,
    Chef,
    Builder
}

public class PlayerJobController : MonoBehaviour
{
    [Header("현재 선택된 직업")]
    public PlayerJob currentJob = PlayerJob.Police;

    [Header("직업별 모델링 (Models)")]
    public GameObject modelPolice;
    public GameObject modelFirefighter;
    public GameObject modelChef;
    public GameObject modelBuilder;

    [Header("직업별 무기 (Camera 자식들)")]
    public GameObject weaponPolice;
    public GameObject weaponFirefighter;
    public GameObject weaponChef;
    public GameObject weaponBuilder;

    [Header("건설자(Builder) 흙 발사 세팅")]
    public GameObject dirtPrefab;             // 흙 클러스터 메인 프리팹
    public Transform shovelFirePoint;         // FirePoint 위치
    public ParticleSystem dirtParticleSystem; // 흙 먼지/빛 파티클
    public Light dirtFlashLight;              // 흙 빛(섬광) 조명
    public float throwForce = 32f;            // 먼 거리로 발사하는 강한 힘
    public float builderCooldown = 0.5f;      // 쿨타임 (0.5초)

    private float lastAttackTime = -999f;
    private bool isSwinging = false;

    private void Start()
    {
        DetectActiveJobFromHierarchy();
        ApplyJobSettings(currentJob);
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            Attack();
        }
    }

    private void DetectActiveJobFromHierarchy()
    {
        if (modelPolice != null && modelPolice.activeSelf) currentJob = PlayerJob.Police;
        else if (modelFirefighter != null && modelFirefighter.activeSelf) currentJob = PlayerJob.Firefighter;
        else if (modelChef != null && modelChef.activeSelf) currentJob = PlayerJob.Chef;
        else if (modelBuilder != null && modelBuilder.activeSelf) currentJob = PlayerJob.Builder;
    }

    public void Attack()
    {
        if (Time.time < lastAttackTime + builderCooldown) return;

        switch (currentJob)
        {
            case PlayerJob.Police:
                lastAttackTime = Time.time;
                break;

            case PlayerJob.Firefighter:
                lastAttackTime = Time.time;
                break;

            case PlayerJob.Chef:
                lastAttackTime = Time.time;
                break;

            case PlayerJob.Builder:
                lastAttackTime = Time.time;
                if (weaponBuilder != null && !isSwinging)
                {
                    StartCoroutine(ShovelScoopRoutine());
                }
                break;
        }
    }

    private IEnumerator ShovelScoopRoutine()
    {
        isSwinging = true;

        Transform targetTransform = weaponBuilder.transform;
        Transform shovelChild = weaponBuilder.transform.Find("Shovel_001");
        if (shovelChild != null) targetTransform = shovelChild;

        Vector3 origPos = targetTransform.localPosition;
        Vector3 origEuler = targetTransform.localEulerAngles;

        Vector3 downPos = origPos + new Vector3(0f, -0.2f, -0.1f);
        Vector3 downEuler = origEuler + new Vector3(35f, 0f, 0f);

        Vector3 upPos = origPos + new Vector3(0f, 0.2f, 0.15f);
        Vector3 upEuler = origEuler + new Vector3(-25f, 0f, 0f);

        float elapsed = 0f;
        float durationDownToUp = 0.14f;
        bool hasFired = false;

        while (elapsed < durationDownToUp)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / durationDownToUp;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            targetTransform.localPosition = Vector3.Lerp(downPos, upPos, smoothT);
            targetTransform.localEulerAngles = new Vector3(
                Mathf.LerpAngle(downEuler.x, upEuler.x, smoothT),
                origEuler.y,
                origEuler.z
            );

            if (!hasFired && t >= 0.65f)
            {
                SpawnDirtCluster();
                hasFired = true;
            }

            yield return null;
        }

        if (!hasFired) SpawnDirtCluster();

        elapsed = 0f;
        float durationReturn = 0.16f;

        while (elapsed < durationReturn)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / durationReturn;

            targetTransform.localPosition = Vector3.Lerp(upPos, origPos, t);
            targetTransform.localEulerAngles = new Vector3(
                Mathf.LerpAngle(upEuler.x, origEuler.x, t),
                origEuler.y,
                origEuler.z
            );

            yield return null;
        }

        targetTransform.localPosition = origPos;
        targetTransform.localEulerAngles = origEuler;

        isSwinging = false;
    }

    // 메인 탄환 1개를 쏘아 보내는 함수 (공중에서 폭죽처럼 분열하는 탄환)
    private void SpawnDirtCluster()
    {
        if (dirtParticleSystem != null) dirtParticleSystem.Play();
        if (dirtFlashLight != null) StartCoroutine(FlashDirtLight());

        if (dirtPrefab != null && shovelFirePoint != null)
        {
            GameObject mainDirt = Instantiate(dirtPrefab, shovelFirePoint.position, shovelFirePoint.rotation);

            Rigidbody dirtRb = mainDirt.GetComponent<Rigidbody>();
            if (dirtRb != null)
            {
                Vector3 throwDirection = shovelFirePoint.forward * throwForce + Vector3.up * 4f;
                dirtRb.AddForce(throwDirection, ForceMode.Impulse);
            }
        }
    }

    private IEnumerator FlashDirtLight()
    {
        dirtFlashLight.enabled = true;
        yield return new WaitForSeconds(0.1f);
        dirtFlashLight.enabled = false;
    }

    public void ApplyJobSettings(PlayerJob job)
    {
        currentJob = job;
        DisableAllObjects();

        switch (currentJob)
        {
            case PlayerJob.Police:
                if (modelPolice) modelPolice.SetActive(true);
                if (weaponPolice) weaponPolice.SetActive(true);
                break;
            case PlayerJob.Firefighter:
                if (modelFirefighter) modelFirefighter.SetActive(true);
                if (weaponFirefighter) weaponFirefighter.SetActive(true);
                break;
            case PlayerJob.Chef:
                if (modelChef) modelChef.SetActive(true);
                if (weaponChef) weaponChef.SetActive(true);
                break;
            case PlayerJob.Builder:
                if (modelBuilder) modelBuilder.SetActive(true);
                if (weaponBuilder) weaponBuilder.SetActive(true);
                break;
        }
    }

    private void DisableAllObjects()
    {
        if (modelPolice) modelPolice.SetActive(false);
        if (modelFirefighter) modelFirefighter.SetActive(false);
        if (modelChef) modelChef.SetActive(false);
        if (modelBuilder) modelBuilder.SetActive(false);

        if (weaponPolice) weaponPolice.SetActive(false);
        if (weaponFirefighter) weaponFirefighter.SetActive(false);
        if (weaponChef) weaponChef.SetActive(false);
        if (weaponBuilder) weaponBuilder.SetActive(false);
    }
}