using UnityEngine;
using UnityEngine.UI;

namespace ArtColorSupporter
{
    /// <summary>
    /// Text に翻訳キーを割り当て、言語切り替え時に自動で表示を更新する。
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] string key;

        object[] args;
        Text text;

        public string Key => key;

        public void SetKey(string newKey, params object[] newArgs)
        {
            key = newKey;
            args = newArgs;
            Refresh();
        }

        void OnEnable()
        {
            Localization.LanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Localization.LanguageChanged -= Refresh;
        }

        public void Refresh()
        {
            if (text == null) text = GetComponent<Text>();
            text.text = Localization.Get(key, args);
        }
    }
}
