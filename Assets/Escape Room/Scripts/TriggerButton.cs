using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thin wrapper on button prefabs. Clicking routes through GameManager.ActivateTrigger()
/// which handles the UI context switch and action execution.
/// </summary>
public class TriggerButton : MonoBehaviour
{
    private TriggerData data;
    private Button button;

    public TriggerData Data => data;

    public void Initialize(TriggerData triggerData)
    {
        data = triggerData;
        button = GetComponent<Button>();

        // Route all clicks through GameManager — it handles UI context + action
        button.onClick.AddListener(() => GameManager.Instance.ActivateTrigger(data));
    }
}