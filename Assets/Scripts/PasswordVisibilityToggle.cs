using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PasswordVisibilityToggle : MonoBehaviour
{
    public TMP_InputField passwordInput;
    public Image eyeButtonImage;

    public Sprite openEyeSprite;
    public Sprite closedEyeSprite;

    private bool isPasswordVisible = false;

    private void Start()
    {
        if (passwordInput == null || eyeButtonImage == null)
        {
            Debug.LogError("Missing passwordInput or eyeButtonImage.");
            return;
        }

        HidePassword();
    }

    public void ToggleVisibility()
    {
        if (isPasswordVisible)
            HidePassword();
        else
            ShowPassword();
    }

    private void ShowPassword()
    {
        isPasswordVisible = true;

        passwordInput.contentType = TMP_InputField.ContentType.Standard;
        passwordInput.inputType = TMP_InputField.InputType.Standard;

        if (openEyeSprite != null)
            eyeButtonImage.sprite = openEyeSprite;

        passwordInput.ForceLabelUpdate();
    }

    private void HidePassword()
    {
        isPasswordVisible = false;

        passwordInput.contentType = TMP_InputField.ContentType.Password;
        passwordInput.inputType = TMP_InputField.InputType.Password;
        passwordInput.asteriskChar = '*';

        if (closedEyeSprite != null)
            eyeButtonImage.sprite = closedEyeSprite;

        passwordInput.ForceLabelUpdate();
    }
}