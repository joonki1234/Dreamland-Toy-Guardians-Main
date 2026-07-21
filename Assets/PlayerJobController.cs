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
        ApplyJobSettings(currentJob);
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            Attack();
        }
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
                Debug.Log("요리사: 뒤집기!");
                break;
            case PlayerJob.Builder:
                Debug.Log("건설자: 흙 퍼기!");
                break;
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