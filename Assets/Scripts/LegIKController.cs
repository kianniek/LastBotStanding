using CMF;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
[Serializable]
public struct LegStruct
{
    [SerializeField] public Transform upperLeg;
    [SerializeField] public Transform middleLeg;
    [SerializeField] public Transform lowerLeg;
}
public class LegIKController : MonoBehaviour
{
    [SerializeField] AdvancedWalkerController walkerController;
    public Transform bodyCenter; // Reference to the body's center
    public LegStruct[] legs;
    private float[] lerpTimer;   // Array to store lerping timers for each leg
    public float feetDistance;   // Distance from the feet to the ground
    public float stepDistance = 1.0f; // The maximum distance a leg can step at a time
    [Range(0, 1)]
    public float stepSpeed = 0.9f;   // The speed a leg can step at a time
    public float stepHeight;         // The height of the leg's step

    // GameObjects for the leg targets
    public GameObject[] legTarget;
    public bool[] stepping;  // Array to track if each leg is currently stepping
    private Vector3 lastGroundPos_BR;
    private Vector3 lastGroundPos_BL;
    private Vector3 lastGroundPos_L;
    private Vector3 lastGroundPos_R;
    private Vector3 previousBodyPosition;
    private Quaternion previousBodyRotation;

    private int activeLegIndex = 0; // Index of the currently active leg

    public bool standingStill;
    public Transform currentPlatform = null;
    private Vector3 platformPositionLastFrame;
    private Quaternion platformRotationLastFrame;
    // Layer mask to detect ground
    public LayerMask groundLayer;

    void Start()
    {
        // Initialization
        lerpTimer = new float[4];
        stepping = new bool[4];
        previousBodyPosition = bodyCenter.position;
        previousBodyRotation = bodyCenter.rotation;
    }

    void Update()
    {
        // Grounded check to look up if feet are even touching the ground
        if (!walkerController.IsGrounded())
        {
            JumpingBehavour(ref legTarget);
            return;
        }

        // Update leg target positions relative to the body
        AdjustTargetPositionsRelativeToBody();

        // Update the target positions for the active leg
        UpdateLegTargetPositions(activeLegIndex);

        //if (standingStill) { return; }
        // Update the previous body transform for the next frame
        UpdatePreviousBodyTransform();

        HorizontalRotationUpperLeg();
    }

    void UpdateLegTargetPositions(int legIndex)
    {
        // Check if the character is standing still and on a platform
        if (standingStill && currentPlatform != null)
        {
            MoveLegTargetsWithPlatform();
        }
        else
        {
            // Your existing logic for moving leg targets
            switch (legIndex)
            {
                case 0:
                    legTarget[0].transform.position = CalculateTargetForLeg(legs[0].upperLeg, legTarget[0], ref lerpTimer[0], ref stepping[0], ref lastGroundPos_BR);
                    legTarget[3].transform.position = CalculateTargetForLeg(legs[3].upperLeg, legTarget[3], ref lerpTimer[3], ref stepping[3], ref lastGroundPos_R);
                    break;
                case 1:
                    legTarget[1].transform.position = CalculateTargetForLeg(legs[1].upperLeg, legTarget[1], ref lerpTimer[1], ref stepping[1], ref lastGroundPos_BL);
                    legTarget[2].transform.position = CalculateTargetForLeg(legs[2].upperLeg, legTarget[2], ref lerpTimer[2], ref stepping[2], ref lastGroundPos_L);
                    break;
                case 2:
                    break;
                case 3:
                    break;
            }

            // Check if the active leg has finished stepping
            if (!stepping[legIndex] && lerpTimer[legIndex] >= 0.99f)
            {
                // Switch to the next leg as the active leg
                activeLegIndex = (activeLegIndex + 1) % 2;
            }
        }

        bool allFalse = true; // Assume all elements are false initially
        int arraySize = stepping.Length; // Get the size of the array

        for (int i = 0; i < arraySize; i++)
        {
            if (stepping[i] || walkerController.GetVelocity().magnitude > 0.1f)
            { // If any element is true
                allFalse = false;
                break; // Exit the loop early as we found a true element
            }
        }
        // Code to execute if all elements are false
        //if (allFalse) { }
        standingStill = allFalse;
    }

