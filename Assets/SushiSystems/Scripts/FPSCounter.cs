using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsText;
    [SerializeField] private TextMeshProUGUI systemText;

    private void Awake() => Initialization();

    private void Update() => CalculateFPS();

    #region Initialization

    private void Initialization()
    {
        fpsText = GetComponent<TextMeshProUGUI>();
        systemText.text = "Device Model: " + SystemInfo.deviceModel + "\nRefresh Rate: " + Screen.currentResolution.refreshRate;
    }

    #endregion

    #region Calculate FPS

    private void CalculateFPS()
    {
        float fps = 1.0f / Time.deltaTime;

        fpsText.text = string.Format("{0:0.} FPS", fps);
    }

    #endregion
}
