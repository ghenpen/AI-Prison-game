using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUIController : MonoBehaviour
{
    [Header("References")]
    public GameManagerWrapper gameManagerWrapper;
    public CCTVCameraManager cctv;
    public CharacterAnimationController animations;

    [Header("Dialogue Box")]
    public GameObject dialogueBox;
    public TMP_Text speakerName;
    public TMP_Text dialogueText;
    public RawImage portrait;

    [Header("Portrait Cameras")]
    public RenderTexture prisonerPortraitRT;
    public RenderTexture guardPortraitRT;
    public Camera prisonerPortraitCam;
    public Camera guardPortraitCam;

    [Header("Input")]
    public TMP_InputField playerInput;
    public Button sendButton;
    public Button nextButton;

    [Header("Status")]
    public TMP_Text emotionStatus;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;

    [Header("Typewriter")]
    public float typewriterSpeed = 0.03f;

    private GameManager _game;
    private bool _isProcessing = false;

    private bool _isTyping = false;
    private string _fullGuardText = "";

    void Start()
    {
        _game = gameManagerWrapper.Instance;

        _game.OnGuardResponse += msg => Debug.Log($"  GUARD    |  {msg}");
        _game.OnIntentDetected += i => Debug.Log($"  INTENT   |  {i}");
        _game.OnStateChanged += s => Debug.Log($"  STATE    |  {s}");
        _game.OnDQNAction += a => Debug.Log($"  DQN ACT  |  {a}");
        _game.OnDQNReward += r => Debug.Log($"  DQN REW  |  {r:+0.00;-0.00}");
        _game.OnEmotionsUpdated += e => Debug.Log($"  EMOTIONS |  {e.ToPromptString()}");

        _game.OnGuardResponse += OnGuardResponse;
        _game.OnEmotionsUpdated += OnEmotionsUpdated;
        _game.OnStateChanged += OnStateChanged;
        _game.OnGameWon += OnGameWon;
        _game.OnGameLost += OnGameLost;

        sendButton.onClick.AddListener(OnSendClicked);
        nextButton.onClick.AddListener(OnNextClicked);
        nextButton.gameObject.SetActive(false);

        playerInput.onSubmit.AddListener(_ => OnSendClicked());

        gameOverPanel.SetActive(false);

        prisonerPortraitCam.gameObject.SetActive(true);
        guardPortraitCam.gameObject.SetActive(false);
        portrait.texture = prisonerPortraitRT;

        ShowPlayerInput(); 
    }

    void Update()
    {
        if (_isTyping &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
            StopAllCoroutines();
            dialogueText.text = _fullGuardText;
            _isTyping = false;

            if (!_game.IsGameOver())
                nextButton.gameObject.SetActive(true);
        }
    }

    private void ShowPlayerInput()
    {
        dialogueBox.SetActive(true);
        playerInput.gameObject.SetActive(true);
        sendButton.gameObject.SetActive(true);
        dialogueText.gameObject.SetActive(false);
        speakerName.text = "YOU";
        playerInput.ActivateInputField();
    }

    private void ShowGuardDialogue(string text)
    {
        dialogueBox.SetActive(true);
        playerInput.gameObject.SetActive(false);
        sendButton.gameObject.SetActive(false);
        dialogueText.gameObject.SetActive(true);
        speakerName.text = "GUARD";
        dialogueText.text = text;
    }

    private void OnSendClicked()
    {
        if (_isProcessing) return;
        string msg = playerInput.text.Trim();
        if (string.IsNullOrWhiteSpace(msg)) return;

        playerInput.text = "";
        _isProcessing = true;

        animations.OnPlayerSend();
        cctv.SetGuardTurn();
        ShowPortrait(isPrisoner: false); 
        ShowGuardDialogue("...");        

        _ = ProcessInput(msg);
    }

    private async Task ProcessInput(string msg)
    {
        await _game.ProcessPlayerInput(msg);
    }

    private void OnGuardResponse(string response)
    {
        ShowPortrait(isPrisoner: false);
        ShowGuardDialogue(response);
        StopAllCoroutines();
        StartCoroutine(TypewriterEffect("GUARD", response, () =>
        {
            if (!_game.IsGameOver())
            {
                nextButton.gameObject.SetActive(true);
            }
        }));
    }

    private void OnNextClicked()
    {
        nextButton.gameObject.SetActive(false);
        cctv.SetPrisonerTurn();
        ShowPortrait(isPrisoner: true);
        ShowPlayerInput();
        _isProcessing = false;
    }

    private void OnStateChanged(GuardState state)
    {
        animations.OnGuardResponse(state);
    }

    private void OnEmotionsUpdated(EmotionState emotions)
    {
        if (emotionStatus != null)
            emotionStatus.text =
                $"Suspicion {emotions.Suspicion:F0}  " +
                $"Sympathy {emotions.Sympathy:F0}  " +
                $"Fear {emotions.Fear:F0}  " +
                $"Guilt {emotions.Guilt:F0}";
    }

    private void OnGameWon()
    {
        animations.OnGameWon();
        StartCoroutine(ShowGameOver("You convinced the guard.\nYou escaped."));
    }

    private void OnGameLost()
    {
        animations.OnGameLost();
        StartCoroutine(ShowGameOver("The guard called for backup.\nYou failed."));
    }

    private void ShowPortrait(bool isPrisoner)
    {
        if (isPrisoner)
        {
            prisonerPortraitCam.gameObject.SetActive(true);
            guardPortraitCam.gameObject.SetActive(false);
            portrait.texture = prisonerPortraitRT;
        }
        else
        {
            prisonerPortraitCam.gameObject.SetActive(false);
            guardPortraitCam.gameObject.SetActive(true);
            portrait.texture = guardPortraitRT;
        }
    }

    private void ShowDialogue(string speaker, string text)
    {
        dialogueBox.SetActive(true);
        speakerName.text = speaker;
        dialogueText.text = text;
    }

    private IEnumerator TypewriterEffect(
        string speaker,
        string fullText,
        System.Action onComplete)
    {
        dialogueBox.SetActive(true);
        speakerName.text = speaker;
        dialogueText.text = "";

        _isTyping = true;
        _fullGuardText = fullText;

        foreach (char c in fullText)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typewriterSpeed);
        }

        _isTyping = false;
        onComplete?.Invoke();
    }

    private IEnumerator ShowGameOver(string message)
    {
        yield return new WaitForSeconds(2f);
        gameOverPanel.SetActive(true);
        gameOverText.text = message;
    }
}