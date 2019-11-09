﻿using Dcomms.DRP.Packets;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Dcomms.DRP
{
    /// <summary>
    /// sends and retransmits requestUdpData  until 
    /// 1) NPACK-accepted, ACK1 
    /// OR
    /// 2) NPACK-accepted, FAILURE, (sent) NPACK 
    /// OR
    /// 3) NPACK-failure
    /// 
    /// sends NPACK to FAILURE
    /// 
    /// throws RequestRejectedException/RequestRejectedExceptionRouteIsUnavailable/DrpTimeoutException exception
    /// 
    /// stores result: ACK1 UDP data
    /// </summary>
    class SentRequest
    {
        readonly byte[] _requestUdpData;
        readonly RequestP2pSequenceNumber16 _sentReqP2pSeq16;
        readonly LowLevelUdpResponseScanner _ack1Scanner;
        readonly ConnectionToNeighbor _destinationNeighborNullable; // is null in A-EP mode at A
        IPEndPoint _destinationEndpoint;
        readonly DrpPeerEngine _engine;
        readonly Logger _logger;

        public byte[] Ack1UdpData; // result of the request
        byte[] _failureUdpData;

        public SentRequest(DrpPeerEngine engine, Logger logger, IPEndPoint destinationEndpoint, ConnectionToNeighbor destinationNeighborNullable, byte[] requestUdpData, 
            RequestP2pSequenceNumber16 sentReqP2pSeq16, LowLevelUdpResponseScanner ack1Scanner)
        {
            _destinationEndpoint = destinationEndpoint;
            _logger = logger;
            _requestUdpData = requestUdpData;
            _sentReqP2pSeq16 = sentReqP2pSeq16;
            _ack1Scanner = ack1Scanner;
            _destinationNeighborNullable = destinationNeighborNullable;
            _engine = engine;
        }

        public async Task<byte[]> SendRequestAsync(string completionActionVisibleId)
        {
            // wait for NPACK (-accepted or -failure)
            _logger.WriteToLog_detail($">> SendRequestAsync() _requestUdpData= {MiscProcedures.GetArrayHashCodeString(_requestUdpData)}");
            await _engine.OptionallySendUdpRequestAsync_Retransmit_WaitForNeighborPeerAck(completionActionVisibleId+"_after_npack", _requestUdpData, _destinationEndpoint, _sentReqP2pSeq16);
            var tr2 = _engine.CreateTracker(completionActionVisibleId + "_after_npack2");
            _logger.WriteToLog_detail($"@SendRequestAsync() received NPACK");

            var ack1Task = WaitForAck1Async(completionActionVisibleId);
            var failureTask = WaitForFailureAsync(completionActionVisibleId);
            tr2.Dispose();

            // wait for ACK1 OR FAILURE
            await Task.WhenAny(
                ack1Task,
                failureTask
                );
            using var tr3 = _engine.CreateTracker(completionActionVisibleId + "_after_npack3");
            if (_pendingAck1Request != null)
            {
                _engine.CancelPendingRequest(_pendingAck1Request);
                _pendingAck1Request = null;
            }
            if (_pendingFailureRequest != null)
            {
                _engine.CancelPendingRequest(_pendingFailureRequest);
                _pendingFailureRequest = null;
            }


            if (_waitForAck1Completed)
            {
                _logger.WriteToLog_detail($"received ACK1");
                if (Ack1UdpData == null) throw new DrpTimeoutException();
                return Ack1UdpData;
            }
            else if (_waitForFailureCompleted)
            {
                if (_failureUdpData == null) throw new DrpTimeoutException();
                _logger.WriteToLog_detail($"received FAILURE");
                var failure = FailurePacket.DecodeAndOptionallyVerify(_failureUdpData, _sentReqP2pSeq16);
                
                if (_failureUdpData != null)
                {
                    // send NPACK to FAILURE
                    var npAckToFailure = new NeighborPeerAckPacket
                    {
                        ReqP2pSeq16 = _sentReqP2pSeq16,
                        ResponseCode = ResponseOrFailureCode.accepted
                    };
                    if (_destinationNeighborNullable != null)
                    {
                        npAckToFailure.NeighborToken32 = _destinationNeighborNullable.RemoteNeighborToken32;
                        npAckToFailure.NeighborHMAC = _destinationNeighborNullable.GetNeighborHMAC(w => npAckToFailure.GetSignedFieldsForNeighborHMAC(w, failure.GetSignedFieldsForNeighborHMAC));
                    }

                    var npAckToFailureUdpData = npAckToFailure.Encode(_destinationNeighborNullable == null);

                    _engine.RespondToRequestAndRetransmissions(_failureUdpData, npAckToFailureUdpData, _destinationEndpoint);
                }

                throw new RequestRejectedException(failure.ResponseCode);
            }
            else throw new InvalidOperationException();         
        }
        bool _waitForAck1Completed;
        PendingLowLevelUdpRequest _pendingAck1Request;
        async Task WaitForAck1Async(string completionActionVisibleId)
        {
            _logger.WriteToLog_detail($"waiting for ACK1");
            _pendingAck1Request = new PendingLowLevelUdpRequest(completionActionVisibleId, _destinationEndpoint,
                            _ack1Scanner, _engine.DateTimeNowUtc, _engine.Configuration.Ack1TimoutS
                            );
            Ack1UdpData = await _engine.WaitForUdpResponseAsync(_pendingAck1Request);
            _pendingAck1Request = null;
            _waitForAck1Completed = true;

        }

        PendingLowLevelUdpRequest _pendingFailureRequest;
        bool _waitForFailureCompleted;
        async Task WaitForFailureAsync(string completionActionVisibleId)
        {
            var failureScanner = FailurePacket.GetScanner(_logger, _sentReqP2pSeq16, _destinationNeighborNullable); // the scanner verifies neighborHMAC
            _logger.WriteToLog_detail($"waiting for FAILURE");
            _pendingFailureRequest = new PendingLowLevelUdpRequest(completionActionVisibleId, _destinationEndpoint,
                            failureScanner, _engine.DateTimeNowUtc, _engine.Configuration.Ack1TimoutS
                            );
            _failureUdpData = await _engine.WaitForUdpResponseAsync(_pendingFailureRequest);
            _pendingFailureRequest = null;
            _waitForFailureCompleted = true;
        }

    }
}
