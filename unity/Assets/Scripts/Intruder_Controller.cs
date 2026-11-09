using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Intruder_Controller : MonoBehaviour
{
    public List<Transform> waypoints;
    public float moveSpeed = 3.0f;
    public float rotationSpeed = 3.0f;

    private int currentWaypointIndex = 0;
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            return;
        }

        Transform targetWaypoint = waypoints[currentWaypointIndex];

        // 이동
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetWaypoint.position,
            moveSpeed * Time.deltaTime
        );

        // 회전
        Vector3 directionToTarget = targetWaypoint.position - transform.position;
        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // 목표 도착 확인
        if (Vector3.Distance(transform.position, targetWaypoint.position) < 0.1f)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
            {

                currentWaypointIndex = waypoints.Count - 1;
                moveSpeed = 0;
                if (animator != null)
                {
                    animator.speed = 0f;
                }
            }
        }
    }
}