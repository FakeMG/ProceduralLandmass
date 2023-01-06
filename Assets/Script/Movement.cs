using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour {

    public bool CanMove { get; private set; } = true;
    public bool IsSprinting => canSprint && Input.GetKey(sprintKey) && currentInput.x > 0.1f;
    public bool ShouldJump => Input.GetKeyDown(jumpKey) && characterController.isGrounded;

    [Header("Movement Parameters")]
    [SerializeField][Min(0)] private float walkMaxSpeed = 4.0f;
    [SerializeField] private float gravity = 0.24f;

    [Header("Sprint Parameters")]
    [SerializeField] private bool canSprint = true;
    [SerializeField][Min(0)] private float sprintMaxSpeed = 6.0f;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Jump Parameters")]
    [SerializeField] private bool canJump = true;
    [SerializeField][Min(0)] private float jumpForce = 8.0f;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Header("Crouch Parameters")]
    [SerializeField] private bool canCrouch = true;
    [SerializeField][Min(0)] private float crouchMaxSpeed = 1.0f;
    [SerializeField] private float crouchHeight = 0.9f;
    [SerializeField] private float standHeight = 1.9f;
    [SerializeField] private float timeToCrouch = 0.2f;
    [SerializeField] private Vector3 standCenter = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 crouchCenter = new Vector3(0, 0.5f, 0);
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    private bool isCrouching = false;
    private Coroutine crouchCoroutine;

    [Header("Headbob Parameters")]
    [SerializeField] private bool canHeadbob = true;
    [SerializeField] private float headbobTriggerSpeed = 1f;
    [SerializeField] private float walkHeadbobSpeed = 9f;
    [SerializeField] private float walkHeadbobAmount = 0.015f;
    [SerializeField] private float sprintHeadbobSpeed = 11f;
    [SerializeField] private float sprintHeadbobAmount = 0.025f;
    [SerializeField] private float crouchHeadbobSpeed = 4f;
    [SerializeField] private float crouchHeadbobAmount = 0.01f;
    private Vector3 defaultCameraLocalPos;
    private float headbobTimer = 0f;

    [Header("Slope Parameters")]
    [SerializeField] private bool canSlideOnSlope = true;
    [SerializeField] private float slopeSlideSpeed = 8f;
    private Vector3 hitPointNormal;
    private bool IsSliding {
        get {
            if (characterController.isGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHitInfo, 2f)) {
                hitPointNormal = slopeHitInfo.normal;

                return Vector3.Angle(Vector3.up, hitPointNormal) > characterController.slopeLimit;
            } else {
                return false;
            }
        }
    }

    [Header("Look Parameters")]
    [SerializeField, Range(1, 10)] private float lookSpeedX = 2.0f;
    [SerializeField, Range(1, 10)] private float lookSpeedY = 2.0f;
    [SerializeField, Range(1, 90)] private float upperLookLimit = 80.0f;
    [SerializeField, Range(1, 90)] private float lowerLookLimit = 80.0f;

    private CharacterController characterController;
    private Camera playerCamera;

    private Vector3 moveDirection;
    private Vector2 currentInput;

    private float currentMaxSpeed;
    private float cameraRotationX = 0;

    void Awake() {
        playerCamera = GetComponentInChildren<Camera>();
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        defaultCameraLocalPos = playerCamera.transform.localPosition;
    }

    // Update is called once per frame
    void Update() {
        if (CanMove) {
            ControlMaxSpeed();
            LimitDiagonalSpeed();
            HandleMovementDirectionInWorldSpace();

            if (canJump) {
                HandleJump();
            }

            if (canCrouch) {
                HandleCrouch();
            }

            HandleMouseLook();

            ApplyFinalMovement();
        }
    }

    private void LateUpdate() {
        if (CanMove) {
            if (canHeadbob) {
                HandleHeadbob();
                ResetHeadbob();
            }
        }
    }

    private void ControlMaxSpeed() {
        if (IsSprinting) {
            currentMaxSpeed = sprintMaxSpeed;
        } else {
            currentMaxSpeed = walkMaxSpeed;
        }

        if (isCrouching) {
            currentMaxSpeed = crouchMaxSpeed;
        }
    }

    private void LimitDiagonalSpeed() {
        currentInput = new Vector2(currentMaxSpeed * Input.GetAxis("Vertical"), currentMaxSpeed * Input.GetAxis("Horizontal"));

        if (currentInput.x != 0 && currentInput.y != 0) {
            if (IsSprinting) {
                float x = ValueToReduce(sprintMaxSpeed, walkMaxSpeed);
                currentInput.x = Mathf.Clamp(currentInput.x, -(sprintMaxSpeed - x), sprintMaxSpeed - x);
                currentInput.y = Mathf.Clamp(currentInput.y, -(walkMaxSpeed - x), walkMaxSpeed - x);
            } else {
                float speed = Mathf.Sqrt((currentMaxSpeed * currentMaxSpeed) / 2);
                currentInput.x = Mathf.Clamp(currentInput.x, -speed, speed);
                currentInput.y = Mathf.Clamp(currentInput.y, -speed, speed);
            }
        }
    }

    // a, b > 0
    private float ValueToReduce(float a, float b) {
        if (b > a) {
            float temp = a;
            a = b;
            b = temp;
        }
        float delta = Mathf.Pow(-2 * a - 2 * b, 2) - 4 * 2 * b * b;
        float x1 = (-(-2 * a - 2 * b) + Mathf.Sqrt(delta)) / (2 * 2);
        float x2 = (-(-2 * a - 2 * b) - Mathf.Sqrt(delta)) / (2 * 2);

        return x1 < x2 ? x1 : x2;
    }

    private void HandleMovementDirectionInWorldSpace() {
        float moveDirectionY = moveDirection.y;
        moveDirection = transform.TransformDirection(Vector3.forward) * currentInput.x + transform.TransformDirection(Vector3.right) * currentInput.y;
        moveDirection.y = moveDirectionY;
    }

    private void HandleJump() {
        if (ShouldJump) {
            moveDirection.y = jumpForce;
        }
    }

    private void HandleCrouch() {
        if (Input.GetKeyUp(crouchKey) || (!Input.GetKey(crouchKey) && isCrouching)) {
            if (!Physics.Raycast(playerCamera.transform.position, Vector3.up, 1f)) {

                if (crouchCoroutine != null) {
                    StopCoroutine(crouchCoroutine);
                }
                crouchCoroutine = StartCoroutine(CrouchOrStand());
            }
        }

        if (Input.GetKeyDown(crouchKey) && !isCrouching) {
            if (crouchCoroutine != null) {
                StopCoroutine(crouchCoroutine);
            }
            crouchCoroutine = StartCoroutine(CrouchOrStand());
        }
    }

    private IEnumerator CrouchOrStand() {
        isCrouching = !isCrouching;

        float timeElapsed = 0f;

        float targetHeight = isCrouching ? crouchHeight : standHeight;
        float currentHeight = characterController.height;

        Vector3 targetCenter = isCrouching ? crouchCenter : standCenter;
        Vector3 currentCenter = characterController.center;

        while (timeElapsed < timeToCrouch) {
            characterController.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / timeToCrouch);
            characterController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / timeToCrouch);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        characterController.height = targetHeight;
        characterController.center = targetCenter;
    }

    private void HandleHeadbob() {
        if (!characterController.isGrounded)
            return;

        if (currentPlayerSpeed() >= headbobTriggerSpeed) {
            playerCamera.transform.localPosition = new Vector3(
                defaultCameraLocalPos.x + HeadBobMotion().x,
                defaultCameraLocalPos.y + HeadBobMotion().y,
                playerCamera.transform.localPosition.z);
        }
    }

    private Vector3 HeadBobMotion() {
        Vector3 pos = Vector3.zero;
        //làm headbob mượt hơn
        headbobTimer += Time.deltaTime;

        pos.y = Mathf.Sin(headbobTimer * (isCrouching ? crouchHeadbobSpeed : IsSprinting ? sprintHeadbobSpeed : walkHeadbobSpeed))
            * (isCrouching ? crouchHeadbobAmount : IsSprinting ? sprintHeadbobAmount : walkHeadbobAmount);
        pos.x = Mathf.Sin(headbobTimer * (isCrouching ? crouchHeadbobSpeed : IsSprinting ? sprintHeadbobSpeed : walkHeadbobSpeed) / 2)
            * (isCrouching ? crouchHeadbobAmount : IsSprinting ? sprintHeadbobAmount : walkHeadbobAmount)
            * 2;
        return pos;
    }

    private void ResetHeadbob() {
        if (playerCamera.transform.localPosition == defaultCameraLocalPos)
            return;

        if (currentPlayerSpeed() < headbobTriggerSpeed) {
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, defaultCameraLocalPos, 2 * Time.deltaTime);
            headbobTimer = 0;
        }
    }

    private float currentPlayerSpeed() {
        return Mathf.Sqrt(moveDirection.x * moveDirection.x + moveDirection.z * moveDirection.z);
    }

    private void HandleMouseLook() {
        cameraRotationX += Input.GetAxis("Mouse Y") * lookSpeedY;
        cameraRotationX = Mathf.Clamp(cameraRotationX, -lowerLookLimit, upperLookLimit);
        playerCamera.transform.localRotation = Quaternion.Inverse(Quaternion.Euler(cameraRotationX, 0, 0));

        transform.localRotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeedX, 0);
    }

    private void ApplyFinalMovement() {
        if (!characterController.isGrounded) {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        if (canSlideOnSlope && IsSliding) {
            moveDirection += new Vector3(hitPointNormal.x, -hitPointNormal.y, hitPointNormal.z) * slopeSlideSpeed;
        }

        characterController.Move(moveDirection * Time.deltaTime);
    }
}