    Vector3 CalculateTargetForLeg(Transform legRoot, GameObject currentTarget, ref float lerpTimer, ref bool stepping, ref Vector3 lastGroundPos)
    {
        Vector3 directionToSide = Vector3.Cross(bodyCenter.up, legRoot.position - bodyCenter.position).normalized;
        Vector3 averageUp = (Vector3.up + bodyCenter.up).normalized;
        Vector3 rayDirection = Vector3.Cross(averageUp, directionToSide).normalized;
        Vector3 bodyMovement = bodyCenter.position - previousBodyPosition;

        if (Physics.Raycast(legRoot.position - rayDirection * feetDistance + transform.up * 2, -averageUp, out RaycastHit hitDown, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore))
        {
            // Check if the distance is greater than the step distance
            if (Vector3.Distance(currentTarget.transform.position, hitDown.point) > stepDistance && !stepping)
            {
                // Set the leg to start stepping
                stepping = true;
                lastGroundPos = currentTarget.transform.position;
            }
            else if (stepping)
            {
                // Lerp the leg target position to create a stepping motion
                lerpTimer += Time.deltaTime * (1 / stepSpeed + 0.0001f);
                lerpTimer = Mathf.Clamp(lerpTimer, 0, 1);

                // Calculate the offset for the step height
                float offsetHeight = -4 * stepHeight * Mathf.Pow(lerpTimer - 0.5f, 2) + stepHeight;
                stepping = lerpTimer < 0.99f;

                // Apply the offset to the ground position
                Vector3 tmp = hitDown.point;
                hitDown.point = new Vector3(tmp.x, tmp.y + offsetHeight, tmp.z);

                // Lerp between the last ground position and the new position
                return Vector3.Lerp(lastGroundPos, hitDown.point, lerpTimer);
            }
            else
            {
                // Keep the current target position if not stepping
                lerpTimer = 0;
                return currentTarget.transform.position;
            }
        }

        // Return the current position if raycast fails
        lerpTimer = 0;
        return legRoot.position;
    }

    void AdjustTargetPositionsRelativeToBody()
    {
        Vector3 bodyMovement = bodyCenter.position - previousBodyPosition;
        Quaternion bodyRotationChange = bodyCenter.rotation * Quaternion.Inverse(previousBodyRotation);

        // Adjust each target's position relative to the body's movement and rotation
        for (int i = 0; i < legTarget.Length; i++)
        {
            AdjustTargetPosition(legTarget[i], bodyMovement, bodyRotationChange);
        }
    }

    void JumpingBehavour(ref GameObject[] legTargets)
    {
        for (int i = 0; i < legTargets.Length; i++)
        {
            Vector3 directionToSide = Vector3.Cross(bodyCenter.up, legs[i].upperLeg.position - bodyCenter.position).normalized;
            Vector3 averageUp = (Vector3.up + bodyCenter.up).normalized;
            Vector3 rayDirection = Vector3.Cross(averageUp, directionToSide).normalized;

            if (Physics.Raycast(legs[i].upperLeg.position - rayDirection * feetDistance + transform.up * 2, -averageUp, out RaycastHit hitDown, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore))
            {
                legTargets[i].transform.position = hitDown.point;
            }

        }

        previousBodyPosition = bodyCenter.position;
        previousBodyRotation = bodyCenter.rotation;
    }

    void AdjustTargetPosition(GameObject target, Vector3 movement, Quaternion rotationChange)
    {
        // Calculate the offset from the body center to the target
        Vector3 offset = target.transform.position - bodyCenter.position;

        // Apply the inverse of the body's movement and rotation to the target
        offset = Quaternion.Inverse(rotationChange) * offset;
        target.transform.position = bodyCenter.position + offset - movement;
    }

    void UpdatePreviousBodyTransform()
    {
        // Update the previous body position and rotation for the next frame
        previousBodyPosition = bodyCenter.position;
        previousBodyRotation = bodyCenter.rotation;
    }

    void HorizontalRotationUpperLeg()
    {
        for (int i = 0; i < legs.Length; i++)
        {

        }
    }
    private void MoveLegTargetsWithPlatform()
    {
        Vector3 platformDeltaPosition = currentPlatform.position - platformPositionLastFrame;
        Quaternion platformDeltaRotation = currentPlatform.rotation * Quaternion.Inverse(platformRotationLastFrame);

        // Apply the platform's movement and rotation to each leg target
        for (int i = 0; i < legTarget.Length; i++)
        {
            legTarget[i].transform.position += platformDeltaPosition;
            legTarget[i].transform.rotation *= platformDeltaRotation;
        }

        platformPositionLastFrame = currentPlatform.position;
        platformRotationLastFrame = currentPlatform.rotation;
    }

    public void SetCurrentPlatform(Transform platform)
    {
        currentPlatform = platform;
    }
    private void OnDrawGizmos()
    {
        // Draw rays for debugging purposes
        Transform legRoot = legs[0].upperLeg;
        Vector3 directionToSide = Vector3.Cross(bodyCenter.up, legRoot.position - bodyCenter.position).normalized;
        Vector3 averageUp = (Vector3.up + bodyCenter.up).normalized;
        Vector3 rayDirection = Vector3.Cross(averageUp, directionToSide).normalized;
        Gizmos.DrawRay(legRoot.position, -rayDirection * feetDistance + transform.up * 2);
        Gizmos.DrawRay(legRoot.position - rayDirection * feetDistance + transform.up * 2, -averageUp * 2);
    }
}
