﻿using System;
using System.Collections.Generic;
using Dissonance.Datastructures;
using Dissonance.Networking;
using ExitGames.Client.Photon;
using JetBrains.Annotations;
using UnityEngine;

namespace Dissonance.Integrations.PhotonUnityNetworking
{
    [HelpURL("https://placeholder-software.co.uk/dissonance/docs/Basics/Quick-Start-Photon/")]
    public class PhotonCommsNetwork
        : BaseCommsNetwork<PhotonServer, PhotonClient, int, Unit, Unit>
    {
        #region fields and properties
        public byte EventCodeToServer = 173;
        public byte EventCodeToClient = 174;
        public byte SerializationCode = 231;

        private readonly ConcurrentPool<PunPacketWrapperType> _packetWrappers = new ConcurrentPool<PunPacketWrapperType>(3, () => new PunPacketWrapperType());
        private readonly ConcurrentPool<byte[]> _byteBuffers = new ConcurrentPool<byte[]>(3, () => new byte[1024]);

        private readonly List<KeyValuePair<byte, IPhotonPacketCallback>> _packetListeners = new List<KeyValuePair<byte, IPhotonPacketCallback>>();
        private readonly HashSet<byte> _eventCodes = new HashSet<byte>();

        private readonly List<IPhotonDisconnectListener> _disconnectListeners = new List<IPhotonDisconnectListener>();
        #endregion

        protected override PhotonServer CreateServer(Unit details)
        {
            return new PhotonServer(this);
        }

        protected override PhotonClient CreateClient(Unit details)
        {
            return new PhotonClient(this);
        }

        protected override void Initialize()
        {
            PhotonPeer.RegisterType(typeof(PunPacketWrapperType), SerializationCode, Serialize, Deserialize);

            base.Initialize();
        }

        private void OnEnable()
        {
            PhotonNetwork.OnEventCall += OnEvent;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            PhotonNetwork.OnEventCall -= OnEvent;
        }

        internal void RegisterDisconnectListener(IPhotonDisconnectListener listener)
        {
            //Ensure we only add a given listener once
            if (!_disconnectListeners.Contains(listener))
                _disconnectListeners.Add(listener);
        }

        internal void UnregisterDisconnectListener(IPhotonDisconnectListener listener)
        {
            //Remove all instances of the listener
            for (var i = _disconnectListeners.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_disconnectListeners[i], listener))
                    _disconnectListeners.RemoveAt(i);
        }

        internal interface IPhotonDisconnectListener
        {
            void PeerDisconnected(global::PhotonPlayer photonPlayer);
        }

        // ReSharper disable once UnusedMember.Local (Justification: invoked indirectly by photon)
        private void OnMasterClientSwitched([NotNull] global::PhotonPlayer newMasterClient)
        {
            // The photon master client has switched. Kill the current local Dissonance network system, this will force it to
            // re-initialize (either as the host, if that responsibility is now ours, or as the client but properly connected to the new master).
            Stop();

            Log.Info("PUN Master Client switched to '{0}'. Resetting Dissonance network session", newMasterClient.ID);
        }

        // ReSharper disable once UnusedMember.Local (Justification: invoked indirectly by photon)
        private void OnPhotonPlayerDisconnected([NotNull] global::PhotonPlayer player)
        {
            for (var i = 0; i < _disconnectListeners.Count; i++)
                _disconnectListeners[i].PeerDisconnected(player);
        }

        protected override void Update()
        {
            base.Update();

            if (IsInitialized)
            {
                if (PhotonNetwork.connectionStateDetailed == ClientState.Joined)
                {
                    if (PhotonNetwork.isMasterClient)
                    {
                        if (Mode != NetworkMode.Host)
                            RunAsHost(Unit.None, Unit.None);
                    }
                    else
                    {
                        if (Mode != NetworkMode.Client)
                            RunAsClient(Unit.None);
                    }
                }
                else
                {
                    if (Mode != NetworkMode.None)
                        Stop();
                }
            }
        }

        #region photon packet sending and receiving
        internal void OnEvent(byte eventcode, object content, int senderid)
        {
            if (!_eventCodes.Contains(eventcode))
                return;

            var wrapper = content as PunPacketWrapperType;
            if (wrapper == null)
                return;

            //Extract the data and recycle the wrapper
            var data = wrapper.Data;
            wrapper.Data = default(ArraySegment<byte>);
            _packetWrappers.Put(wrapper);

            //Send the data to all the interested listeners
            for (var i = 0; i < _packetListeners.Count; i++)
            {
                var l = _packetListeners[i];
                if (l.Key == eventcode)
                    l.Value.PacketDelivered(eventcode, data, senderid);
            }

            //Recycle the byte buffer
            // ReSharper disable once AssignNullToNotNullAttribute (Justification ArraySegment.Array is not null)
            _byteBuffers.Put(data.Array);
        }

        internal void RegisterPacketListener(byte eventCode, IPhotonPacketCallback listener)
        {
            //Add this event code to the filter
            _eventCodes.Add(eventCode);

            //Try to find this listener to ensure we don't add it twice with the same event code
            for (var i = 0; i < _packetListeners.Count; i++)
                if (ReferenceEquals(_packetListeners[i].Value, listener) && _packetListeners[i].Key == eventCode)
                    return;

            //Since we're here we didn't find this listener
            _packetListeners.Add(new KeyValuePair<byte, IPhotonPacketCallback>(eventCode, listener));
        }

        internal void UnregisterPacketListener(IPhotonPacketCallback listener)
        {
            for (var i = _packetListeners.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_packetListeners[i].Value, listener))
                    _packetListeners.RemoveAt(i);
        }

        internal void Send(ArraySegment<byte> data, byte eventCode, RaiseEventOptions options, bool reliable)
        {
            //Get a wrapper for this data, the wrapper will be recycled inside the serialize method.
            //Yes, I know that a stateful serialization method is evil incarnate.
            var wrapper = _packetWrappers.Get();
            wrapper.Data = data;

            PhotonNetwork.RaiseEvent(eventCode, wrapper, reliable, options);
        }

        internal interface IPhotonPacketCallback
        {
            void PacketDelivered(byte eventcode, ArraySegment<byte> data, int senderid);
        }
        #endregion

        #region Photon packet serialization
        [NotNull] private object Deserialize([NotNull] StreamBuffer instream, short length)
        {
            if (instream == null)
                throw new ArgumentNullException("instream");

            //Get a buffer to copy into and a wrapper type to contain it
            var buffer = _byteBuffers.Get();
            var wrapper = _packetWrappers.Get();

            //Read the raw data
            instream.Read(buffer, 0, length);

            //Hold it in the wrapper (prevent boxing by returning the segment directly)
            wrapper.Data = new ArraySegment<byte>(buffer, 0, length);
            return wrapper;
        }

        private short Serialize([NotNull] StreamBuffer outstream, [NotNull] object customobject)
        {
            if (outstream == null) throw new ArgumentNullException("outstream");
            if (customobject == null) throw new ArgumentNullException("customobject");

            //Copy the packet into a Photon stream
            var packet = (PunPacketWrapperType)customobject;
            // ReSharper disable once AssignNullToNotNullAttribute
            outstream.Write(packet.Data.Array, packet.Data.Offset, packet.Data.Count);
            var count = checked((short)packet.Data.Count);

            //Recycle the wrapper type
            packet.Data = default(ArraySegment<byte>);
            _packetWrappers.Put(packet);

            return count;
        }

        private class PunPacketWrapperType
        {
            public ArraySegment<byte> Data;
        }
        #endregion
    }
}
