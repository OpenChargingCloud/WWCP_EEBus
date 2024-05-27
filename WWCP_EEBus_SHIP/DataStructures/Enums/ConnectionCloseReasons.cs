/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of WWCP EEBus <https://github.com/OpenChargingCloud/WWCP_EEBus>
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

namespace cloud.charging.open.protocols.EEBus
{

    /// <summary>
    /// Extension methods for Connection-Close-Reasons.
    /// </summary>
    public static class ConnectionCloseReasonsExtensions
    {

        #region Parse   (Text)

        /// <summary>
        /// Parse the given text as a Connection-Close-Reason.
        /// </summary>
        /// <param name="Text">A text representation of a Connection-Close-Reason.</param>
        public static ConnectionCloseReasons Parse(String Text)
        {

            if (TryParse(Text, out var reason))
                return reason;

            return ConnectionCloseReasons.Unspecific;

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a Connection-Close-Reason.
        /// </summary>
        /// <param name="Text">A text representation of a Connection-Close-Reason.</param>
        public static ConnectionCloseReasons? TryParse(String Text)
        {

            if (TryParse(Text, out var reason))
                return reason;

            return null;

        }

        #endregion

        #region TryParse(Text, out ConnectionCloseReason)

        /// <summary>
        /// Try to parse the given text as a Connection-Close-Reason.
        /// </summary>
        /// <param name="Text">A text representation of a Connection-Close-Reason.</param>
        /// <param name="ConnectionCloseReason">The parsed Connection-Close-Reason.</param>
        public static Boolean TryParse(String Text, out ConnectionCloseReasons ConnectionCloseReason)
        {
            switch (Text.Trim())
            {

                case "removedConnection":
                    ConnectionCloseReason = ConnectionCloseReasons.RemovedConnection;
                    return true;

                default:
                    ConnectionCloseReason = ConnectionCloseReasons.Unspecific;
                    return false;

            }
        }

        #endregion


        #region AsText(this ConnectionCloseReason)

        public static String AsText(this ConnectionCloseReasons ConnectionCloseReason)

            => ConnectionCloseReason switch {
                   ConnectionCloseReasons.RemovedConnection  => "removedConnection",
                   _                                             => "unspecific"
               };

        #endregion

    }


    /// <summary>
    /// Connection close reasons
    /// </summary>
    public enum ConnectionCloseReasons
    {

        /// <summary>
        /// Unspecific
        /// </summary>
        Unspecific,

        /// <summary>
        /// RemovedConnection
        /// </summary>
        RemovedConnection

    }

}
