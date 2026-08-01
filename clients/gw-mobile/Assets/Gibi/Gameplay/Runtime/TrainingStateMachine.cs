// GW-ARCH-001 section 9.1 — Training state machine.
// Section 1.2 out of scope: "combat, pet injury, punishment, hunger loss, death".
// Every exit path here is kind: timeouts prompt gently or close warmly, never fail.
using Gibi.Core;

namespace Gibi.Gameplay
{
    public enum TrainingState { Ready, Cue, Action, Reward, Rest, Complete }

    public sealed class TrainingStateMachine
    {
        public const long CueTimeoutMs        = 10_000; // -> gentle prompt
        public const long RewardTimeoutMs     =  8_000; // -> auto-kind close
        public const long RestSuggestionMs    = 30_000;
        public const long ActionAbortGraceMs  =  1_000; // clip + 1 s -> abort safely
        public const int  AttemptsBeforeRest  = 3;

        private readonly IMonotonicClock _clock;
        private long _stateEnteredMs;
        private int  _attempts;

        public TrainingState State { get; private set; } = TrainingState.Ready;
        public bool GentlePromptShown { get; private set; }

        public TrainingStateMachine(IMonotonicClock clock)
        { _clock = clock; _stateEnteredMs = clock.ElapsedMilliseconds; }

        private void Enter(TrainingState s)
        { State = s; _stateEnteredMs = _clock.ElapsedMilliseconds; GentlePromptShown = false; }

        public long TimeInStateMs => _clock.ElapsedMilliseconds - _stateEnteredMs;

        /// <summary>READY -> CUE. Requires a stable safe surface and an available pet.</summary>
        public bool StartLesson(bool stableSafeSurface, bool petAvailable)
        {
            if (State != TrainingState.Ready || !stableSafeSurface || !petAvailable) return false;
            Enter(TrainingState.Cue);
            return true;
        }

        /// <summary>CUE -> ACTION on a recognized, validated cue.</summary>
        public bool RecognizeCue()
        {
            if (State != TrainingState.Cue) return false;
            _attempts++;
            Enter(TrainingState.Action);
            return true;
        }

        /// <summary>ACTION -> REWARD when the action completes successfully.</summary>
        public bool CompleteAction(bool success)
        {
            if (State != TrainingState.Action) return false;
            Enter(success ? TrainingState.Reward : TrainingState.Cue);
            return true;
        }

        /// <summary>REWARD -> REST or COMPLETE. Reward is always given, never withheld.</summary>
        public bool Reward()
        {
            if (State != TrainingState.Reward) return false;
            Enter(_attempts >= AttemptsBeforeRest ? TrainingState.Rest : TrainingState.Cue);
            return true;
        }

        public void EndLesson() => Enter(TrainingState.Complete);
        public void Cancel()    => Enter(TrainingState.Ready);

        /// <summary>
        /// Drive timeouts. Returns a localization key when the UI should surface
        /// something, otherwise null. Never returns an error state.
        /// </summary>
        public string Tick(long clipLengthMs = 0)
        {
            long t = TimeInStateMs;
            switch (State)
            {
                case TrainingState.Cue:
                    if (t >= CueTimeoutMs && !GentlePromptShown)
                    { GentlePromptShown = true; return "training.prompt.gentle"; }
                    break;

                case TrainingState.Action:
                    if (t >= clipLengthMs + ActionAbortGraceMs)
                    { Enter(TrainingState.Cue); return "training.action.abort_safe"; }
                    break;

                case TrainingState.Reward:
                    if (t >= RewardTimeoutMs)
                    { Enter(TrainingState.Rest); return "training.reward.auto_kind_close"; }
                    break;

                case TrainingState.Rest:
                    if (t >= RestSuggestionMs)
                    { _attempts = 0; Enter(TrainingState.Ready); return "training.rest.suggestion"; }
                    break;
            }
            return null;
        }
    }
}
