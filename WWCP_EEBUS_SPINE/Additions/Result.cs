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

namespace cloud.charging.open.protocols.EEBUS.SPINE.Model
{

    /// <summary>
    /// The error numbers of SPINE 1.3.0.
    ///
    /// The XSD declares "ErrorNumberType" as a plain "xs:unsignedInt" and says
    /// nothing about what the numbers mean; the resource specification lists
    /// them. They are numbers and not an extensible enumeration, so they are
    /// constants here rather than a type of their own - and an error number
    /// which is not one of these is still a legal error number.
    /// </summary>
    public static class SPINEErrorNumbers
    {

        /// <summary>No error (0). A result carrying this is a positive acknowledgement.</summary>
        public const UInt32 NoError                                             = 0;

        /// <summary>A general error (1), used when nothing more precise fits.</summary>
        public const UInt32 GeneralError                                        = 1;

        /// <summary>The operation timed out (2).</summary>
        public const UInt32 Timeout                                             = 2;

        /// <summary>The receiver is overloaded (3).</summary>
        public const UInt32 Overload                                            = 3;

        /// <summary>The addressed destination is unknown (4).</summary>
        public const UInt32 DestinationUnknown                                  = 4;

        /// <summary>The addressed destination cannot be reached (5).</summary>
        public const UInt32 DestinationUnreachable                              = 5;

        /// <summary>The command is not supported by the addressed feature (6).</summary>
        public const UInt32 CommandNotSupported                                 = 6;

        /// <summary>The command was understood and refused (7).</summary>
        public const UInt32 CommandRejected                                     = 7;

        /// <summary>This combination of a restricted function exchange is not supported (8).</summary>
        public const UInt32 RestrictedFunctionExchangeCombinationNotSupported   = 8;

        /// <summary>The command needs a binding, and there is none (9).</summary>
        public const UInt32 BindingIsNecessaryForThisCommand                    = 9;


        #region Name(ErrorNumber)

        /// <summary>
        /// The name of an error number, or its number where it has none.
        /// </summary>
        /// <param name="ErrorNumber">An error number.</param>
        public static String Name(UInt32 ErrorNumber)

            => ErrorNumber switch {
                   NoError                                            => "no error",
                   GeneralError                                       => "general error",
                   Timeout                                            => "timeout",
                   Overload                                           => "overload",
                   DestinationUnknown                                 => "destination unknown",
                   DestinationUnreachable                             => "destination unreachable",
                   CommandNotSupported                                => "command not supported",
                   CommandRejected                                    => "command rejected",
                   RestrictedFunctionExchangeCombinationNotSupported  => "restricted function exchange combination not supported",
                   BindingIsNecessaryForThisCommand                   => "binding is necessary for this command",
                   _                                                  => $"error {ErrorNumber}"
               };

        #endregion

    }


    /// <summary>
    /// The result of a command (SPINE 1.3.0, Result).
    /// </summary>
    public partial class ResultDataType
    {

        #region Properties

        /// <summary>
        /// Whether this result reports success.
        ///
        /// A result without an error number is a positive one: SPINE 1.3.0 sends
        /// "resultData" with "errorNumber" 0 as the acknowledgement of a write,
        /// and leaving the number out means the same thing.
        /// </summary>
        public Boolean IsSuccess
            => (ErrorNumber ?? SPINEErrorNumbers.NoError) == SPINEErrorNumbers.NoError;

        /// <summary>
        /// Whether this result reports an error.
        /// </summary>
        public Boolean IsError
            => !IsSuccess;

        #endregion


        #region (static) Success() / Error(ErrorNumber, Description = null)

        /// <summary>
        /// A result reporting success.
        /// </summary>
        public static ResultDataType Success()

            => new () {
                   ErrorNumber = SPINEErrorNumbers.NoError
               };


        /// <summary>
        /// A result reporting an error.
        /// </summary>
        /// <param name="ErrorNumber">The error number.</param>
        /// <param name="Description">An optional description.</param>
        public static ResultDataType Error(UInt32   ErrorNumber,
                                           String?  Description   = null)

            => new () {
                   ErrorNumber  = ErrorNumber,
                   Description  = Description
               };

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this result.
        /// </summary>
        public override String ToString()
        {

            var name = SPINEErrorNumbers.Name(ErrorNumber ?? SPINEErrorNumbers.NoError);

            return Description is not null
                       ? $"{name}: {Description}"
                       : name;

        }

        #endregion

    }

}
