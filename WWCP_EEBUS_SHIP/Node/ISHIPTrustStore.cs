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

using System.Collections.Concurrent;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// The SKIs a SHIP node trusts.
    ///
    /// Trust within SHIP is not derived from a PKI: a SKI becomes trusted
    /// because a user accepted it (SHIP TS 1.0.1, chapter 12.3), and that
    /// decision has to survive a restart.
    /// </summary>
    public interface ISHIPTrustStore
    {

        /// <summary>
        /// All trusted SKIs.
        /// </summary>
        IEnumerable<SKI> TrustedSKIs { get; }

        /// <summary>
        /// Whether the given SKI is trusted.
        /// </summary>
        /// <param name="SKI">The SKI of a communication partner.</param>
        Boolean IsTrusted(SKI SKI);

        /// <summary>
        /// Trust the given SKI from now on.
        /// </summary>
        /// <param name="SKI">The SKI of a communication partner.</param>
        Task TrustAsync(SKI SKI);

        /// <summary>
        /// Do not trust the given SKI any more.
        /// </summary>
        /// <param name="SKI">The SKI of a communication partner.</param>
        Task DistrustAsync(SKI SKI);

    }


    /// <summary>
    /// A trust store which keeps its decisions in memory only.
    /// </summary>
    /// <param name="TrustedSKIs">Initially trusted SKIs.</param>
    public class InMemoryTrustStore(IEnumerable<SKI>? TrustedSKIs = null) : ISHIPTrustStore
    {

        #region Data

        private readonly ConcurrentDictionary<SKI, Byte> trustedSKIs
            = new (
                  (TrustedSKIs ?? []).Select(ski => new KeyValuePair<SKI, Byte>(ski, 0))
              );

        #endregion

        /// <summary>
        /// All trusted SKIs.
        /// </summary>
        public IEnumerable<SKI> TrustedSKIs
            => trustedSKIs.Keys;

        /// <summary>
        /// Whether the given SKI is trusted.
        /// </summary>
        public Boolean IsTrusted(SKI SKI)
            => trustedSKIs.ContainsKey(SKI);

        /// <summary>
        /// Trust the given SKI from now on.
        /// </summary>
        public virtual Task TrustAsync(SKI SKI)
        {
            trustedSKIs.TryAdd(SKI, 0);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Do not trust the given SKI any more.
        /// </summary>
        public virtual Task DistrustAsync(SKI SKI)
        {
            trustedSKIs.TryRemove(SKI, out _);
            return Task.CompletedTask;
        }

    }


    /// <summary>
    /// A trust store which persists its decisions within a JSON file.
    /// </summary>
    public class FileTrustStore : InMemoryTrustStore
    {

        #region Data

        private readonly String         filename;
        private readonly SemaphoreSlim  fileLock = new (1, 1);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a trust store persisting its decisions within the given file.
        /// </summary>
        /// <param name="Filename">The name of the JSON file.</param>
        public FileTrustStore(String Filename)

            : base(Load(Filename))

        {
            this.filename = Filename;
        }

        #endregion


        /// <summary>
        /// Trust the given SKI from now on.
        /// </summary>
        public override async Task TrustAsync(SKI SKI)
        {
            await base.TrustAsync(SKI);
            await SaveAsync();
        }

        /// <summary>
        /// Do not trust the given SKI any more.
        /// </summary>
        public override async Task DistrustAsync(SKI SKI)
        {
            await base.DistrustAsync(SKI);
            await SaveAsync();
        }


        #region (private) Load(Filename) / SaveAsync()

        private static IEnumerable<SKI> Load(String Filename)
        {

            if (!File.Exists(Filename))
                return [];

            try
            {

                var json  = JObject.Parse(File.ReadAllText(Filename));
                var skis  = new List<SKI>();

                if (json["trustedSKIs"] is JArray trustedSKIs)
                    foreach (var entry in trustedSKIs)
                    {
                        if (SKI.TryParse(entry.Value<String>() ?? "", out var ski, out _))
                            skis.Add(ski);
                    }

                return skis;

            }
            catch (Exception)
            {
                // A broken trust store must not prevent the node from starting;
                // it just means every communication partner has to be accepted again.
                return [];
            }

        }

        private async Task SaveAsync()
        {

            await fileLock.WaitAsync();

            try
            {

                var json = new JObject(
                               new JProperty("trustedSKIs",
                                   new JArray(TrustedSKIs.Select(ski => ski.ToString()))
                               )
                           );

                var directory = Path.GetDirectoryName(filename);

                if (directory is not null && directory.Length > 0)
                    Directory.CreateDirectory(directory);

                await File.WriteAllTextAsync(filename, json.ToString());

            }
            finally
            {
                fileLock.Release();
            }

        }

        #endregion

    }

}
