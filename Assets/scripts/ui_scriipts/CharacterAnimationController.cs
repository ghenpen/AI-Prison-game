using UnityEngine;

public class CharacterAnimationController : MonoBehaviour
{
    [Header("Characters")]
    public Animator prisonerAnimator;
    public Animator guardAnimator;

    public void OnPlayerSend()
    {
        prisonerAnimator.SetTrigger("Talk");
    }

    public void OnGuardResponse(GuardState state)
    {
        prisonerAnimator.SetTrigger("Idle");

        string trigger = state switch
        {
            GuardState.Suspicious => "Suspicious",
            GuardState.Wavering => "Wavering",
            GuardState.Curious => "Curious",
            GuardState.Amused => "Amused",
            GuardState.Alert => "Alert",
            _ => "Idle"
        };
        guardAnimator.SetTrigger(trigger);
    }

    public void OnGameWon()
    {
        guardAnimator.SetTrigger("Convinced");
        prisonerAnimator.SetTrigger("Win");
    }

    public void OnGameLost()
    {
        guardAnimator.SetTrigger("GameLost");
        prisonerAnimator.SetTrigger("Lose");
    }
}