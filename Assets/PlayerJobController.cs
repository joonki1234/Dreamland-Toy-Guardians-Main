using UnityEngine;

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

    [Header("건설자(Builder) 흙 생성 세팅")]
    public GameObject dirtPrefab;        // 방금 만든 DirtBlock 프리팹 연결용
    public Transform shovelFirePoint;    // Weapon_Builder 하위의 FirePoint 연결용
    public float throwForce = 5f;        // 흙이 튀어나가는 힘

    private void Start()
    {
        // 1. 하이얼라키에서 현재 켜져 있는 모델 감지
        DetectActiveJobFromHierarchy();

        // 2. 해당 직업의 모델과 무기 세팅 적용
        ApplyJobSettings(currentJob);
    }

    private void Update()
    {
        // 마우스 좌클릭 시 공격
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
        switch (currentJob)
        {
            case PlayerJob.Police:
                Debug.Log("경찰: Sci-Fi Pistol 발사!");
                break;

            case PlayerJob.Firefighter:
                Debug.Log("소방관: LaserBeam 발사!");
                break;

            case PlayerJob.Chef:
                Debug.Log("요리사: 뒤집기 공격!");
                break;

            case PlayerJob.Builder:
                Debug.Log("건설자: 삽질/흙 퍼기 공격!");
                SpawnDirt(); // 흙 덩어리 생성 함수 호출
                break;
        }
    }

    // 흙 덩어리를 생성하고 앞으로 튕겨내는 함수
    private void SpawnDirt()
    {
        if (dirtPrefab != null && shovelFirePoint != null)
        {
            // FirePoint 위치에 흙 프리팹 복사 생성
            GameObject dirt = Instantiate(dirtPrefab, shovelFirePoint.position, shovelFirePoint.rotation);

            // 생성된 흙에 물리적 힘을 가해 전방+위쪽으로 살짝 날려줌
            Rigidbody dirtRb = dirt.GetComponent<Rigidbody>();
            if (dirtRb != null)
            {
                dirtRb.AddForce(shovelFirePoint.forward * throwForce + Vector3.up * 2f, ForceMode.Impulse);
            }
        }
        else
        {
            Debug.LogWarning("Builder 흙 생성 실패: Dirt Prefab 또는 Shovel FirePoint가 인스펙터에 연결되지 않았습니다!");
        }
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