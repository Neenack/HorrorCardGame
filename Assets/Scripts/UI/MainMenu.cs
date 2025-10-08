using UnityEngine;

public class MainMenu : MonoBehaviour
{
    bool cursorLocked = true;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (cursorLocked)
            {
                Cursor.visible = true;
                cursorLocked = false;
            }
            else
            {
                Cursor.visible = false;
                cursorLocked = true;
            }
        }
    }
}
