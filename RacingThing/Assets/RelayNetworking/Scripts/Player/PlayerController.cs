using RelayNetworking.Client;
using RelayNetworking.Unity;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkRigidbody))]
public class CubeMovement : MonoBehaviour
{
    public float moveSpeed = 8f;
    public float jumpForce = 6f;
    public float groundCheckDistance = 1.1f;

    private NetworkRigidbody rb;
    private Vector3 moveInput;
    private bool grounded;
    private NetworkObject networkObject;

    void Start()
    {
        networkObject = GetComponent<NetworkObject>();
        rb = GetComponent<NetworkRigidbody>();


        if (!networkObject.IsMine)
        {
            enabled = false;
            return;
        }

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        Debug.Log($"{name} movement enabled");
    }

    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        moveInput = new Vector3(x, 0f, z).normalized;

        grounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance);

        if (grounded && Input.GetKeyDown(KeyCode.Space))
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    void FixedUpdate()
    {
        Vector3 velocity = rb.velocity;
        velocity.x = moveInput.x * moveSpeed;
        velocity.z = moveInput.z * moveSpeed;
        rb.velocity = velocity;
    }
}