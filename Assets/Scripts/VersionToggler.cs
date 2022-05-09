using UnityEngine;
using UnityEngine.UI;

public class VersionDropDown : MonoBehaviour
{
    Dropdown m_Dropdown;
    public Text m_Text;
    int m_DropdownValue;

    public void Update()
    {
        m_DropdownValue = m_Dropdown.value;
        Debug.Log(m_DropdownValue);
    }
}