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

    private void Start()
    {
        // 1. 게임 시작 시 하이얼라키에서 현재 켜져 있는(activeSelf) 모델을 감지해서 currentJob에 반영합니다.
        DetectActiveJobFromHierarchy();

        // 2. 해당 직업의 모델과 무기를 완벽하게 세팅합니다.
        ApplyJobSettings(currentJob);
    }

    private void Update()
    {
        // 공격 키 입력 (마우스 좌클릭)
        if (Input.GetButtonDown("Fire1"))
        {
            Attack();
        }
    }

    // 하이얼라키에 켜져 있는 모델링을 자동으로 찾아내는 함수
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
                break;
        }
    }

    // 외부(UI/로비)나 시작 시 직업을 적용해주는 함수
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