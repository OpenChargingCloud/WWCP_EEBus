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

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// The timeouts of the SHIP message exchange.
    ///
    /// All of them are protocol behaviour and therefore driven by a TimeProvider,
    /// never by the wall clock, so that they can be tested deterministically.
    /// </summary>
    /// <param name="CMI">The time a communication partner has to answer the connection mode initialisation (SHIP TS 1.0.1, chapter 13.4.3).</param>
    /// <param name="HelloInit">The time a communication partner has to announce its readiness (chapter 13.4.4.1.3).</param>
    /// <param name="HelloProlongThresholdIncrement">How long before the expiry of the partner's timer a prolongation is requested (chapter 13.4.4.1.3).</param>
    /// <param name="HelloProlongMinimum">The smallest waiting time still considered valid (chapter 13.4.4.1.3).</param>
    /// <param name="AbortDelay">How long an abort is given to reach the communication partner before the connection is closed.</param>
    /// <param name="CloseConfirm">The time a communication partner has to confirm a connection close (chapter 13.4.7).</param>
    public readonly record struct SHIPTimeouts(TimeSpan  CMI,
                                               TimeSpan  HelloInit,
                                               TimeSpan  HelloProlongThresholdIncrement,
                                               TimeSpan  HelloProlongMinimum,
                                               TimeSpan  AbortDelay,
                                               TimeSpan  CloseConfirm)
    {

        /// <summary>
        /// The timeouts defined by SHIP TS 1.0.1.
        /// </summary>
        public static SHIPTimeouts Default { get; }

            = new (
                  CMI:                              TimeSpan.FromSeconds(10),  // chapter 4.2 / 13.4.3
                  HelloInit:                        TimeSpan.FromSeconds(60),  // chapter 13.4.4.1.3: T_hello_init
                  HelloProlongThresholdIncrement:   TimeSpan.FromSeconds(30),  // chapter 13.4.4.1.3: T_hello_prolong_thr_inc
                  HelloProlongMinimum:              TimeSpan.FromSeconds(1),   // chapter 13.4.4.1.3: T_hello_prolong_min
                  AbortDelay:                       TimeSpan.FromSeconds(1),
                  CloseConfirm:                     TimeSpan.FromSeconds(60)
              );

    }

}
