using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreFramework
{
    /// <summary>
    /// 对话 UI 面板，显示 NPC 台词和玩家选项，支持打字机效果。
    /// </summary>
    public class DialoguePanel : PanelBase
    {
        private TMP_Text speakerText;
        private TMP_Text contentText;
        private Button continueButton;
        private Transform choicesRoot;
        private Button choiceButtonPrefab;

        protected override void EventInit()
        {
            DialogueSystem.Instance.OnNodeChanged += ShowNode;
            DialogueSystem.Instance.OnDialogueEnd += HandleDialogueEnd;
        }

        protected override void ComponentInit()
        {
            speakerText = transform.Find("Speaker")?.GetComponent<TMP_Text>();
            contentText = transform.Find("Content")?.GetComponent<TMP_Text>();
            continueButton = transform.Find("ContinueBtn")?.GetComponent<Button>();
            choicesRoot = transform.Find("Choices");
            choiceButtonPrefab = transform.Find("ChoiceButtonPrefab")?.GetComponent<Button>();

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(() => DialogueSystem.Instance.Continue());
            }
        }

        private void ShowNode(DialogueNode node)
        {
            if (node == null)
                return;

            if (speakerText != null)
                speakerText.text = node.speakerName;

            StopAllCoroutines();
            StartCoroutine(TypewriterEffect(node.content ?? string.Empty));

            DialogueChoice[] choices = node.choices ?? System.Array.Empty<DialogueChoice>();
            if (continueButton != null)
                continueButton.gameObject.SetActive(choices.Length == 0);
            if (choicesRoot == null)
                return;

            for (int i = choicesRoot.childCount - 1; i >= 0; i--)
            {
                if (choiceButtonPrefab != null && choicesRoot.GetChild(i) == choiceButtonPrefab.transform)
                    continue;

                Destroy(choicesRoot.GetChild(i).gameObject);
            }

            if (choiceButtonPrefab == null)
                return;

            for (int i = 0; i < choices.Length; i++)
            {
                int choiceIndex = i;
                Button button = Instantiate(choiceButtonPrefab, choicesRoot);
                button.gameObject.SetActive(true);

                TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                    buttonText.text = choices[i].text;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => DialogueSystem.Instance.SelectChoice(choiceIndex));
            }
        }

        private IEnumerator TypewriterEffect(string text)
        {
            if (contentText == null)
                yield break;

            contentText.text = string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                contentText.text += text[i];
                yield return new WaitForSeconds(0.03f);
            }
        }

        private void HandleDialogueEnd()
        {
            DestroyPanel();
        }

        private void OnDestroy()
        {
            DialogueSystem.Instance.OnNodeChanged -= ShowNode;
            DialogueSystem.Instance.OnDialogueEnd -= HandleDialogueEnd;
        }
    }
}
