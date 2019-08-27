﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dcomms.DRP.Packets
{


    /// <summary>
    /// is sent from next hop to previous hop, when the next hop receives some packet from neighbor, or from registering peer (EP->A).
    /// stops UDP retransmission of a request packet
    /// </summary>
    class NextHopAckPacket
    {
        public NextHopAckSequenceNumber16 NhaSeq16;
        public const byte Flag_EPtoA = 0x01; // set if packet is transmitted from EP to A, is zero otherwise
        byte Flags;
        public P2pConnectionToken32 SenderToken32; // is not transmitted in EP->A packet
        public NextHopResponseCode StatusCode;
        /// <summary>
        /// signature of sender neighbor peer
        /// is NULL for EP->A packet
        /// uses common secret of neighbors within P2P connection
        /// </summary>
        public HMAC SenderHMAC;

        public NextHopAckPacket()
        { }
        public static LowLevelUdpResponseScanner GetScanner(NextHopAckSequenceNumber16 nhaSeq16)
        {
            PacketProcedures.CreateBinaryWriter(out var ms, out var w);
            EncodeHeader(w, nhaSeq16);
            var r = new LowLevelUdpResponseScanner { ResponseFirstBytes = ms.ToArray() };
            return r;
        }
        static void EncodeHeader(BinaryWriter w, NextHopAckSequenceNumber16 nhaSeq16)
        {
            w.Write((byte)DrpPacketType.NextHopAckPacket);
            nhaSeq16.Encode(w);
        }
        public byte[] Encode(bool rpToA)
        {
            PacketProcedures.CreateBinaryWriter(out var ms, out var writer);
            EncodeHeader(writer, NhaSeq16);
            byte flags = 0;
            if (rpToA) flags |= Flag_EPtoA;
            writer.Write(flags);
            if (rpToA == false) SenderToken32.Encode(writer);
            writer.Write((byte)StatusCode);
            if (rpToA == false) SenderHMAC.Encode(writer);
            return ms.ToArray();
        }
        public NextHopAckPacket(byte[] nextHopResponsePacketData)
        {
            var reader = PacketProcedures.CreateBinaryReader(nextHopResponsePacketData, 1);
            NhaSeq16 = NextHopAckSequenceNumber16.Decode(reader);
            var flags = reader.ReadByte();
            if ((flags & Flag_EPtoA) == 0) SenderToken32 = P2pConnectionToken32.Decode(reader);
            StatusCode = (NextHopResponseCode)reader.ReadByte();
            if ((flags & Flag_EPtoA) == 0) SenderHMAC = HMAC.Decode(reader);
        }
    }
    enum NextHopResponseCode
    {
        accepted, // is sent to previous hop immediately when packet is proxied, to stop retransmission timer
        rejected_overloaded,
        rejected_rateExceeded, // anti-ddos
    }



    class DrpTimeoutException : ApplicationException // next hop or EP, or whatever responder timed out
    {

    }
    class NextHopRejectedException : ApplicationException
    {
        public NextHopRejectedException(NextHopResponseCode responseCode)
            : base($"Next hop rejected request with status = {responseCode}")
        {

        }
    }
    class Pow1RejectedException : ApplicationException
    {
        public Pow1RejectedException(RegisterPow1ResponseStatusCode responseCode)
            : base($"EP rejected PoW1 request with status = {responseCode}")
        {

        }
    }


    /// <summary>
    /// gets copied from request/response packets  to "nextHopACK" packet
    /// </summary>
    public class NextHopAckSequenceNumber16
    {
        public ushort Seq16;
        public void Encode(BinaryWriter writer)
        {
            writer.Write(Seq16);
        }
        public static NextHopAckSequenceNumber16 Decode(BinaryReader reader)
        {
            var r = new NextHopAckSequenceNumber16();
            r.Seq16 = reader.ReadUInt16();
            return r;
        }
        public override bool Equals(object obj)
        {
            return ((NextHopAckSequenceNumber16)obj).Seq16 == this.Seq16;
        }
        public override string ToString() => $"hhaSeq{Seq16}";
    }

}
