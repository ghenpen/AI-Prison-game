using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class GameManager
{
    private OllamaConnector _ollama;
    private EmotionState _emotions;
    private GuardFSM _fsm;
    private IntentDecisionTree _dt;
    private GuardBehaviorTree _bt;
    private BTContext _btCtx;
    private DQNAgent _dqn;
    private List<string> _conversationHistory = new List<string>();

    public event System.Action<string> OnGuardResponse;
    public event System.Action<EmotionState> OnEmotionsUpdated;
    public event System.Action<GuardState> OnStateChanged;
    public event System.Action<PlayerIntent> OnIntentDetected;
    public event System.Action<DQNAction> OnDQNAction;
    public event System.Action<float> OnDQNReward;
    public event System.Action OnGameWon;
    public event System.Action OnGameLost;

    private bool _gameOver = false;
    private int _totalTurns = 0;

    public GameManager(
        string ollamaUrl = "http://127.0.0.1:11434/api/generate",
        string model = "llama3.1:latest",
        GuardPersonality personality = GuardPersonality.Neutral)
    {
        _ollama = new OllamaConnector { ollamaUrl = ollamaUrl, model = model };
        _dt = new IntentDecisionTree();
        _dqn = new DQNAgent();

        _emotions = new EmotionState
        {
            SympathyBias = 1.0f,
            FearBias = 1.0f,
            GuiltBias = 1.0f,
            AmusementBias = 1.0f,
            RespectBias = 1.0f
        };

        _fsm = new GuardFSM(_emotions);

        _btCtx = new BTContext
        {
            Emotions = _emotions,
            FSM = _fsm,
            Personality = personality
        };

        _bt = new GuardBehaviorTree(_btCtx);

        _dqn.BeginEpisode(_emotions, PlayerIntent.Unknown, 0, _fsm);
    }

    public async Task ProcessPlayerInput(string playerInput)
    {
        if (_gameOver || string.IsNullOrWhiteSpace(playerInput))
            return;

        _totalTurns++;

        PlayerIntent intent = await _ollama.ClassifyIntent(playerInput);
        OnIntentDetected?.Invoke(intent);

        DQNAction dqnAction = _dqn.CurrentAction;
        _dqn.ApplyActionToFSM(dqnAction, _fsm, _emotions, intent, _dt);
        OnDQNAction?.Invoke(dqnAction);

        EmotionDelta baseDelta = _dt.GetBaseDelta(intent);
        _emotions.ApplyDelta(baseDelta);
        OnEmotionsUpdated?.Invoke(_emotions);

        _btCtx.RecordIntent(intent);
        _fsm.Evaluate();
        BTContext btResult = _bt.Evaluate(intent);
        OnStateChanged?.Invoke(btResult.ResultState);
        OnEmotionsUpdated?.Invoke(_emotions);

        bool isTerminal = btResult.ResultState == GuardState.Convinced
                       || btResult.ResultState == GuardState.Alert;

        float reward = _dqn.CalculateReward(
            btResult.ResultState,
            gameOver: isTerminal,
            gameWon: btResult.ResultState == GuardState.Convinced);

        OnDQNReward?.Invoke(reward);

        _dqn.Step(_emotions, intent, _fsm.TurnsInCurrentState, _fsm, reward, done: isTerminal);

        string dialogueHint = isTerminal
            ? GetStateHint(btResult.ResultState)
            : btResult.OverrideDialogueHint ?? GetStateHint(btResult.ResultState);

        _conversationHistory.Add($"Prisoner: {playerInput}");

        Task<EmotionDelta> emotionTask = isTerminal
            ? Task.FromResult<EmotionDelta>(null)
            : _ollama.GetEmotionDelta(playerInput, intent, _emotions, _fsm);

        Task<string> dialogueTask = _ollama.GetGuardDialogue(
            playerInput, _emotions, _fsm, _conversationHistory, dialogueHint);

        await Task.WhenAll(emotionTask, dialogueTask);

        if (!isTerminal && emotionTask.Result != null)
        {
            _emotions.ApplyDelta(ClampLLMCorrection(emotionTask.Result, baseDelta));
            OnEmotionsUpdated?.Invoke(_emotions);
        }

        string guardResponse = dialogueTask.Result;
        _conversationHistory.Add($"Guard: {guardResponse}");

        if (_conversationHistory.Count > 10)
            _conversationHistory.RemoveRange(0, _conversationHistory.Count - 10);

        OnGuardResponse?.Invoke(guardResponse);

        if (isTerminal)
        {
            _gameOver = true;
            _dqn.Save();

            if (btResult.ResultState == GuardState.Convinced)
                OnGameWon?.Invoke();
            else
                OnGameLost?.Invoke();
        }
    }

    private string GetStateHint(GuardState state) => state switch
    {
        GuardState.Convinced => "You are fully convinced. Let the prisoner go, reluctantly but clearly.",
        GuardState.Alert => "You are alarmed. Call for backup immediately.",
        GuardState.Wavering => "You are hesitant. Show visible doubt but don't commit yet.",
        GuardState.Suspicious => "You are suspicious. Be cold and skeptical.",
        GuardState.Amused => "You are amused but still professional.",
        GuardState.Curious => "You are curious. Ask a follow-up question.",
        _ => null
    };

    private EmotionDelta ClampLLMCorrection(EmotionDelta llm, EmotionDelta already) =>
        new EmotionDelta
        {
            Suspicion = Mathf.Clamp(llm.Suspicion - already.Suspicion, -10f, 10f),
            Sympathy = Mathf.Clamp(llm.Sympathy - already.Sympathy, -10f, 10f),
            Fear = Mathf.Clamp(llm.Fear - already.Fear, -10f, 10f),
            Guilt = Mathf.Clamp(llm.Guilt - already.Guilt, -10f, 10f),
            Amusement = Mathf.Clamp(llm.Amusement - already.Amusement, -10f, 10f),
            Respect = Mathf.Clamp(llm.Respect - already.Respect, -10f, 10f),
        };

    public EmotionState GetEmotions() => _emotions;
    public GuardState GetState() => _btCtx.ResultState;
    public bool IsGameOver() => _gameOver;
    public float GetEpsilon() => _dqn.Epsilon;
    public float GetLastLoss() => _dqn.LastLoss;
    public int GetTotalSteps() => _dqn.TotalSteps;
    public string GetResistance() => _fsm.ResistanceLevelToString();
    public DQNAction GetCurrentAction() => _dqn.CurrentAction;

    public void ResetGame()
    {
        _gameOver = false;
        _totalTurns = 0;
        _conversationHistory.Clear();
        _btCtx.ResetForNewSession();
        _fsm.Reset();
        _emotions.Reset(); 

        _btCtx.Emotions = _emotions;
        _btCtx.FSM = _fsm;
        _bt = new GuardBehaviorTree(_btCtx);

        _dqn.BeginEpisode(_emotions, PlayerIntent.Unknown, 0, _fsm);
    }
}