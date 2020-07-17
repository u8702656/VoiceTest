using System;
using Photon;
using UnityEngine;

namespace Dissonance.Integrations.PhotonUnityNetworking.Demo
{
    public class PunMenuController
        : PunBehaviour
    {
        private string _guiCreateNamedRoomName = Guid.NewGuid().ToString().Substring(0, 10);

        private State _state;
        private enum State
        {
            JoiningLobby,
            InLobby,
            JoiningRoom,
            CreatingRoom,
            InRoom
        }

        [SerializeField] public string SceneName = "PUN Game World";

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            var objs = FindObjectsOfType<PunMenuController>();
            if (objs.Length > 1)
                Destroy(gameObject);
        }

        private void Start ()
        {
            PhotonNetwork.NetworkStatisticsEnabled = true;
            PhotonNetwork.autoJoinLobby = true;
            PhotonNetwork.ConnectUsingSettings("0.1");

            _state = State.JoiningLobby;
        }

        public override void OnJoinedLobby()
        {
            _state = State.InLobby;
        }

        public override void OnPhotonRandomJoinFailed(object[] codeAndMsg)
        {
            _state = State.InLobby;
        }

        public override void OnPhotonJoinRoomFailed(object[] codeAndMsg)
        {
            _state = State.InLobby;
        }

        public override void OnJoinedRoom()
        {
            PhotonNetwork.LoadLevel(SceneName ?? "PUN Game World");
            _state = State.InRoom;
        }

        private void OnGUI()
        {
            using (new GUILayout.AreaScope(new Rect(10, 10, 250, Screen.height - 20)))
            {
                switch (_state)
                {
                    case State.JoiningLobby:
                        GUILayout.Label("Connecting To Photon Servers...");
                        break;

                    case State.InLobby:
                        InLobbyGUI();
                        break;

                    case State.JoiningRoom:
                        GUILayout.Label("Joining Room...");
                        break;

                    case State.CreatingRoom:
                        GUILayout.Label("Creating Room...");
                        break;

                    case State.InRoom:
                        InRoomGUI();
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void InRoomGUI()
        {
            if (GUILayout.Button(new GUIContent("Exit Room")))
            {
                PhotonNetwork.LeaveRoom();
                PhotonNetwork.LoadLevel("PUN Demo");
            }
        }

        private void InLobbyGUI()
        {
            using (new GUILayout.VerticalScope())
            {
                if (GUILayout.Button(new GUIContent("Join Random Room")))
                {
                    PhotonNetwork.JoinRandomRoom();
                    _state = State.JoiningRoom;
                }

                using (new GUILayout.HorizontalScope())
                {
                    _guiCreateNamedRoomName = GUILayout.TextField(_guiCreateNamedRoomName);

                    if (GUILayout.Button(new GUIContent("Create New Room")))
                    {
                        PhotonNetwork.CreateRoom(_guiCreateNamedRoomName);
                        _state = State.CreatingRoom;
                    }
                }

                var rooms = PhotonNetwork.GetRoomList();
                if (rooms.Length > 0)
                {
                    GUILayout.Label(string.Format("Available Rooms ({0}):", rooms.Length));
                    for (var i = 0; i < rooms.Length; i++)
                    {
                        if (!rooms[i].IsOpen)
                            continue;

                        var join = GUILayout.Button(string.Format(" - {0}", rooms[i].Name));
                        if (join)
                        {
                            PhotonNetwork.JoinRoom(rooms[i].Name);
                            _state = State.JoiningRoom;
                        }
                    }
                }
            }
        }
    }
}
