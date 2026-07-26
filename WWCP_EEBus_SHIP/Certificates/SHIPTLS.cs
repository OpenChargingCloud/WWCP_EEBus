/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

#region Usings

using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// A remote SHIP node presented a certificate during the TLS handshake.
    /// </summary>
    /// <param name="SKI">The SKI of the remote SHIP node.</param>
    /// <param name="Certificate">The certificate of the remote SHIP node.</param>
    public delegate void OnRemoteSKIDelegate(SKI               SKI,
                                             X509Certificate2  Certificate);


    /// <summary>
    /// The TLS profile of SHIP (SHIP TS 1.0.1, chapter 9).
    ///
    /// SHIP requires TLS 1.2 with mutual authentication: both communication
    /// partners always present a certificate. The certificates are usually
    /// self-signed, so the usual chain validation is meaningless here - the
    /// identity of a SHIP node is the SKI of its public key, and whether that
    /// SKI is trusted is decided afterwards, during the connection data
    /// preparation ("Hello", chapter 13.4.4.1).
    /// </summary>
    public static class SHIPTLS
    {

        #region Data

        private static readonly Lazy<CipherSuitesPolicy?> cipherSuitesPolicy = new(CreateCipherSuitesPolicy);

        #endregion

        #region Properties

        /// <summary>
        /// The cipher suites defined by SHIP TS 1.0.1, chapter 9.1.
        ///
        /// TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256 is required,
        /// TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256 is optional. Both are
        /// nowadays considered weak, but SHIP 1.0.1 defines them.
        /// </summary>
        public static IEnumerable<TlsCipherSuite>  CipherSuites          { get; }
            = [
                  TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256,
                  TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256
              ];

        /// <summary>
        /// The TLS versions accepted by this implementation.
        ///
        /// SHIP TS 1.0.1 mandates TLS 1.2; TLS 1.3 is allowed additionally, as
        /// the Go reference implementation does, so that newer communication
        /// partners are not rejected.
        /// </summary>
        public static SslProtocols                 Protocols             { get; }
            = SslProtocols.Tls12 | SslProtocols.Tls13;

        /// <summary>
        /// Whether this platform allows restricting the TLS cipher suites.
        ///
        /// Windows/SChannel does not: the cipher suites are configured system
        /// wide there. This is not a problem in practice, because Windows offers
        /// the GCM variant of the SHIP cipher suites by default - but a
        /// communication partner insisting on the CBC variant may fail to
        /// connect if that suite is disabled on the system.
        /// </summary>
        public static Boolean                      SupportsCipherSuitesPolicy
            => cipherSuitesPolicy.Value is not null;

        /// <summary>
        /// The cipher suites policy of SHIP, or null on platforms which do not
        /// support restricting the cipher suites.
        /// </summary>
        public static CipherSuitesPolicy?          SHIPCipherSuitesPolicy
            => cipherSuitesPolicy.Value;

        #endregion


        #region ClientOptions(Certificate, TargetHost = null, OnRemoteSKI = null)

        /// <summary>
        /// Return the TLS options of a SHIP client.
        /// </summary>
        /// <param name="Certificate">The certificate of the local SHIP node, including its private key.</param>
        /// <param name="TargetHost">An optional target host for the server name indication (SHIP TS 1.0.1, chapter 9.4).</param>
        /// <param name="OnRemoteSKI">An optional delegate called with the SKI of the remote SHIP node.</param>
        public static SslClientAuthenticationOptions ClientOptions(X509Certificate2      Certificate,
                                                                   String?               TargetHost    = null,
                                                                   OnRemoteSKIDelegate?  OnRemoteSKI   = null)

            => new () {

                   TargetHost                        = TargetHost ?? "",
                   ClientCertificates                = [ Certificate ],
                   EnabledSslProtocols               = Protocols,
                   CipherSuitesPolicy                = SHIPCipherSuitesPolicy,

                   // SHIP TS 1.0.1, chapter 12.1: certificates are usually self-signed
                   // and trust is based on the SKI, not on a certificate chain.
                   RemoteCertificateValidationCallback = (sender, certificate, chain, errors)
                                                             => ValidateRemoteCertificate(certificate, OnRemoteSKI),

                   CertificateRevocationCheckMode    = X509RevocationMode.NoCheck

               };

        #endregion

        #region ServerOptions(Certificate, OnRemoteSKI = null)

        /// <summary>
        /// Return the TLS options of a SHIP server.
        /// </summary>
        /// <param name="Certificate">The certificate of the local SHIP node, including its private key.</param>
        /// <param name="OnRemoteSKI">An optional delegate called with the SKI of the remote SHIP node.</param>
        public static SslServerAuthenticationOptions ServerOptions(X509Certificate2      Certificate,
                                                                   OnRemoteSKIDelegate?  OnRemoteSKI   = null)

            => new () {

                   ServerCertificate                 = Certificate,

                   // SHIP TS 1.0.1, chapter 9 and 12.1.1: mutual authentication,
                   // a SHIP client MUST present a certificate as well.
                   ClientCertificateRequired         = true,

                   EnabledSslProtocols               = Protocols,
                   CipherSuitesPolicy                = SHIPCipherSuitesPolicy,

                   RemoteCertificateValidationCallback = (sender, certificate, chain, errors)
                                                             => ValidateRemoteCertificate(certificate, OnRemoteSKI),

                   CertificateRevocationCheckMode    = X509RevocationMode.NoCheck

               };

        #endregion


        #region ValidateRemoteCertificate(Certificate, OnRemoteSKI = null)

        /// <summary>
        /// Validate the certificate of a remote SHIP node.
        ///
        /// Anything a PKI would check is deliberately ignored here: SHIP TS 1.0.1,
        /// chapter 12.1.1 demands that a failing optional check - an expired
        /// certificate, an unknown issuer - must not prevent communication as long
        /// as the public key is authenticated. What a certificate must provide is
        /// a usable SHIP identity: an ECDSA public key and a matching SKI.
        /// </summary>
        /// <param name="Certificate">The certificate of the remote SHIP node.</param>
        /// <param name="OnRemoteSKI">An optional delegate called with the SKI of the remote SHIP node.</param>
        public static Boolean ValidateRemoteCertificate(X509Certificate?      Certificate,
                                                        OnRemoteSKIDelegate?  OnRemoteSKI   = null)
        {

            if (Certificate is null)
                return false;

            var certificate = Certificate as X509Certificate2
                                  ?? X509CertificateLoader.LoadCertificate(Certificate.GetRawCertData());

            if (!SHIPCertificates.TryGetSKI(certificate, out var ski, out _))
                return false;

            OnRemoteSKI?.Invoke(ski, certificate);

            return true;

        }

        #endregion


        #region (private) CreateCipherSuitesPolicy()

        private static CipherSuitesPolicy? CreateCipherSuitesPolicy()
        {
            try
            {
                return new CipherSuitesPolicy(CipherSuites);
            }
            catch (PlatformNotSupportedException)
            {
                // Windows/SChannel: the cipher suites cannot be restricted per connection.
                return null;
            }
        }

        #endregion

    }

}
