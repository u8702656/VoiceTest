using Photon;
using UnityEngine;

namespace Dissonance.Integrations.PhotonUnityNetworking
{
    public class PhotonPlayer
        : PunBehaviour, IDissonancePlayer
    {
        private static readonly Log Log = Logs.Create(LogCategory.Network, "Photon Player Component");

        private DissonanceComms _comms;

        public bool IsTracking { get; private set; }

        public string PlayerId { get; private set; }

        public Vector3 Position
        {
            get { return transform.position; }
        }

        public Quaternion Rotation
        {
            get { return transform.rotation; }
        }

        public NetworkPlayerType Type
        {
            get { return photonView.isMine ? NetworkPlayerType.Local : NetworkPlayerType.Remote; }
        }

        public override void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            _comms = FindObjectOfType<DissonanceComms>();
            if (_comms == null)
            {
                throw Log.CreateUserErrorException(
                    "cannot find DissonanceComms component in scene",
                    "not placing a DissonanceComms component on a game object in the scene",
                    "https://dissonance.readthedocs.io/en/latest/Basics/Quick-Start-Photon/",
                    "00077AC8-3CBF-4DD8-A1C7-3ED3E8F64914");
            }

            if (info.sender.IsLocal)
            {
                // This method is called on the client which has control authority over this object. This will be the local client of whichever player we are tracking.
                if (_comms.LocalPlayerName != null)
                    SetPlayerName(_comms.LocalPlayerName);

                //Subscribe to future name changes (this is critical because we may not have run the initial set name yet and this will trigger that initial call)
                _comms.LocalPlayerNameChanged += SetPlayerName;
            }
        }

        public void OnDestroy()
        {
            if (_comms != null)
                _comms.LocalPlayerNameChanged -= SetPlayerName;
        }

        public void OnEnable()
        {
            if (!IsTracking)
                StartTracking();
        }

        public void OnDisable()
        {
            if (IsTracking)
                StopTracking();
        }

        [PunRPC]
        private void SetPlayerName(string playerName)
        {
            //We need to stop and restart tracking to handle the name change
            if (IsTracking)
                StopTracking();

            //Perform the actual work
            PlayerId = playerName;
            StartTracking();

            //Inform the other clients the name has changed, if we are the owner
            if (photonView.isMine)
                photonView.RPC("SetPlayerName", PhotonTargets.OthersBuffered, PlayerId);
        }

        private void StartTracking()
        {
            if (IsTracking)
                throw Log.CreatePossibleBugException("Attempting to start player tracking, but tracking is already started", "0663D808-ACCC-4D13-8913-03F9BA0C8578");

            if (_comms != null)
            {
                _comms.TrackPlayerPosition(this);
                IsTracking = true;
            }
        }

        private void StopTracking()
        {
            if (!IsTracking)
                throw Log.CreatePossibleBugException("Attempting to stop player tracking, but tracking is not started", "48802E32-C840-4C4B-BC58-4DC741464B9A");

            if (_comms != null)
            {
                _comms.StopTracking(this);
                IsTracking = false;
            }
        }
    }
}
