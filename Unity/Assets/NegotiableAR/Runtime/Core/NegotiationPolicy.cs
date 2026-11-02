using System;

namespace NegotiableAR
{
    // Pure C#: all time comes from the caller, so sessions can be replayed in tests.
    public sealed class NegotiationPolicy
    {
        readonly AgentConfig config;
        Observation observation;
        double observedAt = double.NegativeInfinity;
        double lastIntervention = double.NegativeInfinity;
        double offerUntil;
        double quietUntil;
        public AssistanceMode Mode { get; set; } = AssistanceMode.Negotiated;
        public bool OfferPending { get; private set; }
        public float CooldownSeconds { get; private set; }
        public int IgnoredOffers { get; private set; }

        public NegotiationPolicy(AgentConfig config)
        {
            this.config = config;
            CooldownSeconds = config.cooldownSeconds;
        }

        public void Observe(Observation value, double captureTime)
        {
            observation = value;
            observedAt = captureTime;
        }

        bool Fresh(double now) => observation != null && now - observedAt <= config.staleSeconds &&
            now >= observedAt && observation.confidence >= config.minimumConfidence;

        public Decision Tick(double now, ActivityEvidence activity)
        {
            if (OfferPending)
            {
                if (now < offerUntil) return Decision.Silent("Awaiting explicit response; silence is not consent");
                OfferPending = false;
                IgnoredOffers++;
                BackOff();
                lastIntervention = now;
                return Decision.Silent("Unanswered offer expired; intent remains unknown");
            }
            if (Mode != AssistanceMode.Negotiated) return Decision.Silent("User selected " + Mode);
            if (now < quietUntil || now - lastIntervention < CooldownSeconds) return Decision.Silent("Cooldown");
            if (!Fresh(now)) return Decision.Silent("Missing, uncertain, or stale perception");
            if (!activity.HandsReliable || !activity.LookingAtWorkspace) return Decision.Silent("Insufficient engagement evidence");
            if (activity.HandsMoving || now - activity.LastMovementTime < config.pauseSeconds) return Decision.Silent("Preserve active exploration");
            OfferPending = true;
            offerUntil = now + config.offerTimeoutSeconds;
            lastIntervention = now;
            return new Decision { Action = AgentAction.Offer, Text = "Would you like a small hint, or time to explore?", Reason = "Pause is ambiguous; ask rather than infer difficulty", What = "W09/W17", When = "T08 (possible pause)" };
        }

        public Decision Signal(UserSignal signal, double now)
        {
            // An expired offer cannot be accepted even when input arrives before the next Tick.
            bool hadOffer = OfferPending && now < offerUntil;
            switch (signal)
            {
                case UserSignal.Quiet:
                    Mode = AssistanceMode.Quiet; OfferPending = false;
                    return Reply("I'll stay quiet until you ask for help or resume.", "Explicit quiet preference");
                case UserSignal.Resume:
                    Mode = AssistanceMode.Negotiated; lastIntervention = now;
                    return Reply("I'll offer occasional help again.", "Explicit resume preference");
                case UserSignal.MoreHelp:
                    Mode = AssistanceMode.Negotiated;
                    CooldownSeconds = Math.Max(8, CooldownSeconds * 0.75f);
                    return Reply("I'll offer help a little more often.", "Explicit frequency preference");
                case UserSignal.LessHelp:
                    BackOff(); lastIntervention = now; OfferPending = false;
                    return Reply("I'll give you more time between offers.", "Explicit frequency preference");
                case UserSignal.Later:
                case UserSignal.Decline:
                    if (!hadOffer) return Decision.Silent("No active offer to decline");
                    OfferPending = false; BackOff(); lastIntervention = now;
                    quietUntil = now + CooldownSeconds;
                    return Reply(signal == UserSignal.Later ? "Take your time." : "Okay, I'll give you space.", "Local refusal; do not infer a permanent preference");
                case UserSignal.Accept:
                    if (!hadOffer) return Decision.Silent("No active offer to accept");
                    break;
                case UserSignal.RequestHelp:
                    break;
                default: return Decision.Silent("Unknown input");
            }
            OfferPending = false; lastIntervention = now;
            if (!Fresh(now) || string.IsNullOrWhiteSpace(observation.suggestedHint))
                return new Decision { Action = AgentAction.Hint, Text = "What are you trying to make, and what would you like help with?", Reason = "Requested help without grounded context", What = "W08", When = "T01" };
            return new Decision { Action = AgentAction.Hint, Text = observation.suggestedHint, Reason = "Explicit request or accepted offer using recent evidence", What = "W10", When = "T01" };
        }

        void BackOff() => CooldownSeconds = Math.Min(config.maximumCooldownSeconds, CooldownSeconds * 1.5f);
        static Decision Reply(string text, string reason) => new Decision { Action = AgentAction.Acknowledge, Text = text, Reason = reason, What = "W17", When = "explicit preference" };
    }
}
