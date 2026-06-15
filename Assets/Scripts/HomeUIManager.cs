using UnityEngine;

public class HomeUIManager : MonoBehaviour
{
    [SerializeField] private GameObject dropdownPanel;

    private void Start()
    {
        dropdownPanel.SetActive(false);
    }

    public void ToggleMenu()
    {
        dropdownPanel.SetActive(!dropdownPanel.activeSelf);
    }

    public void CloseMenu()
    {
        dropdownPanel.SetActive(false);
    }
}   