using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string characterName;

    [TextArea(3, 8)]
    public string text;
}