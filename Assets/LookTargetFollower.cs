using UnityEngine;

public class LookTargetFollower : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float followSpeed = 10f;
    [SerializeField] private float distanceFromHead = 5f;

    void LateUpdate()
    {
        // Позиция перед камерой
        Vector3 targetPos = cameraTransform.position + cameraTransform.forward * distanceFromHead;
        transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.deltaTime);
    }
}