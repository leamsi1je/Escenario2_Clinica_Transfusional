using UnityEngine;

public class SubMenuToggle : MonoBehaviour
{
    [SerializeField] private GameObject subMenu; // arrastra aquí el objeto padre que agrupa los 4 botones

    public void MostrarSubMenu()
    {
        subMenu.SetActive(true);
    }

    public void OcultarSubMenu()
    {
        subMenu.SetActive(false);
    }

    public void ToggleSubMenu()
    {
        subMenu.SetActive(!subMenu.activeSelf);
    }
}