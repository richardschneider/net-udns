using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Provides the information to make a secure connection to a
    ///   DNS over TLS server.
    /// </summary>
    public class DotEndPoint
    {
        /// <summary>
        ///   The IP address.
        /// </summary>
        /// <value>
        ///   Can be IPv4 and IPv6.
        /// </value>
        public IPAddress Address;

        /// <summary>
        ///   The name of the host.
        /// </summary>
        /// <remarks>
        ///    Used to verify the TLS handshake.  Also known as Server Name Indication (SNI).
        /// </remarks>
        public string Hostname;

        /// <summary>
        ///   The SPKI Fingerprint of a valid certificate.
        /// </summary>
        /// <value>
        ///   The base-64 encoding of the SPKI Fingerprint.
        /// </value>
        /// <remarks>
        ///   The fingerprint is the SHA-256 hash of the DER-encoded
        ///   ASN.1 representation of the SPKI of an X.509 certificate.
        ///   <note>
        ///   Checking of PINS is not implemented, see <see href="https://github.com/richardschneider/net-udns/issues/5"/>.
        ///   </note>
        /// </remarks>
        public string[] Pins;

        /// <summary>
        ///   The TCP port.
        /// </summary>
        /// <value>
        ///   Defaults to 853.
        /// </value>
        public int Port = DotClient.DefaultPort;
    }
}
