using TMPro;
using UnityEngine;

namespace Sokoban
{
    public class NameInputFilter : MonoBehaviour
    {
        private const int MaxNameLength = 50;

        private TMP_InputField inputField;
        private bool isFiltering;

        public static void Configure(TMP_InputField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            NameInputFilter filter = inputField.GetComponent<NameInputFilter>();
            if (filter == null)
            {
                filter = inputField.gameObject.AddComponent<NameInputFilter>();
            }

            filter.Bind(inputField);
        }

        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char[] buffer = new char[Mathf.Min(value.Length, MaxNameLength)];
            int length = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (length >= MaxNameLength)
                {
                    break;
                }

                char character = value[i];
                if (IsAllowedNameCharacter(character))
                {
                    buffer[length] = character;
                    length++;
                }
            }

            return new string(buffer, 0, length);
        }

        private void OnDestroy()
        {
            if (inputField != null)
            {
                inputField.onValueChanged.RemoveListener(HandleValueChanged);
            }
        }

        private void Bind(TMP_InputField target)
        {
            if (inputField != null && inputField != target)
            {
                inputField.onValueChanged.RemoveListener(HandleValueChanged);
            }

            inputField = target;
            inputField.characterLimit = MaxNameLength;
            inputField.onValidateInput = ValidateCharacter;
            inputField.onValueChanged.RemoveListener(HandleValueChanged);
            inputField.onValueChanged.AddListener(HandleValueChanged);
            SanitizeCurrentText();
        }

        private char ValidateCharacter(string text, int charIndex, char addedChar)
        {
            if (Sanitize(text).Length >= MaxNameLength)
            {
                return '\0';
            }

            return IsAllowedNameCharacter(addedChar) ? addedChar : '\0';
        }

        private void HandleValueChanged(string value)
        {
            if (isFiltering)
            {
                return;
            }

            string filtered = Sanitize(value);
            if (filtered == value)
            {
                return;
            }

            int caretPosition = inputField.caretPosition;
            int removedBeforeCaret = CountRemovedCharactersBefore(value, caretPosition);
            isFiltering = true;
            inputField.text = filtered;
            int adjustedCaretPosition = Mathf.Clamp(caretPosition - removedBeforeCaret, 0, filtered.Length);
            inputField.caretPosition = adjustedCaretPosition;
            inputField.selectionAnchorPosition = adjustedCaretPosition;
            inputField.selectionFocusPosition = adjustedCaretPosition;
            inputField.ForceLabelUpdate();
            isFiltering = false;
        }

        private void SanitizeCurrentText()
        {
            if (inputField != null)
            {
                HandleValueChanged(inputField.text);
            }
        }

        private static int CountRemovedCharactersBefore(string value, int caretPosition)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int length = Mathf.Clamp(caretPosition, 0, value.Length);
            int count = 0;
            int keptCharacters = 0;
            for (int i = 0; i < length; i++)
            {
                if (!IsAllowedNameCharacter(value[i]) || keptCharacters >= MaxNameLength)
                {
                    count++;
                    continue;
                }

                keptCharacters++;
            }

            return count;
        }

        private static bool IsAllowedNameCharacter(char character)
        {
            return character == ' '
                || character == '-'
                || character == '_'
                || (character >= '0' && character <= '9')
                || (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z');
        }
    }
}
