using System;

namespace NegotiableAR
{
    [Serializable] public class TaskProfile
    {
        public string name = "Creative tabletop exploration";
        public string goal = "Explore an arrangement using the available pieces. The user decides what to make.";
        public string[] distinctiveItems = { "colored geometric pieces", "construction bricks" };
        public string constraints = "No predefined solution. Ask about intent before correcting creative choices.";
    }

    [Serializable] public class AgentConfig
    {
        public float pauseSeconds = 12;
        public float cooldownSeconds = 25;
        public float offerTimeoutSeconds = 10;
        public float staleSeconds = 8;
        public float minimumConfidence = 0.55f;
        public float maximumCooldownSeconds = 120;
    }

    [Serializable] public class ItemObservation
    {
        public string label;
        public float confidence;
        // Image coordinates: normalized, origin at TOP LEFT. Not world coordinates.
        public float x, y, width, height;
        public bool boundsValid;
    }

    [Serializable] public class Observation
    {
        public string frameId;
        public string summary;
        public string phase;
        public string visibleAction;
        public string suggestedHint;
        public float confidence;
        public ItemObservation[] items = Array.Empty<ItemObservation>();
        public float latencyMs;
        public string foregroundMode;
    }

    public enum AssistanceMode { Negotiated, OnRequest, Quiet }
    public enum UserSignal { RequestHelp, Accept, Decline, Later, MoreHelp, LessHelp, Quiet, Resume }
    public enum AgentAction { Silent, Offer, Hint, Acknowledge }

    public sealed class Decision
    {
        public AgentAction Action;
        public string Text, Reason, What, When;
        public static Decision Silent(string reason) => new Decision { Action = AgentAction.Silent, Text = "", Reason = reason, What = "W16", When = "withhold" };
    }

    public struct ActivityEvidence
    {
        public bool HandsReliable, HandsMoving, LookingAtWorkspace;
        public double LastMovementTime;
    }
}
