using UnityEngine;

public class SlowMotionController : MonoBehaviour
{
    private bool isSlowMotionActive = false;
    private float originalTimeScale;

    private void Start()
    {
        originalTimeScale = Time.timeScale;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleSlowMotion();
        }
    }

    private void ToggleSlowMotion()
    {
        isSlowMotionActive = !isSlowMotionActive;

        if (isSlowMotionActive)
        {
            // You can adjust the time scale value to control the slow-motion effect.
            Time.timeScale = 0.5f; // Example: Set to 0.5 for half-speed.
        }
        else
        {
            Time.timeScale = originalTimeScale;
        }
    }
}
