﻿using System;
using System.IO;
using System.Threading.Tasks;

namespace S7.Net
{

    /// <summary>
    /// COTP Protocol functions and types
    /// </summary>
    internal class COTP
    {
        /// <summary>
        /// Describes a COTP TPDU (Transport protocol data unit)
        /// </summary>
        public class TPDU
        {
            public TPKT TPkt { get; }
            public byte HeaderLength;
            public byte PDUType;
            public int TPDUNumber;
            public byte[] Data;
            public bool LastDataUnit;

            public TPDU(TPKT tPKT)
            {
                TPkt = tPKT;

                HeaderLength = tPKT.Data[0]; // Header length excluding this length byte
                if (HeaderLength >= 2)
                {
                    PDUType = tPKT.Data[1];
                    if (PDUType == 0xf0) //DT Data
                    {
                        var flags = tPKT.Data[2];
                        TPDUNumber = flags & 0x7F;
                        LastDataUnit = (flags & 0x80) > 0;
                        Data = new byte[tPKT.Data.Length - HeaderLength - 1]; // substract header length byte + header length.
                        Array.Copy(tPKT.Data, HeaderLength + 1, Data, 0, Data.Length);
                        return;
                    }
                    //TODO: Handle other PDUTypes
                }
                Data = new byte[0];
            }

            /// <summary>
            /// Reads COTP TPDU (Transport protocol data unit) from the network stream
            /// See: https://tools.ietf.org/html/rfc905
            /// </summary>
            /// <param name="stream">The socket to read from</param>
            /// <returns>COTP DPDU instance</returns>
            public static TPDU Read(Stream stream)
            {
                var tpkt = TPKT.Read(stream);
                if (tpkt.Length == 0)
                {
                    throw new TPDUInvalidException("No protocol data received");
                }
                return new TPDU(tpkt);
            }

            /// <summary>
            /// Reads COTP TPDU (Transport protocol data unit) from the network stream
            /// See: https://tools.ietf.org/html/rfc905
            /// </summary>
            /// <param name="stream">The socket to read from</param>
            /// <returns>COTP DPDU instance</returns>
            public static async Task<TPDU> ReadAsync(Stream stream)
            {
                var tpkt = await TPKT.ReadAsync(stream);
                if (tpkt.Length == 0)
                {
                    throw new TPDUInvalidException("No protocol data received");
                }
                return new TPDU(tpkt);
            }

            public override string ToString()
            {
                return string.Format("Length: {0} PDUType: {1} TPDUNumber: {2} Last: {3} Segment Data: {4}",
                    HeaderLength,
                    PDUType,
                    TPDUNumber,
                    LastDataUnit,
                    BitConverter.ToString(Data)
                    );
            }

        }

        /// <summary>
        /// Describes a COTP TSDU (Transport service data unit). One TSDU consist of 1 ore more TPDUs
        /// </summary>
        public class TSDU
        {
            /// <summary>
            /// Reads the full COTP TSDU (Transport service data unit)
            /// See: https://tools.ietf.org/html/rfc905
            /// </summary>
            /// <param name="stream">The stream to read from</param>
            /// <returns>Data in TSDU</returns>
            public static byte[] Read(Stream stream)
            {
                var segment = TPDU.Read(stream);

                if (segment.LastDataUnit)
                {
                    return segment.Data;
                }

                // More segments are expected, prepare a buffer to store all data
                var buffer = new byte[segment.Data.Length];
                Array.Copy(segment.Data, buffer, segment.Data.Length);

                while (!segment.LastDataUnit)
                {
                    segment = TPDU.Read(stream);
                    var previousLength = buffer.Length;
                    Array.Resize(ref buffer, buffer.Length + segment.Data.Length);
                    Array.Copy(segment.Data, 0, buffer, previousLength, segment.Data.Length);
                }

                return buffer;
            }

            /// <summary>
            /// Reads the full COTP TSDU (Transport service data unit)
            /// See: https://tools.ietf.org/html/rfc905
            /// </summary>
            /// <param name="stream">The stream to read from</param>
            /// <returns>Data in TSDU</returns>
            public static async Task<byte[]> ReadAsync(Stream stream)
            {                
                var segment = await TPDU.ReadAsync(stream);

                if (segment.LastDataUnit)
                {
                    return segment.Data;
                }

                // More segments are expected, prepare a buffer to store all data
                var buffer = new byte[segment.Data.Length];
                Array.Copy(segment.Data, buffer, segment.Data.Length);

                while (!segment.LastDataUnit)
                {
                    segment = await TPDU.ReadAsync(stream);
                    var previousLength = buffer.Length;
                    Array.Resize(ref buffer, buffer.Length + segment.Data.Length);
                    Array.Copy(segment.Data, 0, buffer, previousLength, segment.Data.Length);
                }

                return buffer;
            }
        }
    }
}
