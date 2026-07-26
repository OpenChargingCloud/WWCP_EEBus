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

#region Usings

using Microsoft.Extensions.Time.Testing;

using NUnit.Framework;

using cloud.charging.open.protocols.EEBUS.UseCases.LPC;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// The state machine of the controllable system
    /// (EEBus_UC_TS_LimitationOfPowerConsumption_V1.0.0, section 2.3).
    ///
    /// All twelve transitions of section 2.3.3, and Table 1, which says which
    /// limit applies in which state. This is the normative heart of the use
    /// case, and the part the Go reference implementation leaves to the
    /// application - which is the wrong way round for a test bench, because the
    /// states are exactly what a conformance test asks about and the device
    /// under test is somebody else's.
    /// </summary>
    [TestFixture]
    public class LPCStateMachineTests
    {

        #region Data

        private FakeTimeProvider  time    = null!;
        private LPCStateMachine   states  = null!;

        #endregion

        #region Setup()

        [SetUp]
        public void Setup()
        {

            time    = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));
            states  = new LPCStateMachine(time);

        }

        #endregion

        #region (private) Beat() / Limit(...)

        /// <summary>A heartbeat of the energy guard arrives.</summary>
        private void Beat()
            => states.HeartbeatReceived();

        /// <summary>A write of the limit arrives and is evaluated.</summary>
        private LPCTransition? Limit(Boolean Activated, Boolean CanBeApplied = true)
            => states.MayEvaluateLimitWrite()
                   ? states.LimitWritten(Activated, CanBeApplied)
                   : null;

        #endregion


        #region AControllableSystemStartsInInitAndIsLimitedByItsFailsafeValue()

        /// <summary>
        /// Transition 0, and Table 1: after a restart the controllable system is
        /// limited by its failsafe value, and the active power consumption limit
        /// is deactivated ([LPC-901], [LPC-903], [LPC-009/2]).
        /// </summary>
        [Test]
        public void AControllableSystemStartsInInitAndIsLimitedByItsFailsafeValue()
        {

            Assert.Multiple(() => {
                Assert.That(states.State,          Is.EqualTo(LPCState.Init));
                Assert.That(states.Limitation,     Is.EqualTo(LPCLimitation.FailsafeConsumptionActivePowerLimit));
                Assert.That(states.IsLimitActive,  Is.False);
            });

        }

        #endregion

        #region Transition1_InitToUnlimitedControlled()

        /// <summary>
        /// "Init --> Unlimited/controlled: Heartbeat and a following deactivated
        /// power limit or an activated power limit that cannot be applied
        /// received within 120 seconds."
        /// </summary>
        [Test]
        public void Transition1_InitToUnlimitedControlled()
        {

            Beat();

            var deactivated = Limit(Activated: false);

            Assert.Multiple(() => {
                Assert.That(deactivated?.Transition,  Is.EqualTo(1));
                Assert.That(states.State,             Is.EqualTo(LPCState.UnlimitedControlled));
                Assert.That(states.Limitation,        Is.EqualTo(LPCLimitation.None));
            });

            // The other half of the same transition: an activated limit which
            // cannot be applied.
            Setup();
            Beat();

            var inapplicable = Limit(Activated: true, CanBeApplied: false);

            Assert.Multiple(() => {
                Assert.That(inapplicable?.Transition, Is.EqualTo(1));
                Assert.That(states.State,             Is.EqualTo(LPCState.UnlimitedControlled));
            });

        }

        #endregion

        #region Transition2_InitToLimited()

        /// <summary>
        /// "Init --> Limited: Heartbeat and a following activated power limit
        /// received within 120 seconds" ([LPC-904]).
        /// </summary>
        [Test]
        public void Transition2_InitToLimited()
        {

            Beat();

            var transition = Limit(Activated: true);

            Assert.Multiple(() => {
                Assert.That(transition?.Transition, Is.EqualTo(2));
                Assert.That(states.State,           Is.EqualTo(LPCState.Limited));
                Assert.That(states.Limitation,      Is.EqualTo(LPCLimitation.ActivePowerConsumptionLimit));
                Assert.That(states.IsLimitActive,   Is.True);
            });

        }

        #endregion

        #region Transition3_InitToUnlimitedAutonomous()

        /// <summary>
        /// "Init --> Unlimited/autonomous: Heartbeat and a following write
        /// command on the power limit are not received within 120 seconds"
        /// ([LPC-906]).
        ///
        /// A device which nobody claims does not stay limited forever.
        /// </summary>
        [Test]
        public void Transition3_InitToUnlimitedAutonomous()
        {

            time.Advance(TimeSpan.FromSeconds(119));

            Assert.Multiple(() => {
                Assert.That(states.Check(),     Is.Null);
                Assert.That(states.State,       Is.EqualTo(LPCState.Init));
                Assert.That(states.Limitation,  Is.EqualTo(LPCLimitation.FailsafeConsumptionActivePowerLimit),
                            "It is still limited by its failsafe value while it waits.");
            });

            time.Advance(TimeSpan.FromSeconds(2));

            var transition = states.Check();

            Assert.Multiple(() => {
                Assert.That(transition?.Transition, Is.EqualTo(3));
                Assert.That(states.State,           Is.EqualTo(LPCState.UnlimitedAutonomous));
                Assert.That(states.Limitation,      Is.EqualTo(LPCLimitation.None));
            });

        }

        #endregion

        #region Transition4_UnlimitedControlledToLimited()

        [Test]
        public void Transition4_UnlimitedControlledToLimited()
        {

            Beat();
            Limit(Activated: false);

            var transition = Limit(Activated: true);

            Assert.Multiple(() => {
                Assert.That(transition?.Transition, Is.EqualTo(4));
                Assert.That(states.State,           Is.EqualTo(LPCState.Limited));
            });

        }

        #endregion

        #region Transition5And7_TheHeartbeatStops()

        /// <summary>
        /// "No Heartbeat received within 120 seconds since the last Heartbeat"
        /// ([LPC-911], [LPC-912]).
        ///
        /// This is the rule the whole use case turns on: an energy guard which
        /// goes quiet has limited every device it manages, 120 seconds later.
        /// </summary>
        [Test]
        public void Transition5And7_TheHeartbeatStops()
        {

            // From "limited": transition 7.
            Beat();
            Limit(Activated: true);

            time.Advance(TimeSpan.FromSeconds(121));

            var fromLimited = states.Check();

            Assert.Multiple(() => {
                Assert.That(fromLimited?.Transition,  Is.EqualTo(7));
                Assert.That(states.State,             Is.EqualTo(LPCState.FailsafeState));
                Assert.That(states.Limitation,        Is.EqualTo(LPCLimitation.FailsafeConsumptionActivePowerLimit));
                Assert.That(states.IsLimitActive,     Is.False,
                            "[LPC-009/2]: in the failsafe state the limit is deactivated.");
            });

            // From "unlimited/controlled": transition 5.
            Setup();
            Beat();
            Limit(Activated: false);

            time.Advance(TimeSpan.FromSeconds(121));

            var fromControlled = states.Check();

            Assert.Multiple(() => {
                Assert.That(fromControlled?.Transition, Is.EqualTo(5));
                Assert.That(states.State,               Is.EqualTo(LPCState.FailsafeState));
            });

        }

        #endregion

        #region TheHeartbeatIsCountedFromTheLastOne()

        /// <summary>
        /// Every heartbeat starts the 120 seconds again; a device which keeps
        /// beating keeps its partner controlled.
        /// </summary>
        [Test]
        public void TheHeartbeatIsCountedFromTheLastOne()
        {

            Beat();
            Limit(Activated: true);

            for (var i = 0; i < 10; i++)
            {
                time.Advance(TimeSpan.FromSeconds(60));
                Beat();
                Assert.That(states.Check(), Is.Null);
            }

            Assert.That(states.State, Is.EqualTo(LPCState.Limited));

        }

        #endregion

        #region Transition6_TheLimitEndsOrIsInterrupted()

        /// <summary>
        /// "Limited --> Unlimited/controlled: Duration of activated power limit
        /// expired or deactivated power limit received. Or the CS has to
        /// interrupt the state 'limited' for exceptional reasons"
        /// ([LPC-908], [LPC-909], [LPC-923]).
        /// </summary>
        [Test]
        public void Transition6_TheLimitEndsOrIsInterrupted()
        {

            // The duration ran out.
            Beat();
            Limit(Activated: true);

            var expired = states.LimitExpired();

            Assert.Multiple(() => {
                Assert.That(expired?.Transition, Is.EqualTo(6));
                Assert.That(states.State,        Is.EqualTo(LPCState.UnlimitedControlled));
            });

            // A deactivated limit arrived.
            Setup();
            Beat();
            Limit(Activated: true);

            var deactivated = Limit(Activated: false);

            Assert.Multiple(() => {
                Assert.That(deactivated?.Transition, Is.EqualTo(6));
                Assert.That(states.State,            Is.EqualTo(LPCState.UnlimitedControlled));
            });

            // The device had to stop keeping it.
            Setup();
            Beat();
            Limit(Activated: true);

            var interrupted = states.LimitInterrupted("self-protection");

            Assert.Multiple(() => {
                Assert.That(interrupted?.Transition, Is.EqualTo(6));
                Assert.That(interrupted?.Reason,     Does.Contain("self-protection"));
                Assert.That(states.State,            Is.EqualTo(LPCState.UnlimitedControlled));
            });

        }

        #endregion

        #region Transition8And9_LeavingTheFailsafeState()

        /// <summary>
        /// "Failsafe state --> Limited: Heartbeat and a following activated
        /// power limit received that can be applied" ([LPC-919]), and
        /// "--> Unlimited/controlled" for a deactivated one or one which cannot
        /// be applied ([LPC-918], [LPC-920]).
        /// </summary>
        [Test]
        public void Transition8And9_LeavingTheFailsafeState()
        {

            // Into the failsafe state.
            Beat();
            Limit(Activated: true);
            time.Advance(TimeSpan.FromSeconds(121));
            states.Check();

            Assert.That(states.State, Is.EqualTo(LPCState.FailsafeState));

            // The energy guard comes back: heartbeat, then a limit.
            Beat();

            var transition = Limit(Activated: true);

            Assert.Multiple(() => {
                Assert.That(transition?.Transition, Is.EqualTo(9));
                Assert.That(states.State,           Is.EqualTo(LPCState.Limited));
            });

            // The other way out: a limit which cannot be applied.
            Setup();
            Beat();
            Limit(Activated: true);
            time.Advance(TimeSpan.FromSeconds(121));
            states.Check();
            Beat();

            var inapplicable = Limit(Activated: true, CanBeApplied: false);

            Assert.Multiple(() => {
                Assert.That(inapplicable?.Transition, Is.EqualTo(8));
                Assert.That(states.State,             Is.EqualTo(LPCState.UnlimitedControlled),
                            "[LPC-918]: a limit which cannot be applied still ends the failsafe state.");
            });

        }

        #endregion

        #region ALimitWhichDoesNotFollowAHeartbeatIsNotEvaluated()

        /// <summary>
        /// Section 2.2: "In state 'init' or 'failsafe state' or state
        /// 'unlimited/autonomous', only after a Heartbeat from the Energy Guard,
        /// a following received write command within 60 seconds on the Active
        /// Power Consumption Limit SHALL be evaluated."
        ///
        /// A limit from a device which is not proving that it is there does not
        /// take a controllable system out of its failsafe state.
        /// </summary>
        [Test]
        public void ALimitWhichDoesNotFollowAHeartbeatIsNotEvaluated()
        {

            Assert.That(states.MayEvaluateLimitWrite(), Is.False,
                        "A limit was evaluated in 'init' without a heartbeat.");

            Beat();

            Assert.That(states.MayEvaluateLimitWrite(), Is.True);

            time.Advance(TimeSpan.FromSeconds(61));

            Assert.That(states.MayEvaluateLimitWrite(), Is.False,
                        "A limit was evaluated more than 60 seconds after the heartbeat.");

            // In the controlled states there is nothing to wait for: being there
            // at all is what the 120 second rule watches.
            Beat();
            Limit(Activated: true);

            time.Advance(TimeSpan.FromSeconds(90));

            Assert.That(states.MayEvaluateLimitWrite(), Is.True);

        }

        #endregion

        #region Transition10_TheFailsafeStateIsNotForever()

        /// <summary>
        /// "Failsafe state --> Unlimited/autonomous: After expiry of Failsafe
        /// Duration Minimum or Heartbeat received but no following limit
        /// received within 120s" ([LPC-921], [LPC-922]).
        ///
        /// The second half is the important one: it keeps a controllable system
        /// from being held in its failsafe state by an energy guard which beats
        /// but says nothing.
        /// </summary>
        [Test]
        public void Transition10_TheFailsafeStateIsNotForever()
        {

            // A heartbeat, but never a limit.
            Beat();
            Limit(Activated: true);
            time.Advance(TimeSpan.FromSeconds(121));
            states.Check();

            Assert.That(states.State, Is.EqualTo(LPCState.FailsafeState));

            Beat();
            time.Advance(TimeSpan.FromSeconds(121));

            var byHeartbeat = states.Check();

            Assert.Multiple(() => {
                Assert.That(byHeartbeat?.Transition, Is.EqualTo(10));
                Assert.That(byHeartbeat?.Reason,     Does.Contain("LPC-921"));
                Assert.That(states.State,            Is.EqualTo(LPCState.UnlimitedAutonomous));
                Assert.That(states.Limitation,       Is.EqualTo(LPCLimitation.None));
            });

            // Nothing at all, for the whole failsafe duration minimum.
            Setup();
            states.FailsafeDurationMinimum = TimeSpan.FromHours(2);

            Beat();
            Limit(Activated: true);
            time.Advance(TimeSpan.FromSeconds(121));
            states.Check();

            time.Advance(TimeSpan.FromHours(2));

            var byDuration = states.Check();

            Assert.Multiple(() => {
                Assert.That(byDuration?.Transition, Is.EqualTo(10));
                Assert.That(byDuration?.Reason,     Does.Contain("LPC-922"));
                Assert.That(states.State,           Is.EqualTo(LPCState.UnlimitedAutonomous));
            });

        }

        #endregion

        #region Transition11And12_LeavingTheAutonomousState()

        /// <summary>
        /// The autonomous state is left the same way as the failsafe state: a
        /// heartbeat and a limit following it ([LPC-918] to [LPC-920]).
        /// </summary>
        [Test]
        public void Transition11And12_LeavingTheAutonomousState()
        {

            time.Advance(TimeSpan.FromSeconds(121));
            states.Check();

            Assert.That(states.State, Is.EqualTo(LPCState.UnlimitedAutonomous));

            Beat();

            var toLimited = Limit(Activated: true);

            Assert.Multiple(() => {
                Assert.That(toLimited?.Transition, Is.EqualTo(12));
                Assert.That(states.State,          Is.EqualTo(LPCState.Limited));
            });

            Setup();
            time.Advance(TimeSpan.FromSeconds(121));
            states.Check();
            Beat();

            var toControlled = Limit(Activated: false);

            Assert.Multiple(() => {
                Assert.That(toControlled?.Transition, Is.EqualTo(11));
                Assert.That(states.State,             Is.EqualTo(LPCState.UnlimitedControlled));
            });

        }

        #endregion

        #region ARestartPutsItBackIntoInit()

        [Test]
        public void ARestartPutsItBackIntoInit()
        {

            Beat();
            Limit(Activated: true);

            var transition = states.Restart();

            Assert.Multiple(() => {
                Assert.That(transition.Transition,          Is.EqualTo(0));
                Assert.That(states.State,                   Is.EqualTo(LPCState.Init));
                Assert.That(states.Limitation,              Is.EqualTo(LPCLimitation.FailsafeConsumptionActivePowerLimit));
                Assert.That(states.MayEvaluateLimitWrite(), Is.False,
                            "After a restart the heartbeat has to arrive again.");
            });

        }

        #endregion

        #region Table1_WhichLimitAppliesInWhichState()

        /// <summary>
        /// Table 1 of the specification, in one place: which limit the
        /// controllable system holds itself to, per state.
        /// </summary>
        [Test]
        public void Table1_WhichLimitAppliesInWhichState()
        {

            (LPCState State, LPCLimitation Limitation, Boolean IsLimitActive)[] expected = [
                (LPCState.Init,                 LPCLimitation.FailsafeConsumptionActivePowerLimit, false),
                (LPCState.UnlimitedControlled,  LPCLimitation.None,                                false),
                (LPCState.Limited,              LPCLimitation.ActivePowerConsumptionLimit,         true),
                (LPCState.FailsafeState,        LPCLimitation.FailsafeConsumptionActivePowerLimit, false),
                (LPCState.UnlimitedAutonomous,  LPCLimitation.None,                                false)
            ];

            foreach (var (state, limitation, isActive) in expected)
            {

                Setup();

                switch (state)
                {

                    case LPCState.UnlimitedControlled:
                        Beat(); Limit(Activated: false);
                        break;

                    case LPCState.Limited:
                        Beat(); Limit(Activated: true);
                        break;

                    case LPCState.FailsafeState:
                        Beat(); Limit(Activated: true);
                        time.Advance(TimeSpan.FromSeconds(121));
                        states.Check();
                        break;

                    case LPCState.UnlimitedAutonomous:
                        time.Advance(TimeSpan.FromSeconds(121));
                        states.Check();
                        break;

                }

                Assert.Multiple(() => {
                    Assert.That(states.State,         Is.EqualTo(state));
                    Assert.That(states.Limitation,    Is.EqualTo(limitation), $"in state {state}");
                    Assert.That(states.IsLimitActive, Is.EqualTo(isActive),   $"in state {state}");
                });

            }

        }

        #endregion

    }

}
