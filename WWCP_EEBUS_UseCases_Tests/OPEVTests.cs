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

using cloud.charging.open.protocols.EEBUS.SPINE;
using cloud.charging.open.protocols.EEBUS.SPINE.Model;
using cloud.charging.open.protocols.EEBUS.UseCases.OPEV;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "Overload Protection by EV Charging Current Curtailment", both actors,
    /// over the wire.
    ///
    /// An energy guard keeps a fuse from tripping by telling an electric vehicle
    /// to charge with less current. The whole chain has six seconds
    /// (section 2.1), which is why the heartbeat timeout here is four seconds
    /// rather than the 120 of the limitation of power consumption - and why the
    /// EV falling back on its own is the point rather than an afterthought.
    /// </summary>
    [TestFixture]
    public class OPEVTests
    {

        #region Data

        private FakeTimeProvider       time    = null!;
        private SPINELoopback          wire    = null!;

        private OPEVEnergyGuard        guard   = null!;
        private OPEVElectricVehicle    ev      = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var car  = new SPINELocalDevice("d:_i:19667_EV",   DeviceTypeType.Generic,                TimeProvider: time);

            guard = new OPEVEnergyGuard    (hems.AddEntity(EntityTypeType.CEM));
            ev    = new OPEVElectricVehicle(car. AddEntity(EntityTypeType.EV));

            ev.SetPermittedCurrents(6, 16);
            ev.SafeCurrent = 6;

            wire = new SPINELoopback(hems, car);

            await guard.Register();
            await ev.   Register();

            await Discover();

        }

        #endregion

        #region (private) Discover() / Commission() / the two sides

        private async Task Discover()
        {
            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);
        }

        /// <summary>The EV, as the energy guard sees it.</summary>
        private SPINERemoteEntity EV
            => wire.BAsSeenByA.Entity([ 1 ])!;

        /// <summary>The energy guard, as the EV sees it.</summary>
        private SPINERemoteEntity EG
            => wire.AAsSeenByB.Entity([ 1 ])!;


        private async Task Commission()
        {

            // The EV watches the energy guard; the energy guard reads and writes
            // the EV.
            await new UseCaseFeature(FeatureTypeType.DeviceDiagnosis, ev.Entity, EG).Subscribe();

            var loadControl = guard.LoadControlOf(EV);

            await loadControl.Subscribe();
            await loadControl.Bind();

            await guard.ElectricalOf(EV).Subscribe();

            await guard.StartHeartbeat();

            ev.Check();

        }

        #endregion


        #region TheTwoActorsFindEachOther()

        [Test]
        public void TheTwoActorsFindEachOther()
        {

            Assert.Multiple(() => {

                var partnerOfGuard = guard.PartnerFor(EV);
                var partnerOfEV    = ev.   PartnerFor(EG);

                Assert.That(partnerOfGuard,             Is.Not.Null);
                Assert.That(partnerOfGuard?.Scenarios,  Is.EquivalentTo(new UInt32[] { 1, 2, 3 }));
                Assert.That(partnerOfGuard?.Version.ToString(), Is.EqualTo("1.0.1"));

                Assert.That(partnerOfEV,                Is.Not.Null);
                Assert.That(partnerOfEV?.Scenarios,     Is.EquivalentTo(new UInt32[] { 1, 2, 3 }));

            });

        }

        #endregion

        #region AnEnergyGuardMayAnnounceItselfAsCEMBecauseTheFieldDoesIt()

        /// <summary>
        /// The specification calls the client actor "EnergyGuard"; the certified
        /// Go implementation announces it as "CEM", and devices in the field
        /// were built against that. An EV which accepts only one of the two
        /// would not work with half of them.
        /// </summary>
        [Test]
        public async Task AnEnergyGuardMayAnnounceItselfAsCEMBecauseTheFieldDoesIt()
        {

            var hems = new SPINELocalDevice("d:_i:19667_OtherHEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var car  = new SPINELocalDevice("d:_i:19667_OtherEV",   DeviceTypeType.Generic,                TimeProvider: time);

            var asCEM   = new OPEVEnergyGuard    (hems.AddEntity(EntityTypeType.CEM), AnnounceAsCEM: true);
            var otherEV = new OPEVElectricVehicle(car. AddEntity(EntityTypeType.EV));

            var other   = new SPINELoopback(hems, car);

            await asCEM.  Register();
            await otherEV.Register();

            await other.A.NodeManagement.RequestDetailedDiscovery(other.BAsSeenByA);
            await other.B.NodeManagement.RequestDetailedDiscovery(other.AAsSeenByB);
            await other.B.NodeManagement.RequestUseCaseData      (other.AAsSeenByB);

            Assert.Multiple(() => {

                Assert.That(asCEM.Actor, Is.EqualTo("CEM"));

                Assert.That(otherEV.PartnerFor(other.AAsSeenByB.Entity([ 1 ])), Is.Not.Null,
                            "An energy guard which calls itself CEM was not recognised.");

            });

        }

        #endregion


        #region Scenario1_TheEnergyGuardLearnsWhichPhaseIsWhich()

        /// <summary>
        /// "Before the Energy Guard curtails the EV current, the Energy Guard
        /// needs to know from the EV which phases are used for charging and the
        /// electrical charging constraints" (section 2.3.1.1).
        ///
        /// Three functions across two features, joined by nothing but
        /// identifiers: the limit description says which measurement its limit
        /// is about, the parameter description says which phase that measurement
        /// is on, and the permitted value set says what the EV can do.
        /// </summary>
        [Test]
        public async Task Scenario1_TheEnergyGuardLearnsWhichPhaseIsWhich()
        {

            await Commission();

            var phases = await guard.ReadPhases(EV);

            Assert.Multiple(() => {

                Assert.That(phases, Has.Count.EqualTo(3));

                Assert.That(phases[0].Phase,           Is.EqualTo(ElectricalConnectionPhaseNameType.A));
                Assert.That(phases[1].Phase,           Is.EqualTo(ElectricalConnectionPhaseNameType.B));
                Assert.That(phases[2].Phase,           Is.EqualTo(ElectricalConnectionPhaseNameType.C));

                Assert.That(phases[0].MinimumCurrent,  Is.EqualTo(6));
                Assert.That(phases[0].MaximumCurrent,  Is.EqualTo(16));

                // Each phase has its own limit.
                Assert.That(phases.Select(phase => phase.LimitId).Distinct().Count(), Is.EqualTo(3));

            });

        }

        #endregion

        #region Scenario1_TheEnergyGuardCurtailsTheChargingCurrent()

        /// <summary>
        /// [OPEV-001]: the energy guard curtails the charging current so that no
        /// overload occurs.
        /// </summary>
        [Test]
        public async Task Scenario1_TheEnergyGuardCurtailsTheChargingCurrent()
        {

            await Commission();

            var response = await guard.WriteCurrentLimit(EV, 10);

            Assert.Multiple(() => {

                Assert.That(response.IsError,          Is.False, response.Result?.Description);

                Assert.That(ev.Trust,                  Is.EqualTo(OPEVTrust.Curtailed));
                Assert.That(ev.ChargingCurrents,       Is.EqualTo(new Decimal[] { 10, 10, 10 }));

                Assert.That(ev.CurrentLimits.All(limit => limit.IsActive), Is.True);

            });

        }

        #endregion

        #region Scenario1_AsymmetricCharging()

        /// <summary>
        /// [OPEV-002]: where asymmetric charging is supported the phases are
        /// curtailed independently - which in the specification's own example is
        /// the difference between 690 W and 460 W.
        /// </summary>
        [Test]
        public async Task Scenario1_AsymmetricCharging()
        {

            await Commission();

            await guard.WriteCurrentLimits(EV, [ 6, 10, 16 ]);

            Assert.That(ev.ChargingCurrents, Is.EqualTo(new Decimal[] { 6, 10, 16 }));

        }

        #endregion

        #region Scenario1_NoCurtailmentNeededIsAlsoSaid()

        /// <summary>
        /// [OPEV-004]: "if currently no curtailment is needed the Energy Guard
        /// shall also inform the EV". A deactivated limit is how it says so, and
        /// the EV then charges with what it can rather than with what it was
        /// told.
        /// </summary>
        [Test]
        public async Task Scenario1_NoCurtailmentNeededIsAlsoSaid()
        {

            await Commission();

            await guard.WriteCurrentLimit(EV, 10);

            Assert.That(ev.ChargingCurrents[0], Is.EqualTo(10));

            await guard.WriteCurrentLimit(EV, 16, IsActive: false);

            Assert.Multiple(() => {
                Assert.That(ev.CurrentLimits[0].IsActive, Is.False);
                Assert.That(ev.Trust,                     Is.EqualTo(OPEVTrust.Curtailed),
                            "A deactivated limit is not a reason to stop trusting the energy guard.");
            });

        }

        #endregion

        #region Scenario1_ALimitForAPhaseTheEVDoesNotHaveIsRefused()

        /// <summary>
        /// A single phase EV has one limit, and a write to a second one is about
        /// nothing.
        /// </summary>
        [Test]
        public async Task Scenario1_ALimitForAPhaseTheEVDoesNotHaveIsRefused()
        {

            var hems       = new SPINELocalDevice("d:_i:19667_HEMS2", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var car        = new SPINELocalDevice("d:_i:19667_EV2",   DeviceTypeType.Generic,                TimeProvider: time);

            var otherGuard = new OPEVEnergyGuard    (hems.AddEntity(EntityTypeType.CEM));
            var onePhase   = new OPEVElectricVehicle(car. AddEntity(EntityTypeType.EV), PhaseCount: 1);

            onePhase.SetPermittedCurrents(6, 16);

            var other      = new SPINELoopback(hems, car);

            await otherGuard.Register();
            await onePhase.  Register();

            await other.A.NodeManagement.RequestDetailedDiscovery(other.BAsSeenByA);
            await other.B.NodeManagement.RequestDetailedDiscovery(other.AAsSeenByB);

            var otherEV    = other.BAsSeenByA.Entity([ 1 ])!;

            await otherGuard.LoadControlOf(otherEV).Bind();

            var phases     = await otherGuard.ReadPhases(otherEV);

            Assert.That(phases, Has.Count.EqualTo(1));

            var response   = await otherGuard.LoadControlOf(otherEV).WriteData(
                                       OverloadProtection.LimitListData,
                                       new LoadControlLimitListDataType {
                                           LoadControlLimitData = [
                                               new LoadControlLimitDataType {
                                                   LimitId        = 3,
                                                   IsLimitActive  = true,
                                                   Value          = ScaledNumberType.FromValue(10)
                                               }
                                           ]
                                       }
                                   );

            Assert.Multiple(() => {
                Assert.That(response.IsError,             Is.True);
                Assert.That(response.Result?.Description, Does.Contain("1 phase"));
            });

        }

        #endregion


        #region Scenario2_WhenTheHeartbeatStopsTheEVChargesSafely()

        /// <summary>
        /// [OPEV-005]: "If the Energy Guard is not available for more than 4
        /// seconds, the EV should switch to a safe current setting that
        /// guarantees that no overload occurs during absence of the Energy
        /// Guard."
        ///
        /// Four seconds, because the fuse it is protecting can trip in six.
        /// </summary>
        [Test]
        public async Task Scenario2_WhenTheHeartbeatStopsTheEVChargesSafely()
        {

            await Commission();

            await guard.WriteCurrentLimit(EV, 16);

            Assert.That(ev.ChargingCurrents[0], Is.EqualTo(16));

            // The heartbeats keep arriving.
            for (var i = 0; i < 3; i++)
            {
                time.Advance(TimeSpan.FromSeconds(2));
                ev.Check();
            }

            Assert.That(ev.Trust, Is.EqualTo(OPEVTrust.Curtailed),
                        "The EV gave up although the heartbeats arrived.");

            // The energy guard goes quiet.
            guard.StopHeartbeat();

            time.Advance(TimeSpan.FromSeconds(3));

            Assert.That(ev.Check(), Is.Null,
                        "The EV gave up before the four seconds were over.");

            time.Advance(TimeSpan.FromSeconds(2));

            var change = ev.Check();

            Assert.Multiple(() => {
                Assert.That(change?.To,             Is.EqualTo(OPEVTrust.HeartbeatMissing));
                Assert.That(change?.Reason,         Does.Contain("OPEV-005"));
                Assert.That(ev.ChargingCurrents,    Is.EqualTo(new Decimal[] { 6, 6, 6 }),
                            "The EV kept charging with the current of an energy guard which is gone.");

                // The limit itself is still there; it is the trust which is not.
                Assert.That(ev.CurrentLimits[0].Value, Is.EqualTo(16));
            });

        }

        #endregion

        #region Scenario2_WhenTheHeartbeatReturnsTheCurtailmentAppliesAgain()

        [Test]
        public async Task Scenario2_WhenTheHeartbeatReturnsTheCurtailmentAppliesAgain()
        {

            await Commission();

            await guard.WriteCurrentLimit(EV, 16);

            guard.StopHeartbeat();
            time.Advance(TimeSpan.FromSeconds(5));
            ev.Check();

            Assert.That(ev.Trust, Is.EqualTo(OPEVTrust.HeartbeatMissing));

            await guard.StartHeartbeat();

            Assert.Multiple(() => {
                Assert.That(ev.Trust,            Is.EqualTo(OPEVTrust.Curtailed));
                Assert.That(ev.ChargingCurrents, Is.EqualTo(new Decimal[] { 16, 16, 16 }));
            });

        }

        #endregion

        #region Scenario3_AnEnergyGuardWhichAnnouncesAFailureIsNoLongerTrusted()

        /// <summary>
        /// [OPEV-007]: "If the Energy Guard has announced an error, the EV
        /// should not trust the Energy Guard regarding its charging current
        /// curtailment and should switch to a safe current setting."
        ///
        /// This is the faster of the two fallbacks: the energy guard is still
        /// sending heartbeats, so scenario 2 would never notice. Announcing
        /// one's own failure is a rare thing for a protocol to ask for, and it
        /// is what makes the difference here.
        /// </summary>
        [Test]
        public async Task Scenario3_AnEnergyGuardWhichAnnouncesAFailureIsNoLongerTrusted()
        {

            await Commission();

            await guard.WriteCurrentLimit(EV, 16);

            Assert.That(ev.ChargingCurrents[0], Is.EqualTo(16));

            await guard.SetOperatingState(DeviceDiagnosisOperatingStateType.Failure,
                                          "submeter unreachable");

            Assert.Multiple(() => {

                Assert.That(ev.Trust,             Is.EqualTo(OPEVTrust.EnergyGuardFailed));
                Assert.That(ev.ChargingCurrents,  Is.EqualTo(new Decimal[] { 6, 6, 6 }));

                // Without ever missing a heartbeat.
                Assert.That(ev.Check(),           Is.Null);

            });

            // ... and when it recovers, the curtailment applies again.
            await guard.SetOperatingState(DeviceDiagnosisOperatingStateType.NormalOperation);

            Assert.Multiple(() => {
                Assert.That(ev.Trust,             Is.EqualTo(OPEVTrust.Curtailed));
                Assert.That(ev.ChargingCurrents,  Is.EqualTo(new Decimal[] { 16, 16, 16 }));
            });

        }

        #endregion

        #region Scenario3_AnythingButNormalOperationIsAReasonToBeCareful()

        /// <summary>
        /// The specification names "failure" (Table 11). A guard which says it
        /// is in an alarm, or not ready, is not more trustworthy than one which
        /// says failure - and an EV which only watches for the one word would
        /// keep charging at full current through the others.
        /// </summary>
        [Test]
        public async Task Scenario3_AnythingButNormalOperationIsAReasonToBeCareful()
        {

            await Commission();
            await guard.WriteCurrentLimit(EV, 16);

            foreach (var state in new[] {
                         DeviceDiagnosisOperatingStateType.Failure,
                         DeviceDiagnosisOperatingStateType.InAlarm,
                         DeviceDiagnosisOperatingStateType.TemporarilyNotReady,
                         DeviceDiagnosisOperatingStateType.Off
                     })
            {

                await guard.SetOperatingState(state);

                Assert.That(ev.Trust, Is.EqualTo(OPEVTrust.EnergyGuardFailed),
                            $"The EV kept trusting an energy guard in state '{state}'.");

                await guard.SetOperatingState(DeviceDiagnosisOperatingStateType.NormalOperation);

                Assert.That(ev.Trust, Is.EqualTo(OPEVTrust.Curtailed));

            }

        }

        #endregion

        #region TheFallbackHappensWellInsideTheBudgetOfTheSpecification()

        /// <summary>
        /// Section 2.1 budgets six seconds from the overload happening to the EV
        /// charging with less, of which the EV may use two.
        ///
        /// This test does not measure our own speed - a loopback would tell
        /// nothing about a real device - it checks that the **rule** we
        /// implement fits the budget: four seconds of heartbeat timeout plus the
        /// EV's two leaves nothing over, which is exactly why the specification
        /// asks for the heartbeat to arrive more often than the timeout.
        /// </summary>
        [Test]
        public void TheFallbackHappensWellInsideTheBudgetOfTheSpecification()
        {

            Assert.Multiple(() => {

                Assert.That(OverloadProtection.HeartbeatInterval,
                            Is.LessThan(OverloadProtection.HeartbeatTimeout),
                            "A heartbeat which is sent no more often than the timeout is a timeout waiting to happen.");

                Assert.That(OverloadProtection.EnergyGuardBudget +
                            OverloadProtection.MessageBudget +
                            OverloadProtection.ElectricVehicleBudget,
                            Is.LessThan(OverloadProtection.ReactionBudget),
                            "The budgets of section 2.1 leave no slack.");

            });

        }

        #endregion

    }

}
