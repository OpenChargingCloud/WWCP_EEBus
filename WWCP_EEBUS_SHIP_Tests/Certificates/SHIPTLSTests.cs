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

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP.tests
{

    /// <summary>
    /// Tests for the SHIP TLS profile (SHIP TS 1.0.1, chapter 9).
    /// </summary>
    [TestFixture]
    public class SHIPTLSTests
    {

        #region CipherSuites_AreThoseOfTheSpecification()

        [Test]
        public void CipherSuites_AreThoseOfTheSpecification()
        {

            Assert.Multiple(() => {

                // SHIP 9.1: required cipher suite
                Assert.That(SHIPTLS.CipherSuites, Does.Contain(TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256));

                // SHIP 9.1: optional cipher suite
                Assert.That(SHIPTLS.CipherSuites, Does.Contain(TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256));

                // SHIP 9: TLS 1.2 is mandatory
                Assert.That(SHIPTLS.Protocols.HasFlag(SslProtocols.Tls12), Is.True);

                // The property quotes the specification and nothing else: the TLS 1.3
                // suites are part of the policy, but not of chapter 9.1.
                Assert.That(SHIPTLS.CipherSuites, Has.No.Member(TlsCipherSuite.TLS_AES_128_GCM_SHA256));

            });

        }

        #endregion

        #region CipherSuitesPolicy_CoversEveryEnabledProtocol()

        /// <summary>
        /// A cipher suites policy has to offer suites for every enabled TLS version.
        /// TLS 1.3 uses cipher suites of its own, so a policy holding only the TLS 1.2
        /// suites of SHIP 9.1 leaves TLS 1.3 without any - and .NET/OpenSSL then refuses
        /// the connection outright ("The 'RequireEncryption' encryption policy is not
        /// supported by this installation of OpenSSL") instead of just not negotiating
        /// TLS 1.3.
        /// </summary>
        [Test]
        public void CipherSuitesPolicy_CoversEveryEnabledProtocol()
        {

            if (!SHIPTLS.SupportsCipherSuitesPolicy)
                Assert.Ignore("This platform does not support a per connection cipher suites policy.");

            // CA1416: guarded above - the policy is null exactly where the platform
            // does not support it, and Assert.Ignore has already ended the test then.
            #pragma warning disable CA1416
            var allowed = SHIPTLS.SHIPCipherSuitesPolicy!.AllowedCipherSuites;
            #pragma warning restore CA1416

            Assert.Multiple(() => {

                if (SHIPTLS.Protocols.HasFlag(SslProtocols.Tls12))
                    Assert.That(allowed, Does.Contain(TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256),
                                "TLS 1.2 is enabled, but the policy holds none of its suites.");

                if (SHIPTLS.Protocols.HasFlag(SslProtocols.Tls13))
                    Assert.That(SHIPTLS.TLS13CipherSuites.Intersect(allowed), Is.Not.Empty,
                                "TLS 1.3 is enabled, but the policy holds none of its suites.");

            });

        }

        #endregion

        #region CipherSuitesPolicy_ReflectsThePlatform()

        /// <summary>
        /// Windows/SChannel cannot restrict the cipher suites per connection.
        /// The stack has to cope with that instead of crashing.
        /// </summary>
        [Test]
        public void CipherSuitesPolicy_ReflectsThePlatform()
        {

            if (OperatingSystem.IsWindows())
                Assert.That(SHIPTLS.SupportsCipherSuitesPolicy, Is.False,
                            "Windows/SChannel does not support a per connection cipher suites policy.");

            else
                Assert.That(SHIPTLS.SupportsCipherSuitesPolicy, Is.True);

            // Either way the options have to be usable.
            using var certificate = SHIPCertificates.GenerateCertificate("EVSE-12345678");

            Assert.Multiple(() => {
                Assert.That(SHIPTLS.ClientOptions(certificate),  Is.Not.Null);
                Assert.That(SHIPTLS.ServerOptions(certificate),  Is.Not.Null);
            });

        }

        #endregion

        #region ServerOptions_RequireAClientCertificate()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 12.1.1: a SHIP node MUST always provide a
        /// certificate, no matter whether it acts as client or as server.
        /// </summary>
        [Test]
        public void ServerOptions_RequireAClientCertificate()
        {

            using var certificate = SHIPCertificates.GenerateCertificate("EVSE-12345678");

            var options = SHIPTLS.ServerOptions(certificate);

            Assert.Multiple(() => {
                Assert.That(options.ClientCertificateRequired,      Is.True);
                Assert.That(options.ServerCertificate,              Is.EqualTo(certificate));
                Assert.That(options.CertificateRevocationCheckMode, Is.EqualTo(X509RevocationMode.NoCheck));
            });

        }

        #endregion

        #region ValidateRemoteCertificate_SelfSignedCertificate_IsAccepted()

        /// <summary>
        /// Chapter 12.1.1: trust is based on the public key, not on a PKI, so a
        /// self-signed certificate has to be accepted at TLS level.
        /// </summary>
        [Test]
        public void ValidateRemoteCertificate_SelfSignedCertificate_IsAccepted()
        {

            using var certificate = SHIPCertificates.GenerateCertificate("EVSE-12345678");

            SKI? reportedSKI = null;

            var accepted = SHIPTLS.ValidateRemoteCertificate(
                               certificate,
                               (ski, cert) => reportedSKI = ski
                           );

            Assert.Multiple(() => {
                Assert.That(accepted,     Is.True);
                Assert.That(reportedSKI,  Is.Not.Null);
            });

        }

        #endregion

        #region ValidateRemoteCertificate_ExpiredCertificate_IsStillAccepted()

        /// <summary>
        /// Chapter 12.1.1: a failing lifetime check must not prevent the
        /// communication as long as the public key is authenticated. Whether the
        /// SKI is trusted is decided later, during the "Hello" phase.
        /// </summary>
        [Test]
        public void ValidateRemoteCertificate_ExpiredCertificate_IsStillAccepted()
        {

            using var certificate = SHIPCertificates.GenerateCertificate(
                                        "EVSE-12345678",
                                        Lifetime:  TimeSpan.FromDays(1),
                                        TimeProvider: new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
                                                          DateTimeOffset.UtcNow.AddYears(-5)
                                                      )
                                    );

            Assert.Multiple(() => {
                Assert.That(certificate.NotAfter.ToUniversalTime(),                Is.LessThan(DateTime.UtcNow));
                Assert.That(SHIPTLS.ValidateRemoteCertificate(certificate),        Is.True);
            });

        }

        #endregion

        #region ValidateRemoteCertificate_WithoutSKI_IsRejected()

        [Test]
        public void ValidateRemoteCertificate_WithoutSKI_IsRejected()
        {

            using var privateKey = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);

            var request = new CertificateRequest(
                              new X500DistinguishedName("CN=NoSKI"),
                              privateKey,
                              System.Security.Cryptography.HashAlgorithmName.SHA256
                          );

            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
                                                             DateTimeOffset.UtcNow.AddDays(+1));

            Assert.That(SHIPTLS.ValidateRemoteCertificate(certificate), Is.False);

        }

        #endregion

        #region MutualAuthentication_OverTCP_Succeeds()

        /// <summary>
        /// A complete TLS handshake between two SHIP nodes over a real socket:
        /// both sides present a certificate and learn the SKI of the other side.
        /// </summary>
        [Test]
        public async Task MutualAuthentication_OverTCP_Succeeds()
        {

            using var serverCertificate = SHIPCertificates.GenerateCertificate("EVSE-Server");
            using var clientCertificate = SHIPCertificates.GenerateCertificate("CEM-Client");

            Assert.That(SHIPCertificates.TryGetSKI(serverCertificate, out var serverSKI, out _), Is.True);
            Assert.That(SHIPCertificates.TryGetSKI(clientCertificate, out var clientSKI, out _), Is.True);

            SKI? skiSeenByServer = null;
            SKI? skiSeenByClient = null;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var port = ((IPEndPoint) listener.LocalEndpoint).Port;

            var serverTask = Task.Run(async () => {

                using var tcpClient  = await listener.AcceptTcpClientAsync();
                using var sslStream  = new SslStream(tcpClient.GetStream(), false);

                await sslStream.AuthenticateAsServerAsync(
                          SHIPTLS.ServerOptions(
                              serverCertificate,
                              (ski, certificate) => skiSeenByServer = ski
                          )
                      );

                var buffer = new Byte[2];
                await sslStream.ReadExactlyAsync(buffer);
                await sslStream.WriteAsync(buffer);
                await sslStream.FlushAsync();

                return sslStream.SslProtocol;

            });

            using var client     = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);

            using var clientSsl  = new SslStream(client.GetStream(), false);

            await clientSsl.AuthenticateAsClientAsync(
                      SHIPTLS.ClientOptions(
                          clientCertificate,
                          "localhost",
                          (ski, certificate) => skiSeenByClient = ski
                      )
                  );

            // The SHIP "Connection Mode Initialisation" message, as the very
            // first thing that would go over this connection.
            await clientSsl.WriteAsync(SHIPFrame.Init.ToByteArray());
            await clientSsl.FlushAsync();

            var echo = new Byte[2];
            await clientSsl.ReadExactlyAsync(echo);

            var serverProtocol = await serverTask;
            listener.Stop();

            Assert.Multiple(() => {

                Assert.That(clientSsl.IsAuthenticated,      Is.True);
                Assert.That(clientSsl.IsMutuallyAuthenticated, Is.True, "SHIP requires mutual authentication.");

                Assert.That(skiSeenByServer,                Is.EqualTo(clientSKI), "The server has to learn the SKI of the client.");
                Assert.That(skiSeenByClient,                Is.EqualTo(serverSKI), "The client has to learn the SKI of the server.");

                Assert.That(echo,                           Is.EqualTo(new Byte[] { 0x00, 0x00 }));

                Assert.That(serverProtocol,                 Is.AnyOf(SslProtocols.Tls12, SslProtocols.Tls13));

            });

        }

        #endregion

    }

}
