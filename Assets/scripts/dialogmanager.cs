using System.Collections;
using UnityEngine;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI")]
    public GameObject dialoguePanel;
    public TMP_Text characterNameText;
    public TMP_Text dialogueText;

    [Header("Settings")]
    public float typingSpeed = 0.03f;

    private Dialogue currentDialogue;
    private int currentIndex;
    private Coroutine typingRoutine;
    private bool isTyping;

    public bool IsDialogueActive => dialoguePanel != null && dialoguePanel.activeSelf;

    void Awake()
    {
        Instance = this;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    void Update()
    {
        if (dialoguePanel == null || !dialoguePanel.activeSelf)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isTyping)
            {
                StopCoroutine(typingRoutine);

                dialogueText.text = currentDialogue.lines[currentIndex].text;

                isTyping = false;
            }
            else
            {
                NextLine();
            }
        }
    }

    public void StartDialogue(Dialogue dialogue)
    {
        if (dialoguePanel == null || characterNameText == null || dialogueText == null ||
            dialogue == null || dialogue.lines == null || dialogue.lines.Length == 0) return;
        currentDialogue = dialogue;
        currentIndex = 0;

        dialoguePanel.SetActive(true);

        ShowLine();
    }

    void ShowLine()
    {
        DialogueLine line = currentDialogue.lines[currentIndex];

        characterNameText.text = line.characterName;

        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        typingRoutine = StartCoroutine(TypeSentence(line.text));
    }

    IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;

        dialogueText.text = "";

        foreach (char c in sentence)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }

    void NextLine()
    {
        currentIndex++;

        if (currentIndex >= currentDialogue.lines.Length)
        {
            EndDialogue();
            return;
        }

        ShowLine();
    }

    void EndDialogue()
    {
        dialoguePanel.SetActive(false);
    }
}
