namespace NegotiableAR
{
    public enum PointerPhase { None, Pressed, Held, Released }

    // A pointer must release before another pointer can press. This prevents a hand
    // tracking dropout or a switch of hands/controllers from becoming a new click.
    public sealed class PointerGesture
    {
        int source;
        bool held;
        bool waitForRelease;

        public PointerPhase Update(int sourceId, bool down)
        {
            if (held && (sourceId != source || !down))
            {
                held = false;
                waitForRelease = sourceId != source;
                source = sourceId;
                return PointerPhase.Released;
            }
            source = sourceId;
            if (waitForRelease)
            {
                if (sourceId != 0 && !down) waitForRelease = false;
                return PointerPhase.None;
            }
            if (!down || sourceId == 0) return PointerPhase.None;
            if (held) return PointerPhase.Held;
            held = true;
            return PointerPhase.Pressed;
        }
    }
}
