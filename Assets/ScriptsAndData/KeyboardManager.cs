using UnityEngine;
using TMPro;

public class KeyboardManager : MonoBehaviour
{
    [Header("Canvas del teclado")]
    public GameObject keyboardCanvas;
    public GameObject keyboardInputCanvas;

    [Header("Donde se muestra el texto en la tablet")]
    public TextMeshProUGUI panelText;

    void Start()
    {
        keyboardCanvas.SetActive(false);
        keyboardInputCanvas.SetActive(false);
    }

    public void ShowKeyboard()
    {
        keyboardCanvas.SetActive(true);
        keyboardInputCanvas.SetActive(true);
    }

    public void HideKeyboard()
    {
        keyboardCanvas.SetActive(false);
        keyboardInputCanvas.SetActive(false);
    }

    // Llama este método desde el botón "Confirmar" del teclado
    public void OnTextConfirmed(string inputText)
    {
        if (panelText != null)
            panelText.text = inputText;
        HideKeyboard();
    }
}