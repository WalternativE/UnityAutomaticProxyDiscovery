using TMPro;
using UnityEngine;

namespace Ui
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public class FetchMethodDropdown : MonoBehaviour
    {
        public delegate void OnMethodChangedHandler(string method);

        public event OnMethodChangedHandler OnMethodChanged;

        public void MethodChanged()
        {
            var dropdown = GetComponent<TMP_Dropdown>();
        
            var option = dropdown.options[dropdown.value];
            OnMethodChanged?.Invoke(option.text);
        }
    }
}