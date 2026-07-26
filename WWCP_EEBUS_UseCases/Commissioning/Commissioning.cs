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

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.Commissioning
{

    /// <summary>
    /// Who made a device and what it calls itself.
    ///
    /// The specialization "DeviceClassification_ManufacturerData", which the
    /// commissioning use cases all carry with the same ten elements and the same
    /// element rules. None of it is needed to run anything: it exists so that an
    /// energy manager can show a person a name and an icon rather than a SPINE
    /// address.
    ///
    /// The length rules are the sender's job. "The string-length SHOULD NOT be
    /// longer than 256 characters. If it is longer, the sender SHALL consider
    /// the possibility that the receiver will shorten the string to 256
    /// characters" - so a sender which shortens is the one which knows where to
    /// cut. <see cref="Shortened"/> does that.
    /// </summary>
    /// <param name="DeviceName">What this device calls itself.</param>
    /// <param name="DeviceCode">The manufacturer's code for this kind of device.</param>
    /// <param name="SerialNumber">Its serial number.</param>
    /// <param name="SoftwareRevision">Which software it is running.</param>
    /// <param name="HardwareRevision">Which hardware it is.</param>
    /// <param name="VendorName">Who sells it.</param>
    /// <param name="VendorCode">The vendor's code for itself.</param>
    /// <param name="BrandName">Under which brand.</param>
    /// <param name="ManufacturerLabel">A short line the manufacturer wants shown.</param>
    /// <param name="ManufacturerDescription">A longer one.</param>
    public sealed record ManufacturerData(String?  DeviceName                = null,
                                          String?  DeviceCode                = null,
                                          String?  SerialNumber              = null,
                                          String?  SoftwareRevision          = null,
                                          String?  HardwareRevision          = null,
                                          String?  VendorName                = null,
                                          String?  VendorCode                = null,
                                          String?  BrandName                 = null,
                                          String?  ManufacturerLabel         = null,
                                          String?  ManufacturerDescription   = null)
    {

        #region Data

        /// <summary>How long a string of this specialization should be at most.</summary>
        public const Int32  MaxLength              = 256;

        /// <summary>Except the description, which may be longer.</summary>
        public const Int32  MaxDescriptionLength   = 4096;

        #endregion


        #region ToSPINE()

        /// <summary>
        /// This manufacturer data as SPINE carries it, with every string
        /// shortened to what the use cases ask for.
        /// </summary>
        public DeviceClassificationManufacturerDataType ToSPINE()

            => new () {
                   DeviceName               = Shortened(DeviceName),
                   DeviceCode               = Shortened(DeviceCode),
                   SerialNumber             = Shortened(SerialNumber),
                   SoftwareRevision         = Shortened(SoftwareRevision),
                   HardwareRevision         = Shortened(HardwareRevision),
                   VendorName               = Shortened(VendorName),
                   VendorCode               = Shortened(VendorCode),
                   BrandName                = Shortened(BrandName),
                   ManufacturerLabel        = Shortened(ManufacturerLabel),
                   ManufacturerDescription  = Shortened(ManufacturerDescription, MaxDescriptionLength)
               };

        #endregion

        #region (static) FromSPINE(Data)

        /// <summary>
        /// The manufacturer data a partner published, or null when it published
        /// none.
        ///
        /// Not shortened on the way in: what a partner sent is what it sent, and
        /// a test bench which quietly trimmed it could not report that the
        /// partner sent too much.
        /// </summary>
        /// <param name="Data">What a partner published.</param>
        public static ManufacturerData? FromSPINE(DeviceClassificationManufacturerDataType? Data)

            => Data is null
                   ? null
                   : new (Data.DeviceName,
                          Data.DeviceCode,
                          Data.SerialNumber,
                          Data.SoftwareRevision,
                          Data.HardwareRevision,
                          Data.VendorName,
                          Data.VendorCode,
                          Data.BrandName,
                          Data.ManufacturerLabel,
                          Data.ManufacturerDescription);

        #endregion

        #region (private static) Shortened(Text, MaxLength = MaxLength)

        /// <summary>
        /// A string cut to the length the use cases ask for, or null when there
        /// is nothing to send.
        /// </summary>
        private static String? Shortened(String?  Text,
                                         Int32    MaxLength   = ManufacturerData.MaxLength)

            => Text is null || Text.Length <= MaxLength
                   ? Text
                   : Text[..MaxLength];

        #endregion


        /// <summary>Return a text representation of this manufacturer data.</summary>
        public override String ToString()

            => String.Join(", ",
                           new[] { BrandName ?? VendorName, DeviceName, DeviceCode, SerialNumber }.
                               Where(part => part is not null));

    }


    /// <summary>
    /// What tells one commissioning use case from another.
    ///
    /// The commissioning use cases are the same conversation about different
    /// devices: something is plugged in, it says who made it and how it is
    /// doing, and an energy manager writes that down. There is nothing to write
    /// back, nothing to agree and no state machine - the two facts every one of
    /// them carries are the manufacturer data and the operating state, which is
    /// why they live here rather than once per use case.
    /// </summary>
    /// <param name="UseCaseName">The name of the use case.</param>
    /// <param name="Version">The version this implementation follows.</param>
    /// <param name="DocumentSubRevision">The sub revision of the use case document.</param>
    /// <param name="ServerActor">What the side which is commissioned is called.</param>
    /// <param name="ClientActor">What the side which commissions it is called.</param>
    /// <param name="ServerEntityTypes">Which entity types the commissioned side may be.</param>
    /// <param name="Scenarios">The scenarios of the use case.</param>
    /// <param name="ManufacturerScenario">Which scenario the manufacturer data belongs to.</param>
    /// <param name="StateScenario">Which scenario the operating state belongs to.</param>
    /// <param name="ReportedState">The operating state that scenario exists to report.</param>
    public sealed record CommissioningProfile(String                             UseCaseName,
                                              UseCaseVersion                     Version,
                                              String                             DocumentSubRevision,
                                              String                             ServerActor,
                                              String                             ClientActor,
                                              IEnumerable<EntityTypeType>?       ServerEntityTypes,
                                              IReadOnlyList<UseCaseScenario>     Scenarios,
                                              UInt32                             ManufacturerScenario,
                                              UInt32                             StateScenario,
                                              DeviceDiagnosisOperatingStateType  ReportedState)
    {

        #region MandatoryScenarios

        /// <summary>
        /// The scenarios which every device implementing this use case supports.
        /// </summary>
        public IEnumerable<UInt32> MandatoryScenarios

            => Scenarios.Where (scenario => scenario.Mandatory).
                         Select(scenario => scenario.Number);

        #endregion

        #region SupportedScenarios(ForClient, Scenarios = null)

        /// <summary>
        /// The scenarios of this use case which the given side supports, as the
        /// framework needs them.
        /// </summary>
        /// <param name="ForClient">Whether the list is for the commissioning side.</param>
        /// <param name="Scenarios">Which optional scenarios are supported.</param>
        public IEnumerable<UseCaseScenario> SupportedScenarios(Boolean               ForClient,
                                                               IEnumerable<UInt32>?  Scenarios   = null)
        {

            var supported = new SortedSet<UInt32>(Scenarios ?? []);

            foreach (var mandatory in MandatoryScenarios)
                supported.Add(mandatory);

            return [.. this.Scenarios.
                          Where (scenario => supported.Contains(scenario.Number)).
                          Select(scenario => new UseCaseScenario(scenario.Number,
                                                                 ForClient ? scenario.ServerFeatures : [],
                                                                 scenario.Description))];

        }

        #endregion


        /// <summary>Return a text representation of this profile.</summary>
        public override String ToString()

            => $"{UseCaseName} v{Version}";

    }


    /// <summary>
    /// What every commissioning use case has in common on the wire.
    /// </summary>
    public static class CommissioningFunctions
    {

        /// <summary>The function carrying who made the device.</summary>
        public const String ManufacturerData  = "deviceClassificationManufacturerData";

        /// <summary>The function carrying how the device is doing.</summary>
        public const String DiagnosisStateData = "deviceDiagnosisStateData";

    }

}
