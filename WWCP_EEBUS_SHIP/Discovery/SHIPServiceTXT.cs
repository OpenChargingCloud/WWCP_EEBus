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

using System.Diagnostics.CodeAnalysis;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// The content of the mDNS TXT record of a SHIP node
    /// (SHIP TS 1.0.1, chapter 7.3.2).
    ///
    /// This is what a SHIP node tells the network about itself before any
    /// connection exists - most importantly its SKI, which the user compares
    /// with the value printed on the device.
    /// </summary>
    /// <param name="Id">A unique identifier of the SHIP node.</param>
    /// <param name="SKI">The SKI of the SHIP node.</param>
    /// <param name="Path">The path of the SHIP WebSocket endpoint.</param>
    /// <param name="DeviceBrand">The brand of the device.</param>
    /// <param name="DeviceModel">The model of the device.</param>
    /// <param name="DeviceType">The type of the device.</param>
    /// <param name="Register">Whether the SHIP node currently accepts a registration ("auto accept").</param>
    /// <param name="DeviceSerialNumber">An optional serial number (SHIP Requirements For Installation Process).</param>
    /// <param name="DeviceCategories">Optional device categories (SHIP Requirements For Installation Process).</param>
    /// <param name="AdditionalKeyValues">Further key/value pairs which are not defined by SHIP.</param>
    public class SHIPServiceTXT(SHIP_Id                       Id,
                                SKI                           SKI,
                                String?                       Path                  = null,
                                String?                       DeviceBrand           = null,
                                String?                       DeviceModel           = null,
                                String?                       DeviceType            = null,
                                Boolean                       Register              = false,
                                String?                       DeviceSerialNumber    = null,
                                IEnumerable<String>?          DeviceCategories      = null,
                                IEnumerable<KeyValuePair<String, String>>?  AdditionalKeyValues   = null)
    {

        #region Data

        /// <summary>
        /// The TXT record version defined by SHIP TS 1.0.1, chapter 7.3.2.
        /// </summary>
        public const String  TXTVersion   = "1";

        /// <summary>
        /// The default path of the SHIP WebSocket endpoint.
        /// </summary>
        public const String  DefaultPath  = "/ship/";

        #endregion

        #region Properties

        /// <summary>
        /// A unique identifier of the SHIP node.
        /// </summary>
        public SHIP_Id              Id                    { get; } = Id;

        /// <summary>
        /// The SKI of the SHIP node.
        /// </summary>
        public SKI                  SKI                   { get; } = SKI;

        /// <summary>
        /// The path of the SHIP WebSocket endpoint.
        /// </summary>
        public String               Path                  { get; } = Path ?? DefaultPath;

        /// <summary>
        /// The brand of the device.
        /// </summary>
        public String               DeviceBrand           { get; } = DeviceBrand ?? "";

        /// <summary>
        /// The model of the device.
        /// </summary>
        public String               DeviceModel           { get; } = DeviceModel ?? "";

        /// <summary>
        /// The type of the device.
        /// </summary>
        public String               DeviceType            { get; } = DeviceType  ?? "";

        /// <summary>
        /// Whether the SHIP node currently accepts a registration.
        /// </summary>
        public Boolean              Register              { get; } = Register;

        /// <summary>
        /// An optional serial number of the device.
        /// </summary>
        public String?              DeviceSerialNumber    { get; } = DeviceSerialNumber;

        /// <summary>
        /// Optional device categories.
        /// </summary>
        public IEnumerable<String>  DeviceCategories      { get; } = DeviceCategories ?? [];

        /// <summary>
        /// Further key/value pairs which are not defined by SHIP.
        /// </summary>
        public IEnumerable<KeyValuePair<String, String>>  AdditionalKeyValues    { get; } = AdditionalKeyValues ?? [];

        #endregion


        #region ToTXTStrings()

        /// <summary>
        /// Return the key/value strings of the mDNS TXT record, in the order
        /// given by SHIP TS 1.0.1, chapter 7.3.2.
        /// </summary>
        public IEnumerable<String> ToTXTStrings()
        {

            var strings = new List<String> {
                              $"txtvers={TXTVersion}",
                              $"path={Path}",
                              $"id={Id}",
                              $"ski={SKI}",
                              $"brand={DeviceBrand}",
                              $"model={DeviceModel}",
                              $"type={DeviceType}",
                              $"register={(Register ? "true" : "false")}"
                          };

            if (DeviceSerialNumber is not null && DeviceSerialNumber.Length > 0)
                strings.Add($"serial={DeviceSerialNumber}");

            var categories = DeviceCategories.ToArray();

            if (categories.Length > 0)
                strings.Add($"cat={String.Join(",", categories)}");

            foreach (var keyValue in AdditionalKeyValues)
                strings.Add($"{keyValue.Key}={keyValue.Value}");

            return strings;

        }

        #endregion

        #region (static) TryParse(TXTStrings, out ServiceTXT, out ErrorResponse)

        /// <summary>
        /// Try to parse the given mDNS TXT record of a SHIP node.
        /// </summary>
        /// <param name="TXTStrings">The key/value strings of an mDNS TXT record.</param>
        /// <param name="ServiceTXT">The parsed TXT record content.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(IEnumerable<String>                   TXTStrings,
                                       [NotNullWhen(true)]  out SHIPServiceTXT?  ServiceTXT,
                                       [NotNullWhen(false)] out String?          ErrorResponse)
        {

            ServiceTXT     = null;
            ErrorResponse  = null;

            var keyValues  = new List<KeyValuePair<String, String>>();

            foreach (var text in TXTStrings)
            {

                var separator = text.IndexOf('=');

                // A TXT string without '=' is a valid DNS-SD boolean attribute,
                // but SHIP does not define any; keep it as an empty value.
                keyValues.Add(separator < 0
                                  ? new KeyValuePair<String, String>(text, "")
                                  : new KeyValuePair<String, String>(text[..separator], text[(separator + 1)..]));

            }

            String? Get(String Key)
                => keyValues.FirstOrDefault(keyValue => String.Equals(keyValue.Key, Key, StringComparison.OrdinalIgnoreCase)).Value;

            Boolean Has(String Key)
                => keyValues.Any(keyValue => String.Equals(keyValue.Key, Key, StringComparison.OrdinalIgnoreCase));

            #region SKI    [mandatory]

            var skiText = Get("ski");

            if (skiText is null)
            {
                ErrorResponse = "The mDNS TXT record of a SHIP node must contain its SKI (SHIP TS 1.0.1, chapter 7.3.2)!";
                return false;
            }

            if (!SKI.TryParse(skiText, out var ski, out ErrorResponse))
                return false;

            #endregion

            #region Id     [mandatory]

            var idText = Get("id");

            if (idText is null || idText.Length == 0)
            {
                ErrorResponse = "The mDNS TXT record of a SHIP node must contain its identifier (SHIP TS 1.0.1, chapter 7.3.2)!";
                return false;
            }

            if (!SHIP_Id.TryParse(idText, out var id))
            {
                ErrorResponse = $"The SHIP identifier '{idText}' is invalid!";
                return false;
            }

            #endregion

            var known       = new[] { "txtvers", "path", "id", "ski", "brand", "model", "type", "register", "serial", "cat" };

            var categories  = Get("cat");

            ServiceTXT      = new SHIPServiceTXT(
                                  id,
                                  ski,
                                  Get("path"),
                                  Get("brand"),
                                  Get("model"),
                                  Get("type"),
                                  // Anything but "true" means the node does not accept a registration.
                                  String.Equals(Get("register"), "true", StringComparison.OrdinalIgnoreCase),
                                  Has("serial") ? Get("serial") : null,
                                  categories is not null && categories.Length > 0
                                      ? categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                      : null,
                                  keyValues.Where(keyValue => !known.Contains(keyValue.Key, StringComparer.OrdinalIgnoreCase))
                              );

            return true;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{Id} ({SKI.ToGroupedString()}){(Register ? ", accepting registrations" : "")}";

        #endregion

    }

}
