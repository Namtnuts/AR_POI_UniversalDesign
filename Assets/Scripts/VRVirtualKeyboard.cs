using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class VRVirtualKeyboard : MonoBehaviour
{
    [Header("Przypisania")]
    [Tooltip("Twoje pole tekstowe do wpisywania ID")]
    public TMP_InputField targetInputField;

    [Header("Ustawienia Wyglądu")]
    public int keyFontSize = 18;
    public Color keyColor = new Color(0.2f, 0.2f, 0.25f, 1f);
    public Color specialKeyColor = new Color(0.35f, 0.2f, 0.2f, 1f);
    public Color confirmKeyColor = new Color(0.15f, 0.45f, 0.2f, 1f);

    private CanvasGroup canvasGroup;
    private bool isBuilt = false;

    private readonly string[] keyboardLayout = new string[]
    {
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
        "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P",
        "A", "S", "D", "F", "G", "H", "J", "K", "L", "_",
        "Z", "X", "C", "V", "B", "N", "M", "-", "BACK", "OK"
    };

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        BuildKeyboardUI();
        HideKeyboard();
    }

    private void Start()
    {
        if (targetInputField != null)
        {
            targetInputField.onSelect.AddListener((val) => ShowKeyboard());

            EventTrigger trigger = targetInputField.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = targetInputField.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerClick;
            entry.callback.AddListener((data) => ShowKeyboard());
            trigger.triggers.Add(entry);
        }
    }

    public void ShowKeyboard()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    public void HideKeyboard()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void BuildKeyboardUI()
    {
        if (isBuilt) return;

        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        GridLayoutGroup grid = gameObject.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = gameObject.AddComponent<GridLayoutGroup>();

        // Zmniejszone wymiary klawiszy i odstępy
        grid.cellSize = new Vector2(50, 42);
        grid.spacing = new Vector2(4, 4);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 10;

        foreach (string key in keyboardLayout)
        {
            CreateKeyButton(key);
        }

        isBuilt = true;
    }

    private void CreateKeyButton(string character)
    {
        GameObject btnObj = new GameObject($"Key_{character}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(transform, false);

        Image img = btnObj.GetComponent<Image>();
        img.raycastTarget = true;
        if (character == "BACK") img.color = specialKeyColor;
        else if (character == "OK") img.color = confirmKeyColor;
        else img.color = keyColor;

        Button btn = btnObj.GetComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.5f, 0.5f, 0.6f, 1f);
        colors.pressedColor = new Color(0.1f, 0.6f, 0.9f, 1f);
        btn.colors = colors;

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = character;
        tmp.fontSize = (character.Length > 1) ? keyFontSize - 5 : keyFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        btn.onClick.AddListener(() => OnKeyClicked(character));
    }

    private void OnKeyClicked(string character)
    {
        if (targetInputField == null) return;

        if (character == "BACK")
        {
            if (targetInputField.text.Length > 0)
            {
                targetInputField.text = targetInputField.text.Substring(0, targetInputField.text.Length - 1);
            }
        }
        else if (character == "OK")
        {
            if (UXResearchLogger.Instance != null)
            {
                UXResearchLogger.Instance.participantID = targetInputField.text;
                Debug.Log($"<color=green>[UX Logger]:</color> Zatwierdzono Participant ID: {targetInputField.text}");
            }

            HideKeyboard();
        }
        else
        {
            targetInputField.text += character;
        }

        if (UXResearchLogger.Instance != null)
        {
            UXResearchLogger.Instance.participantID = targetInputField.text;
        }
    }
}