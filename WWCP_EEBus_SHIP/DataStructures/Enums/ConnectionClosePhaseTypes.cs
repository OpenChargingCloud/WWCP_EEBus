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
    /// Extension methods for Connection-Close-Phases.
    /// </summary>
    public static class ConnectionClosePhasesExtensions
    {

        #region Parse   (Text)

        /// <summary>
        /// Parse the given text as a Connection-Close-Phase.
        /// </summary>
        /// <param name="Text">A text representation of a Connection-Close-Phase.</param>
        public static ConnectionClosePhases Parse(String Text)
        {

            if (TryParse(Text, out var phase))
                return phase;

            return ConnectionClosePhases.Invalid;

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a Connection-Close-Phase.
        /// </summary>
        /// <param name="Text">A text representation of a Connection-Close-Phase.</param>
        public static ConnectionClosePhases? TryParse(String Text)
        {

            if (TryParse(Text, out var phase))
                return phase;

            return null;

        }

        #endregion

        #region TryParse(Text, out ConnectionClosePhase)

        /// <summary>
        /// Try to parse the given text as a Connection-Close-Phase.
        /// </summary>
        /// <param name="Text">A text representation of a Connection-Close-Phase.</param>
        /// <param name="ConnectionClosePhase">The parsed Connection-Close-Phase.</param>
        public static Boolean TryParse(String Text, out ConnectionClosePhases ConnectionClosePhase)
        {
            switch (Text.Trim())
            {

                case "announce":
                    ConnectionClosePhase = ConnectionClosePhases.Announce;
                    return true;

                case "confirm":
                    ConnectionClosePhase = ConnectionClosePhases.Confirm;
                    return true;

                default:
                    ConnectionClosePhase = ConnectionClosePhases.Invalid;
                    return false;

            }
        }

        #endregion


        #region AsText(this ConnectionClosePhase)

        public static String AsText(this ConnectionClosePhases ConnectionClosePhase)

            => ConnectionClosePhase switch {
                   ConnectionClosePhases.Announce  => "announce",
                   ConnectionClosePhases.Confirm   => "confirm",
                   _                               => "unspecific"
               };

        #endregion

    }


    public enum ConnectionClosePhases
    {

        /// <summary>
        /// Invalid
        /// </summary>
        Invalid,

        /// <summary>
        /// Announce
        /// </summary>
        Announce,

        /// <summary>
        /// Confirm
        /// </summary>
        Confirm

    }

}
