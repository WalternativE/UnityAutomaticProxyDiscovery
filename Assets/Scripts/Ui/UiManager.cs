using TMPro;
using UnityEngine;

namespace Ui
{
    public class UiManager : MonoBehaviour
    {
        [SerializeField] private FetchQuotesButton fetchQuotesButton;
        [SerializeField] private FetchMethodDropdown fetchMethodDropdown;
        [SerializeField] private TextMeshProUGUI text;

        private void Awake()
        {
            text.text = string.Empty;

            fetchQuotesButton.OnQuoteFetched += (quoteText) => text.text = quoteText;
            fetchMethodDropdown.OnMethodChanged += (methodName) =>
            {
                switch (methodName)
                {
                    case "WWW":
                        fetchQuotesButton.shouldUseUnityWebRequest = true;
                        break;
                    default:
                        fetchQuotesButton.shouldUseUnityWebRequest = false;
                        break;
                }
            };
        }
    }
}