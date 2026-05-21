using UnityEngine;

/// <summary>
/// Simple camera follower that smoothly follows the player.
/// </summary>
public class CameraFollower : MonoBehaviour
{
    [SerializeField] Transform _target;
    [SerializeField] float _smoothSpeed = 5f;
    [SerializeField] Vector3 _offset = new Vector3(0f, 1f, -10f);

    void LateUpdate()
    {
        if (_target == null) return;

        Vector3 desiredPosition = _target.position + _offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;
    }
}
