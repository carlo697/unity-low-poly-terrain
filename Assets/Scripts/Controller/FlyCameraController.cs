using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlyCameraController : MonoBehaviour {
  public float speed = 7f;
  public float sprintSpeed = 25f;
  public float mouseSensitivity = 5f;

  private Rigidbody m_rigidbody;
  private bool m_isSprinting;

  private void Awake() {
    m_rigidbody = GetComponent<Rigidbody>();
    LockCursor();
  }

  private void LockCursor() {
    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
  }

  private void Update() {
    m_isSprinting = Input.GetButton("Sprint");

    Vector3 rotation = transform.rotation.eulerAngles;

    // Horizontal mouse movement
    float mouseX = Input.GetAxis("Mouse X");
    rotation.y += mouseX;

    // Vertical mouse movement
    float mouseY = Input.GetAxis("Mouse Y");
    rotation.x += mouseY * -1f;

    transform.rotation = Quaternion.Euler(rotation);
  }

  private void FixedUpdate() {
    Vector3 direction = Vector3.zero;
    float targetSpeed = speed;

    // Sprinting
    if (m_isSprinting) {
      targetSpeed = sprintSpeed;
    }

    // Add vertical speed
    float vertical = Input.GetAxis("Vertical");
    direction += transform.forward * vertical * targetSpeed;

    // Add horizontal speed
    float horizontal = Input.GetAxis("Horizontal");
    direction += transform.right * horizontal * targetSpeed;

    // Add axial speed
    float axial = Input.GetAxis("Axial");
    direction += Vector3.up * axial * targetSpeed;

    m_rigidbody.velocity = direction;
  }
}
