using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GNB;


public class FollowBullet : MonoBehaviour
{

    [Header("Targeting")]
    [Tooltip("The bullet to follow.")]
    public Transform youngestBullet;

    [Header("Camera Control")]
    [Tooltip("The offset from the bullet's position. X is right/left, Y is up/down, Z is in front/behind.")]
    public Vector3 positionOffset = new Vector3(-2f, 1f, -5f);
    [Tooltip("An additional rotation applied to the camera's look direction.")]
    public Vector3 rotationOffset = new Vector3(0f, 0f, 0f);
    [Tooltip("How smoothly the camera rotates to look at the bullet.")]
    public float lookSpeed = 5f;

    void LateUpdate()
    {
        if (youngestBullet == null) return;

        // Get the bullet's script to access its velocity
        Bullet bulletScript = youngestBullet.GetComponent<Bullet>();
        if (bulletScript == null) return;

        // Calculate the direction the bullet is moving
        Vector3 bulletVelocityDirection = bulletScript.Velocity.normalized;

        // Create a look-at rotation based on the bullet's velocity direction.
        // This acts as our "reference frame" for the offsets.
        Quaternion bulletRotation = Quaternion.LookRotation(bulletVelocityDirection);

        // Apply the position offset relative to the bullet's direction of travel.
        Vector3 worldPositionOffset = bulletRotation * positionOffset;
        transform.position = youngestBullet.position + worldPositionOffset;

        // Calculate the rotation needed to look directly at the bullet.
        Vector3 directionToTarget = youngestBullet.position - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        // Apply the rotation offset. This is done by multiplying the base rotation
        // by an additional Quaternion from the rotationOffset vector.
        Quaternion finalRotation = targetRotation * Quaternion.Euler(rotationOffset);

        // Smoothly interpolate the camera's rotation to the new final rotation.
        transform.rotation = Quaternion.Slerp(transform.rotation, finalRotation, lookSpeed * Time.deltaTime);
    }
}
