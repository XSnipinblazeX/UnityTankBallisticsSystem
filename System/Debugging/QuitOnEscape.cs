using UnityEngine;

public class QuitOnEscape : MonoBehaviour
{
    void Update()
    {
        // Check if the Escape key was pressed down this frame
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            
            // This function closes the application
            Application.Quit();
            
            // Note: For testing in the Unity Editor, this will not close the Editor.
            // It will stop Play Mode. You can add a Debug.Log to confirm it works.
            Debug.Log("Application has quit.");

            // For editor testing, you can use:
            // #if UNITY_EDITOR
            //     UnityEditor.EditorApplication.isPlaying = false;
            // #endif
        }
    }
}