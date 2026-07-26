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

using NUnit.Framework;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// The EEBUS use case implementations (LPC, LPP, MPC, MGCP, OPEV, OSCEV, ...)
    /// are built in work packages WP08/WP09; their tests will live here.
    /// </summary>
    [TestFixture]
    public class UseCasesPlaceholderTests
    {

        #region UseCaseAssembly_IsLoadable()

        [Test]
        public void UseCaseAssembly_IsLoadable()
        {
            Assert.That(typeof(UseCasesPlaceholderTests).Assembly, Is.Not.Null);
        }

        #endregion

    }

}
