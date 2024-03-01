using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtMouse : MonoBehaviour
{
    private Vector3 mousePos;
    private float baseSmoothing = 0.2f;
    [SerializeField] bool smoothRotation = true;
    [SerializeField] float smoothRotationSpeed = 1;

    Plane plane;

    //debugValues
    Vector3 hitPoint;
    // Start is called before the first frame update
    void Start()
    {
        plane = new Plane(Vector3.up, Vector3.zero); // Plane at Y=0
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        plane.SetNormalAndPosition(Vector3.up, transform.position);

        float enter;
        if (plane.Raycast(ray, out enter))
        {
            hitPoint = ray.GetPoint(enter);
            RotateObjectToPoint(hitPoint);
        }
    }

    void RotateObjectToPoint(Vector3 point)
    {
        Vector3 directionToFace = point - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(-directionToFace);
        targetRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0); // Ensure rotation only on Y axis
        transform.rotation = smoothRotation ? Quaternion.Slerp(transform.rotation, targetRotation, baseSmoothing * smoothRotationSpeed) : targetRotation;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawSphere(hitPoint, 0.5f);
        Gizmos.DrawWireCube(transform.position, new Vector3(1,0.01f, 1));
    }
}
