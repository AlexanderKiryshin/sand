using UnityEngine;

public class OpenWindow : MonoBehaviour
{
    public void Open()
    {
        GameIsReady.SetWindowOpen(this, true);
        gameObject.SetActive(true);
        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(true);
        }
    }

    public void Close()
    {
        gameObject.SetActive(false);
        GameIsReady.SetWindowOpen(this, false);
        if (AdManager.instance != null)
        {
            AdManager.instance.OnWindowEnabled(false);
        }
    }

    private void OnDisable() => GameIsReady.SetWindowOpen(this, false);
}
