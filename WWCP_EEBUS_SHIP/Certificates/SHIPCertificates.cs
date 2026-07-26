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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// Creation and inspection of SHIP node certificates
    /// (SHIP TS 1.0.1, chapter 12 "Key Management").
    ///
    /// A SHIP node MUST always provide a certificate during the TLS handshake -
    /// no matter whether it acts as client or as server - and trust is based on
    /// the SKI of its public key, not on a PKI.
    /// </summary>
    public static class SHIPCertificates
    {

        #region Data

        /// <summary>
        /// The default validity period of a generated SHIP node certificate.
        ///
        /// SHIP does not require a certain lifetime: chapter 12.1.1 states that a
        /// failing lifetime check must not prevent communication when the public
        /// key is trusted.
        /// </summary>
        public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(365 * 10);

        #endregion


        #region GenerateCertificate(CommonName, Organization = null, OrganizationalUnit = null, Country = null, Lifetime = null, TimeProvider = null)

        /// <summary>
        /// Generate a new self-signed SHIP node certificate along with its
        /// ECDSA P-256 key pair.
        /// </summary>
        /// <param name="CommonName">The common name, e.g. "deviceModel-deviceSerialNumber". SHIP nodes should ignore this field (chapter 12.1.1).</param>
        /// <param name="Organization">An optional organization.</param>
        /// <param name="OrganizationalUnit">An optional organizational unit.</param>
        /// <param name="Country">An optional two letter country code.</param>
        /// <param name="Lifetime">The validity period of the certificate.</param>
        /// <param name="TimeProvider">An optional time provider, e.g. for testing certificate expiration.</param>
        public static X509Certificate2 GenerateCertificate(String         CommonName,
                                                           String?        Organization         = null,
                                                           String?        OrganizationalUnit   = null,
                                                           String?        Country              = null,
                                                           TimeSpan?      Lifetime             = null,
                                                           TimeProvider?  TimeProvider         = null)
        {

            // SHIP 9.1 requires ECDHE_ECDSA cipher suites, which implies an
            // ECDSA key on the NIST P-256 curve.
            var privateKey     = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var subject        = new List<String>();

            if (CommonName.        IsNotNullOrEmpty())  subject.Add($"CN={CommonName}");
            if (OrganizationalUnit.IsNotNullOrEmpty())  subject.Add($"OU={OrganizationalUnit}");
            if (Organization.      IsNotNullOrEmpty())  subject.Add($"O={Organization}");
            if (Country.           IsNotNullOrEmpty())  subject.Add($"C={Country}");

            var request        = new CertificateRequest(
                                     new X500DistinguishedName(String.Join(", ", subject)),
                                     privateKey,
                                     HashAlgorithmName.SHA256
                                 );

            // The SKI is the identity of the SHIP node and therefore has to be
            // part of the certificate (chapter 12.2).
            var ski            = ComputeSKI(privateKey);

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(ski.ToByteArray(), false)
            );

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false)
            );

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, false, 0, true)
            );

            var timeProvider   = TimeProvider ?? System.TimeProvider.System;
            var notBefore      = timeProvider.GetUtcNow();
            var notAfter       = notBefore + (Lifetime ?? DefaultLifetime);

            var certificate    = request.CreateSelfSigned(notBefore, notAfter);

            // Windows/SChannel refuses to use a certificate with an ephemeral private
            // key within a TLS handshake ("unexpected EOF from the transport stream").
            // Exporting and re-importing the certificate gives it a key SChannel accepts.
            if (OperatingSystem.IsWindows())
            {

                var pkcs12 = certificate.Export(X509ContentType.Pkcs12);
                certificate.Dispose();

                return X509CertificateLoader.LoadPkcs12(
                           pkcs12,
                           null,
                           X509KeyStorageFlags.Exportable
                       );

            }

            return certificate;

        }

        #endregion

        #region ComputeSKI  (PublicKey)

        /// <summary>
        /// Compute the Subject Key Identifier of the given key, as described in
        /// RFC 3280, chapter 4.2.1.2, method (1): the SHA-1 hash of the
        /// subjectPublicKey BIT STRING.
        /// </summary>
        /// <param name="PublicKey">An ECDSA key.</param>
        public static SKI ComputeSKI(ECDsa PublicKey)
        {

            var parameters = PublicKey.ExportParameters(false);

            if (parameters.Q.X is null || parameters.Q.Y is null)
                throw new ArgumentException("The given key does not provide a public point!",
                                            nameof(PublicKey));

            // The subjectPublicKey of an EC key is the uncompressed point:
            // 0x04 || X || Y (SEC 1, chapter 2.3.3).
            var publicKey = new Byte[1 + parameters.Q.X.Length + parameters.Q.Y.Length];

            publicKey[0] = 0x04;
            parameters.Q.X.CopyTo(publicKey, 1);
            parameters.Q.Y.CopyTo(publicKey, 1 + parameters.Q.X.Length);

            // SHA-1 is required by RFC 3280 method (1) and thus by SHIP; it is
            // used as an identifier here, not as a security primitive.
#pragma warning disable CA5350
            var hash = SHA1.HashData(publicKey);
#pragma warning restore CA5350

            if (!SKI.TryParseBytes(hash, out var ski, out var errorResponse))
                throw new InvalidOperationException(errorResponse);

            return ski;

        }

        #endregion

        #region TryGetSKI   (Certificate, out SKI, out ErrorResponse)

        /// <summary>
        /// Try to get the SKI of the given SHIP node certificate.
        ///
        /// The SKI is recomputed from the public key of the certificate and
        /// compared with the subject key identifier extension, so that a
        /// certificate cannot claim an identity that does not belong to its key.
        /// </summary>
        /// <param name="Certificate">A SHIP node certificate.</param>
        /// <param name="SKI">The SKI of the certificate.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryGetSKI(X509Certificate2                  Certificate,
                                        out SKI                           SKI,
                                        [NotNullWhen(false)] out String?  ErrorResponse)
        {

            SKI            = default;
            ErrorResponse  = null;

            var publicKey  = Certificate.GetECDsaPublicKey();

            if (publicKey is null)
            {
                ErrorResponse = "SHIP node certificates must contain an ECDSA public key (SHIP TS 1.0.1, chapter 9.1)!";
                return false;
            }

            SKI computedSKI;

            try
            {
                computedSKI = ComputeSKI(publicKey);
            }
            catch (Exception e)
            {
                ErrorResponse = "The SKI of the given certificate could not be computed: " + e.Message;
                return false;
            }

            var extension = Certificate.Extensions.OfType<X509SubjectKeyIdentifierExtension>().FirstOrDefault();

            if (extension is null)
            {
                ErrorResponse = "The given certificate does not contain a subject key identifier extension (SHIP TS 1.0.1, chapter 12.2)!";
                return false;
            }

            if (!SKI.TryParse(extension.SubjectKeyIdentifier ?? "", out var announcedSKI, out ErrorResponse))
                return false;

            if (announcedSKI != computedSKI)
            {
                ErrorResponse = $"The subject key identifier of the given certificate ('{announcedSKI}') does not match its public key ('{computedSKI}')!";
                return false;
            }

            SKI = computedSKI;
            return true;

        }

        #endregion

        #region GetFingerprint(Certificate)

        /// <summary>
        /// Return the SHA-256 fingerprint of the given certificate as upper case
        /// hexadecimal digits, as used by the SHIP pairing service.
        /// </summary>
        /// <param name="Certificate">A SHIP node certificate.</param>
        public static String GetFingerprint(X509Certificate2 Certificate)

            => Convert.ToHexString(SHA256.HashData(Certificate.RawData));

        #endregion

    }

}
