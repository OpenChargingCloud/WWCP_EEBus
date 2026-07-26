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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using Microsoft.Extensions.Time.Testing;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP.tests
{

    /// <summary>
    /// Tests for SHIP node certificates (SHIP TS 1.0.1, chapter 12).
    /// </summary>
    [TestFixture]
    public class SHIPCertificateTests
    {

        #region GenerateCertificate_IsAShipNodeCertificate()

        [Test]
        public void GenerateCertificate_IsAShipNodeCertificate()
        {

            using var certificate = SHIPCertificates.GenerateCertificate("EVSE-12345678");

            Assert.Multiple(() => {

                // SHIP 9.1 requires ECDHE_ECDSA cipher suites, so the key has to be ECDSA.
                Assert.That(certificate.GetECDsaPublicKey(),                        Is.Not.Null);
                Assert.That(certificate.GetRSAPublicKey(),                          Is.Null);
                Assert.That(certificate.HasPrivateKey,                              Is.True);
                Assert.That(certificate.SignatureAlgorithm.FriendlyName,            Does.Contain("ecdsa").IgnoreCase);

                // Chapter 12.2: the certificate has to carry the SKI of its public key.
                Assert.That(certificate.Extensions.OfType<X509SubjectKeyIdentifierExtension>().Any(), Is.True);

                Assert.That(certificate.Subject,                                    Does.Contain("EVSE-12345678"));

            });

        }

        #endregion

        #region GenerateCertificate_UsesTheP256Curve()

        [Test]
        public void GenerateCertificate_UsesTheP256Curve()
        {

            using var certificate = SHIPCertificates.GenerateCertificate("EVSE-12345678");
            using var publicKey   = certificate.GetECDsaPublicKey();

            var parameters = publicKey!.ExportParameters(false);

            Assert.That(parameters.Curve.Oid.Value, Is.EqualTo(ECCurve.NamedCurves.nistP256.Oid.Value));

        }

        #endregion

        #region TryGetSKI_MatchesTheSubjectKeyIdentifierExtension()

        /// <summary>
        /// The SKI is recomputed from the public key and compared with the
        /// extension, so that a certificate cannot claim a foreign identity.
        /// </summary>
        [Test]
        public void TryGetSKI_MatchesTheSubjectKeyIdentifierExtension()
        {

            using var certificate = SHIPCertificates.GenerateCertificate("EVSE-12345678");

            Assert.That(SHIPCertificates.TryGetSKI(certificate, out var ski, out var errorResponse), Is.True, errorResponse);

            var extension = certificate.Extensions.OfType<X509SubjectKeyIdentifierExtension>().First();

            Assert.Multiple(() => {
                Assert.That(ski.ToString().Length,  Is.EqualTo(40));
                Assert.That(ski,                    Is.EqualTo(SKI.Parse(extension.SubjectKeyIdentifier!)));
            });

        }

        #endregion

        #region TryGetSKI_IsStableAcrossExportAndImport()

        [Test]
        public void TryGetSKI_IsStableAcrossExportAndImport()
        {

            using var certificate = SHIPCertificates.GenerateCertificate("EVSE-12345678");
            using var reimported  = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));

            Assert.That(SHIPCertificates.TryGetSKI(certificate, out var ski1, out _), Is.True);
            Assert.That(SHIPCertificates.TryGetSKI(reimported,  out var ski2, out _), Is.True);

            Assert.That(ski2, Is.EqualTo(ski1));

        }

        #endregion

        #region TryGetSKI_DifferentCertificates_HaveDifferentSKIs()

        /// <summary>
        /// Chapter 12.1.1: one key pair must not be used for more than one
        /// certificate, and one certificate not for more than one SHIP node.
        /// </summary>
        [Test]
        public void TryGetSKI_DifferentCertificates_HaveDifferentSKIs()
        {

            using var certificate1 = SHIPCertificates.GenerateCertificate("EVSE-1");
            using var certificate2 = SHIPCertificates.GenerateCertificate("EVSE-2");

            Assert.That(SHIPCertificates.TryGetSKI(certificate1, out var ski1, out _), Is.True);
            Assert.That(SHIPCertificates.TryGetSKI(certificate2, out var ski2, out _), Is.True);

            Assert.That(ski1, Is.Not.EqualTo(ski2));

        }

        #endregion

        #region TryGetSKI_ForeignSubjectKeyIdentifier_Fails()

        /// <summary>
        /// A certificate announcing a SKI that does not belong to its public key
        /// must not be accepted - it would allow impersonating another SHIP node.
        /// </summary>
        [Test]
        public void TryGetSKI_ForeignSubjectKeyIdentifier_Fails()
        {

            using var privateKey  = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var request = new CertificateRequest(
                              new X500DistinguishedName("CN=Impostor"),
                              privateKey,
                              HashAlgorithmName.SHA256
                          );

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(
                    SKI.Parse("6ff5e2d2b1a41c9e0b2d3f4a5b6c7d8e9f001122").ToByteArray(),
                    false
                )
            );

            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
                                                             DateTimeOffset.UtcNow.AddDays(+1));

            Assert.Multiple(() => {
                Assert.That(SHIPCertificates.TryGetSKI(certificate, out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                                          Does.Contain("does not match its public key"));
            });

        }

        #endregion

        #region TryGetSKI_WithoutSubjectKeyIdentifier_Fails()

        [Test]
        public void TryGetSKI_WithoutSubjectKeyIdentifier_Fails()
        {

            using var privateKey  = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var request = new CertificateRequest(
                              new X500DistinguishedName("CN=NoSKI"),
                              privateKey,
                              HashAlgorithmName.SHA256
                          );

            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
                                                             DateTimeOffset.UtcNow.AddDays(+1));

            Assert.Multiple(() => {
                Assert.That(SHIPCertificates.TryGetSKI(certificate, out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                                          Does.Contain("subject key identifier"));
            });

        }

        #endregion

        #region GenerateCertificate_UsesTheGivenTimeProvider()

        /// <summary>
        /// Certificate lifetimes are protocol behaviour that has to be testable
        /// without waiting - so even here the time comes from a TimeProvider.
        /// </summary>
        [Test]
        public void GenerateCertificate_UsesTheGivenTimeProvider()
        {

            var timeProvider = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));

            using var certificate = SHIPCertificates.GenerateCertificate(
                                        "EVSE-12345678",
                                        Lifetime:      TimeSpan.FromDays(365),
                                        TimeProvider:  timeProvider
                                    );

            Assert.Multiple(() => {
                Assert.That(certificate.NotBefore.ToUniversalTime().Year,  Is.EqualTo(2030));
                Assert.That(certificate.NotAfter. ToUniversalTime().Year,  Is.EqualTo(2031));
            });

        }

        #endregion

        #region GetFingerprint_IsTheUpperCaseSHA256OfTheCertificate()

        [Test]
        public void GetFingerprint_IsTheUpperCaseSHA256OfTheCertificate()
        {

            using var certificate = SHIPCertificates.GenerateCertificate("EVSE-12345678");

            var fingerprint = SHIPCertificates.GetFingerprint(certificate);

            Assert.Multiple(() => {
                Assert.That(fingerprint.Length,  Is.EqualTo(64));
                Assert.That(fingerprint,         Is.EqualTo(fingerprint.ToUpperInvariant()));
                Assert.That(fingerprint,         Is.EqualTo(Convert.ToHexString(SHA256.HashData(certificate.RawData))));
            });

        }

        #endregion

    }

}
