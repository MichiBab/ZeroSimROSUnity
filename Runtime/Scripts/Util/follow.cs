using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class follow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    public float smoothSpeed = 1.0f;

    private Vector3 desiredPosition;
    // Start is called before the first frame update
    void Start()
    {
         
    }

    // Update is called once per frame
    void Update()
    {
        desiredPosition = target.position + target.TransformDirection(offset);

        // Smoothly move the camera towards the desired position
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // Make the camera look at the target
        transform.LookAt(target);

    }
}
