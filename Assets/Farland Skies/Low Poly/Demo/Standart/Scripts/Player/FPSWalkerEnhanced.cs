/**
 * This is an enhanced version of the FPSWalker from UnifyWiki:
 * http://wiki.unity3d.com/index.php?title=FPSWalkerEnhanced
 */

using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSWalkerEnhanced : MonoBehaviour
{
    public float walkSpeed = 6.0f;

    public float runSpeed = 11.0f;

    // 대각선으로 이동할 때 속도가 더 빨라지는 것을 제한한다.
    public bool limitDiagonalSpeed = true;

    // 체크하면 달리기 키를 누를 때마다 걷기/달리기가 전환된다.
    // 체크하지 않으면 달리기 키를 누르고 있을 때만 달린다.
    public bool toggleRun = false;

    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;

    // 이 거리보다 더 많이 떨어지면 낙하 피해 함수를 실행한다.
    public float fallingDamageThreshold = 10.0f;

    // 경사가 Character Controller의 Slope Limit 이상이면 미끄러진다.
    public bool slideWhenOverSlopeLimit = false;

    // "Slide" 태그가 붙은 오브젝트 위에서 미끄러질지 설정한다.
    public bool slideOnTaggedObjects = false;

    public float slideSpeed = 12.0f;

    // 공중에서도 이동 방향을 조절할 수 있는지 설정한다.
    public bool airControl = false;

    // 경사면을 내려갈 때 바닥에서 튀는 현상을 줄인다.
    public float antiBumpFactor = 0.75f;

    // 착지 후 다시 점프할 수 있을 때까지 기다리는 물리 프레임 수
    public int antiBunnyHopFactor = 1;

    private Vector3 moveDirection = Vector3.zero;
    private bool grounded = false;
    private CharacterController controller;
    private Transform myTransform;
    private float speed;
    private RaycastHit hit;
    private float fallStartLevel;
    private bool falling;
    private float slideLimit;
    private float rayDistance;
    private Vector3 contactPoint;
    private bool playerControl = false;
    private int jumpTimer;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        myTransform = transform;
        speed = walkSpeed;

        rayDistance =
            controller.height * 0.5f + controller.radius;

        slideLimit = controller.slopeLimit - 0.1f;
        jumpTimer = antiBunnyHopFactor;
    }

    private void FixedUpdate()
    {
        float inputX = Input.GetAxis("Horizontal");
        float inputY = Input.GetAxis("Vertical");

        // 대각선 이동 시 이동 속도가 약 1.4배 빨라지는 것을 방지한다.
        float inputModifyFactor =
            inputX != 0.0f &&
            inputY != 0.0f &&
            limitDiagonalSpeed
                ? 0.7071f
                : 1.0f;

        if (grounded)
        {
            bool sliding = false;

            // 플레이어 바로 아래의 경사를 확인한다.
            if (Physics.Raycast(
                    myTransform.position,
                    -Vector3.up,
                    out hit,
                    rayDistance))
            {
                if (Vector3.Angle(hit.normal, Vector3.up) >
                    slideLimit)
                {
                    sliding = true;
                }
            }
            else
            {
                // 중앙 아래쪽 Raycast가 실패하면
                // 마지막 충돌 지점을 기준으로 다시 확인한다.
                Physics.Raycast(
                    contactPoint + Vector3.up,
                    -Vector3.up,
                    out hit);

                if (Vector3.Angle(hit.normal, Vector3.up) >
                    slideLimit)
                {
                    sliding = true;
                }
            }

            // 낙하 중이었다면 떨어진 거리를 확인한다.
            if (falling)
            {
                falling = false;

                if (myTransform.position.y <
                    fallStartLevel - fallingDamageThreshold)
                {
                    FallingDamageAlert(
                        fallStartLevel - myTransform.position.y);
                }
            }

            // 달리기 토글 방식이 아니라면
            // 키를 누르고 있는 동안만 달린다.
            if (!toggleRun)
            {
                speed = Input.GetButton("Fire3")
                    ? runSpeed
                    : walkSpeed;
            }

            // 경사면에서 미끄러지는 경우
            if ((sliding && slideWhenOverSlopeLimit) ||
                (slideOnTaggedObjects &&
                 hit.collider != null &&
                 hit.collider.CompareTag("Slide")))
            {
                Vector3 hitNormal = hit.normal;

                moveDirection = new Vector3(
                    hitNormal.x,
                    -hitNormal.y,
                    hitNormal.z);

                Vector3.OrthoNormalize(
                    ref hitNormal,
                    ref moveDirection);

                moveDirection *= slideSpeed;
                playerControl = false;
            }
            else
            {
                // 일반적인 지상 이동
                moveDirection = new Vector3(
                    inputX * inputModifyFactor,
                    -antiBumpFactor,
                    inputY * inputModifyFactor);

                moveDirection =
                    myTransform.TransformDirection(moveDirection) *
                    speed;

                playerControl = true;
            }

            // 점프 처리
            if (!Input.GetButton("Jump"))
            {
                jumpTimer++;
            }
            else if (jumpTimer >= antiBunnyHopFactor)
            {
                moveDirection.y = Input.GetButton("Fire3")
                    ? jumpSpeed * 5f
                    : jumpSpeed;

                jumpTimer = 0;
            }
        }
        else
        {
            // 공중에 떨어지기 시작한 위치를 저장한다.
            if (!falling)
            {
                falling = true;
                fallStartLevel = myTransform.position.y;
            }

            // 공중 이동 허용 시 수평 이동 방향을 변경한다.
            if (airControl && playerControl)
            {
                moveDirection.x =
                    inputX * speed * inputModifyFactor;

                moveDirection.z =
                    inputY * speed * inputModifyFactor;

                moveDirection =
                    myTransform.TransformDirection(moveDirection);
            }
        }

        // 중력 적용
        moveDirection.y -= gravity * Time.deltaTime;

        // Character Controller를 실제로 이동시킨다.
        grounded =
            (controller.Move(
                 moveDirection * Time.deltaTime) &
             CollisionFlags.Below) != 0;
    }

    private void Update()
    {
        // 달리기 토글 방식일 때 걷기와 달리기를 전환한다.
        if (toggleRun &&
            grounded &&
            Input.GetButtonDown("Fire3"))
        {
            speed = speed == walkSpeed
                ? runSpeed
                : walkSpeed;
        }
    }

    // Character Controller가 부딪힌 지점을 저장한다.
    private void OnControllerColliderHit(
        ControllerColliderHit controllerHit)
    {
        contactPoint = controllerHit.point;
    }

    // 낙하 피해가 발생했을 때 실행할 함수
    private void FallingDamageAlert(float fallDistance)
    {
        Debug.Log(
            "Ouch! Fell " + fallDistance + " units!");
    }
}