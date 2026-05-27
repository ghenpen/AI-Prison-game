using UnityEngine;

public class GameManagerWrapper : MonoBehaviour
{
    [Header("Ollama Settings")]
    public string ollamaUrl = "http://localhost:11434/api/generate";
    public string model = "llama3.1:latest";
    public GuardPersonality personality = GuardPersonality.Neutral;

    public GameManager Instance { get; private set; }

    void Awake()
    {
        Instance = new GameManager(ollamaUrl, model, personality);
    }
}