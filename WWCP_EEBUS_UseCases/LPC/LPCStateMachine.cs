/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of WWCP EEBUS <https://github.com/OpenChargingCloud/WWCP_EEBUS>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace cloud.charging.open.protocols.EEBUS.UseCases.LPC
{

    /// <summary>
    /// The states of a controllable system (LPC 1.0.0, section 2.3.2).
    /// </summary>
    public enum LPCState
    {

        /// <summary>
        /// Just (re)started. Limited by the failsafe value; the active power
        /// consumption limit is deactivated ([LPC-901], [LPC-903]).
        /// </summary>
        Init,

        /// <summary>
        /// Not limited, but still controlled by the energy guard. The limit is
        /// deactivated ([LPC-009/2]).
        /// </summary>
        UnlimitedControlled,

        /// <summary>
        /// Limited by the active power consumption limit ([LPC-009/1]).
        /// </summary>
        Limited,

        /// <summary>
        /// Nobody is controlling us and we are careful about it: limited by the
        /// failsafe value ([LPC-911], [LPC-912]).
        /// </summary>
        FailsafeState,

        /// <summary>
        /// Nobody is controlling us and we have waited long enough: not limited
        /// at all ([LPC-921], [LPC-922], [LPC-906]).
        /// </summary>
        UnlimitedAutonomous

    }


    /// <summary>
    /// Which limit a controllable system is holding itself to.
    /// </summary>
    public enum LPCLimitation
    {

        /// <summary>No limitation: normal operation.</summary>
        None,

        /// <summary>The active power consumption limit of the energy guard.</summary>
        ActivePowerConsumptionLimit,

        /// <summary>The failsafe consumption active power limit.</summary>
        FailsafeConsumptionActivePowerLimit

    }


    /// <summary>
    /// A state of the controllable system was left for another one.
    /// </summary>
    /// <param name="From">The state it was in.</param>
    /// <param name="To">The state it is in now.</param>
    /// <param name="Transition">The number of the transition in section 2.3.3, or 0 for the start.</param>
    /// <param name="Reason">Which rule of the specification caused it.</param>
    /// <param name="Timestamp">When it happened.</param>
    public sealed record LPCTransition(LPCState        From,
                                       LPCState        To,
                                       UInt32          Transition,
                                       String          Reason,
                                       DateTimeOffset  Timestamp)
    {

        /// <summary>Return a text representation of this transition.</summary>
        public override String ToString()

            => $"{Transition}: {From} -> {To} ({Reason})";

    }


    /// <summary>
    /// The state machine of the controllable system (LPC 1.0.0, section 2.3).
    ///
    /// This is the part of the use case which is normative and which the Go
    /// reference implementation leaves to the application: it exposes the data
    /// points and lets whoever uses them decide what state the device is in.
    /// For a test bench that is the wrong way round - the states and their
    /// transitions are exactly what a conformance test asks about, and the
    /// device under test is somebody else's.
    ///
    /// Everything it decides comes from three things and nothing else: when the
    /// last heartbeat arrived, what the last write of the limit said, and what
    /// time it is now. It has no timer of its own - <see cref="Check"/> is
    /// called by whoever drives the time - because a device which has gone quiet
    /// gives nothing to react to, so somebody has to look.
    /// </summary>
    public class LPCStateMachine
    {

        #region Data

        private readonly Lock            stateLock            = new ();

        private          DateTimeOffset? lastHeartbeat;

        private          DateTimeOffset  enteredState;

        private          DateTimeOffset? heartbeatInThisState;

        #endregion

        #region Properties

        /// <summary>
        /// Where the time comes from.
        /// </summary>
        public TimeProvider   TimeProvider              { get; }

        /// <summary>
        /// The state the controllable system is in.
        /// </summary>
        public LPCState       State                     { get; private set; } = LPCState.Init;

        /// <summary>
        /// How long the controllable system stays in its failsafe state before
        /// it may decide that nobody is coming back ([LPC-022]).
        /// </summary>
        public TimeSpan       FailsafeDurationMinimum   { get; set; } = LimitationOfPowerConsumption.FailsafeDurationMinimumLowerBound;

        /// <summary>
        /// Which limit the controllable system is holding itself to right now
        /// (LPC 1.0.0, Table 1).
        /// </summary>
        public LPCLimitation  Limitation

            => State switch {
                   LPCState.Init                 => LPCLimitation.FailsafeConsumptionActivePowerLimit,
                   LPCState.FailsafeState        => LPCLimitation.FailsafeConsumptionActivePowerLimit,
                   LPCState.Limited              => LPCLimitation.ActivePowerConsumptionLimit,
                   _                             => LPCLimitation.None
               };

        /// <summary>
        /// Whether the active power consumption limit is activated. Only the
        /// state "limited" activates it ([LPC-009]).
        /// </summary>
        public Boolean        IsLimitActive
            => State == LPCState.Limited;

        /// <summary>
        /// When the last heartbeat of the energy guard arrived.
        /// </summary>
        public DateTimeOffset? LastHeartbeat
            => lastHeartbeat;

        #endregion

        #region Events

        /// <summary>
        /// The state changed.
        /// </summary>
        public event Action<LPCStateMachine, LPCTransition>? OnTransition;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the state machine of a controllable system, which starts in
        /// "init" ([LPC-901], [LPC-903]).
        /// </summary>
        /// <param name="TimeProvider">Where the time comes from.</param>
        public LPCStateMachine(TimeProvider? TimeProvider = null)
        {

            this.TimeProvider  = TimeProvider ?? System.TimeProvider.System;
            this.enteredState  = this.TimeProvider.GetUtcNow();

        }

        #endregion


        #region HeartbeatReceived()

        /// <summary>
        /// A heartbeat of the energy guard arrived.
        ///
        /// A heartbeat alone changes no state: it is the write of the limit
        /// which follows it that does. What it does change is that such a write
        /// will now be evaluated at all (section 2.2).
        /// </summary>
        public void HeartbeatReceived()
        {
            lock (stateLock)
            {

                var now = TimeProvider.GetUtcNow();

                lastHeartbeat         = now;
                heartbeatInThisState ??= now;

            }
        }

        #endregion

        #region MayEvaluateLimitWrite()

        /// <summary>
        /// Whether a write of the active power consumption limit arriving now
        /// is to be evaluated at all.
        ///
        /// Section 2.2: "In state 'init' or 'failsafe state' or state
        /// 'unlimited/autonomous', only after a Heartbeat from the Energy Guard,
        /// a following received write command within 60 seconds on the Active
        /// Power Consumption Limit SHALL be evaluated."
        ///
        /// In the two controlled states there is nothing to wait for: a
        /// heartbeat within the last 120 seconds is what keeps us in them.
        /// </summary>
        public Boolean MayEvaluateLimitWrite()
        {
            lock (stateLock)
            {

                if (State is LPCState.UnlimitedControlled or LPCState.Limited)
                    return true;

                return lastHeartbeat is DateTimeOffset heartbeat &&
                       TimeProvider.GetUtcNow() - heartbeat <= LimitationOfPowerConsumption.LimitAfterHeartbeat;

            }
        }

        #endregion

        #region LimitWritten(Activated, CanBeApplied)

        /// <summary>
        /// A write of the active power consumption limit was evaluated.
        /// </summary>
        /// <param name="Activated">Whether the limit which arrived is activated.</param>
        /// <param name="CanBeApplied">Whether the controllable system can apply it.</param>
        /// <returns>The transition it caused, or null when nothing changed.</returns>
        public LPCTransition? LimitWritten(Boolean Activated,
                                           Boolean CanBeApplied)
        {
            lock (stateLock)
            {

                var limited = Activated && CanBeApplied;

                return State switch {

                    // 2 / 1: init --> limited, or --> unlimited/controlled
                    LPCState.Init                 => limited
                                                         ? To(LPCState.Limited,             2, "[LPC-904] heartbeat and an activated limit which can be applied")
                                                         : To(LPCState.UnlimitedControlled, 1, "[LPC-902], [LPC-905] heartbeat and a deactivated limit, or one which cannot be applied"),

                    // 4: unlimited/controlled --> limited
                    LPCState.UnlimitedControlled  => limited
                                                         ? To(LPCState.Limited,             4, "[LPC-910] an activated limit which can be applied")
                                                         : null,

                    // 6: limited --> unlimited/controlled
                    LPCState.Limited              => limited
                                                         ? null
                                                         : To(LPCState.UnlimitedControlled, 6, "[LPC-909] a deactivated limit"),

                    // 9 / 8: failsafe --> limited, or --> unlimited/controlled
                    LPCState.FailsafeState        => limited
                                                         ? To(LPCState.Limited,             9, "[LPC-919] heartbeat and an activated limit which can be applied")
                                                         : To(LPCState.UnlimitedControlled, 8, "[LPC-918], [LPC-920] heartbeat and a deactivated limit, or one which cannot be applied"),

                    // 12 / 11: unlimited/autonomous --> limited, or --> unlimited/controlled
                    LPCState.UnlimitedAutonomous  => limited
                                                         ? To(LPCState.Limited,            12, "[LPC-919] heartbeat and an activated limit which can be applied")
                                                         : To(LPCState.UnlimitedControlled, 11, "[LPC-918], [LPC-920] heartbeat and a deactivated limit, or one which cannot be applied"),

                    _                             => null

                };

            }
        }

        #endregion

        #region LimitExpired() / LimitInterrupted(Reason)

        /// <summary>
        /// The duration of the activated limit ran out ([LPC-908]).
        /// </summary>
        public LPCTransition? LimitExpired()
        {
            lock (stateLock)
            {
                return State == LPCState.Limited
                           ? To(LPCState.UnlimitedControlled, 6, "[LPC-908] the duration of the limit expired")
                           : null;
            }
        }


        /// <summary>
        /// The controllable system had to stop keeping the limit for one of the
        /// reasons the specification allows: self-protection, safety, law, or -
        /// on an energy manager - loads it does not control ([LPC-923]).
        /// </summary>
        /// <param name="Reason">Which of them.</param>
        public LPCTransition? LimitInterrupted(String Reason)
        {
            lock (stateLock)
            {
                return State == LPCState.Limited
                           ? To(LPCState.UnlimitedControlled, 6, $"[LPC-923] the limit had to be interrupted: {Reason}")
                           : null;
            }
        }

        #endregion

        #region Check()

        /// <summary>
        /// Look at the clock and take whichever transition it has made due.
        ///
        /// Three of the twelve transitions are caused by time passing rather
        /// than by a message: falling into the failsafe state when the heartbeat
        /// stops (5, 7), and giving up on being controlled at all (3, 10).
        /// </summary>
        /// <returns>The transition it caused, or null when nothing changed.</returns>
        public LPCTransition? Check()
        {
            lock (stateLock)
            {

                var now = TimeProvider.GetUtcNow();

                switch (State)
                {

                    // 5 / 7: the heartbeat stopped.
                    case LPCState.UnlimitedControlled:
                    case LPCState.Limited:
                        {

                            var since = lastHeartbeat ?? enteredState;

                            if (now - since > LimitationOfPowerConsumption.HeartbeatTimeout)
                                return To(LPCState.FailsafeState,
                                          State == LPCState.Limited ? 7u : 5u,
                                          $"[LPC-91{(State == LPCState.Limited ? "2" : "1")}] no heartbeat for more than {LimitationOfPowerConsumption.HeartbeatTimeout}");

                            return null;

                        }

                    // 3: nobody took control of us after the restart.
                    case LPCState.Init:
                        {

                            if (now - enteredState > LimitationOfPowerConsumption.WaitForControl)
                                return To(LPCState.UnlimitedAutonomous, 3,
                                          $"[LPC-906] no heartbeat and limit within {LimitationOfPowerConsumption.WaitForControl} of the restart");

                            return null;

                        }

                    // 10: the failsafe state is not a place to stay forever.
                    case LPCState.FailsafeState:
                        {

                            if (now - enteredState >= FailsafeDurationMinimum)
                                return To(LPCState.UnlimitedAutonomous, 10,
                                          $"[LPC-922] the failsafe duration minimum of {FailsafeDurationMinimum} expired");

                            if (heartbeatInThisState is DateTimeOffset heartbeat &&
                                now - heartbeat > LimitationOfPowerConsumption.WaitForControl)
                                return To(LPCState.UnlimitedAutonomous, 10,
                                          $"[LPC-921] a heartbeat arrived but no limit within {LimitationOfPowerConsumption.WaitForControl}");

                            return null;

                        }

                    default:
                        return null;

                }

            }
        }

        #endregion

        #region Restart()

        /// <summary>
        /// The controllable system was restarted, which puts it back into "init"
        /// ([LPC-901], [LPC-903]).
        /// </summary>
        public LPCTransition Restart()
        {
            lock (stateLock)
            {

                lastHeartbeat = null;

                return To(LPCState.Init, 0, "[LPC-901], [LPC-903] the controllable system restarted")!;

            }
        }

        #endregion


        #region (private) To(State, Transition, Reason)

        /// <summary>
        /// Go into a state, and say why.
        /// </summary>
        private LPCTransition? To(LPCState  Next,
                                  UInt32    Transition,
                                  String    Reason)
        {

            var now         = TimeProvider.GetUtcNow();

            var transition  = new LPCTransition(State, Next, Transition, Reason, now);

            State                 = Next;
            enteredState          = now;

            // The 120 second window of [LPC-921] counts from the first heartbeat
            // seen in the failsafe state, so it starts again with the state.
            heartbeatInThisState  = null;

            OnTransition?.Invoke(this, transition);

            return transition;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this state machine.
        /// </summary>
        public override String ToString()

            => $"{State}, {Limitation}";

        #endregion

    }

}
