using System;
using UnityEngine;

public class GuardBehaviorTree
{
    private BTNode _root;
    private BTContext _ctx;

    public GuardBehaviorTree(BTContext context)
    {
        _ctx = context;
        _root = BuildTree();
    }

    public BTContext Evaluate(PlayerIntent intent)
    {
        _ctx.LastIntent = intent;
        _ctx.ResultState = GuardState.Neutral;
        _ctx.OverrideDialogueHint = null;
        _ctx.Emotions.ApplyDecay(); 
        _root.Tick(_ctx);
        return _ctx;
    }

    private BTNode BuildTree()
    {
        return new Selector(

            new Sequence(
                new Condition("Max suspicion", ctx => ctx.Emotions.Suspicion >= 90f),
                new Action("→ Alert from suspicion max", ctx => {
                    ctx.ResultState = GuardState.Alert;
                    ctx.OverrideDialogueHint = "You've had enough. This prisoner is clearly dangerous or lying. Call for backup now.";
                    return BTStatus.Success;
                })
            ),

            new Sequence(
                new Condition("Fear critical", ctx => ctx.Emotions.Fear >= 75f),
                new Action("→ Alert", ctx => {
                    ctx.ResultState = GuardState.Alert;
                    ctx.OverrideDialogueHint = "Call for backup immediately, you are scared.";
                    return BTStatus.Success;
                })
            ),

            new Sequence(
                new Condition("Suspicious too long", ctx =>
                    ctx.FSM.CurrentState == GuardState.Suspicious &&
                    ctx.FSM.TurnsInCurrentState >= 3),
                new Action("→ Alert from suspicion", ctx => {
                    ctx.ResultState = GuardState.Alert;
                    ctx.OverrideDialogueHint = "You've had enough. Call for backup.";
                    return BTStatus.Success;
                })
            ),

            new Sequence(
                new Condition("Same strategy spammed", ctx =>
                    ctx.GetIntentCount(ctx.LastIntent) >= 3),
                new Action("Resist spam", ctx => {
                    ctx.Emotions.ApplyDelta(new EmotionDelta { Suspicion = +15f });
                    ctx.OverrideDialogueHint =
                        $"You've noticed they keep using the same tactic. Call it out directly and refuse.";

                    return BTStatus.Failure; 
                })
            ),

            new Sequence(
                new Condition("Is Corrupt", ctx => ctx.Personality == GuardPersonality.Corrupt),
                new Condition("Is Bribe", ctx => ctx.LastIntent == PlayerIntent.Bribe),
                new Action("Corrupt responds to bribe", ctx => {
                    ctx.Emotions.ApplyDelta(new EmotionDelta
                    { Suspicion = -25f, Respect = +20f, Sympathy = +10f });
                    ctx.OverrideDialogueHint =
                        "You're tempted by the bribe. Respond cautiously but interested.";
                    return BTStatus.Failure;
                })
            ),

            new Sequence(
                new Condition("Is Paranoid", ctx => ctx.Personality == GuardPersonality.Paranoid),
                new Condition("Positive intent", ctx =>
                    ctx.LastIntent == PlayerIntent.Appeal ||
                    ctx.LastIntent == PlayerIntent.Humor ||
                    ctx.LastIntent == PlayerIntent.Reason),
                new Action("Paranoid distrusts everything", ctx => {
                    ctx.Emotions.ApplyDelta(new EmotionDelta { Suspicion = +12f, Fear = +5f });
                    ctx.OverrideDialogueHint =
                        "You are paranoid. Even kind words make you more suspicious. React defensively.";
                    return BTStatus.Failure;
                })
            ),

            new Sequence(
                new Condition("Is Empathetic", ctx => ctx.Personality == GuardPersonality.Empathetic),
                new Condition("Emotional appeal", ctx =>
                    ctx.LastIntent == PlayerIntent.Appeal ||
                    ctx.LastIntent == PlayerIntent.Guilt),
                new Action("Empathetic responds strongly", ctx => {
                    ctx.Emotions.ApplyDelta(new EmotionDelta
                    { Sympathy = +20f, Guilt = +15f, Suspicion = -10f });
                    ctx.OverrideDialogueHint =
                        "You are moved by their words. Show visible hesitation and empathy.";
                    return BTStatus.Failure;
                })
            ),

            new Sequence(
                new Condition("Is Strict", ctx => ctx.Personality == GuardPersonality.Strict),
                new Condition("Non-logical intent", ctx =>
                    ctx.LastIntent == PlayerIntent.Appeal ||
                    ctx.LastIntent == PlayerIntent.Humor ||
                    ctx.LastIntent == PlayerIntent.Bribe),
                new Action("Strict dismisses emotions", ctx => {
                    ctx.Emotions.ApplyDelta(new EmotionDelta { Suspicion = +8f });
                    ctx.OverrideDialogueHint =
                        "You are strict and unmoved by emotions or bribes. Respond by the book.";
                    return BTStatus.Failure;
                })
            ),

            new Sequence(
                new Condition("Sympathetic but suspicious", ctx =>
                    ctx.Emotions.Sympathy >= 50f && ctx.Emotions.Suspicion > 35f),
                new Action("Partial offer", ctx => {
                    ctx.ResultState = GuardState.Wavering;
                    ctx.OverrideDialogueHint =
                        "You feel for them but can't fully trust them. Offer a compromise — " +
                        "maybe call a supervisor, but you won't open the cell yourself.";
                    return BTStatus.Success;
                })
            ),

            new Sequence(
                new Condition("Wavering too long", ctx =>
                    ctx.FSM.CurrentState == GuardState.Wavering &&
                    ctx.FSM.TurnsInCurrentState >= 2),
                new Action("→ Convinced by attrition", ctx => {
                    ctx.ResultState = GuardState.Convinced;
                    ctx.OverrideDialogueHint =
                        "You've been hesitating long enough. Finally give in, reluctantly.";
                    return BTStatus.Success;
                })
            ),

            new Sequence(
                new Condition("Low suspicion", ctx => ctx.Emotions.Suspicion <= 25f),
                new Condition("Strong positive emotion", ctx =>
                    ctx.Emotions.Sympathy >= 45f ||
                    ctx.Emotions.Respect >= 45f ||
                    ctx.Emotions.Guilt >= 40f),
                new Action("→ Convinced", ctx => {
                    ctx.ResultState = GuardState.Convinced;
                    ctx.OverrideDialogueHint =
                        "You are fully convinced. Let them go, perhaps a bit reluctantly.";
                    return BTStatus.Success;
                })
            ),

            new Action("FSM fallback", ctx => {
                ctx.ResultState = ctx.FSM.CurrentState;
                return BTStatus.Success;
            })
        );
    }
}