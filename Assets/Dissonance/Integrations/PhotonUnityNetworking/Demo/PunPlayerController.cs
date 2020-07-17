using UnityEngine;

namespace Dissonance.Integrations.PhotonUnityNetworking.Demo
{
    public class PunPlayerController
        : Photon.PunBehaviour
    {
        private Vector3 _correctPlayerPos;
        private Quaternion _correctPlayerRot;

        private void Update()
        {
            UpdateControl();
            UpdatePhoton();
        }

        private void UpdateControl()
        {
            if (!photonView.isMine)
                return;

            var controller = GetComponent<CharacterController>();

            var rotation = Input.GetAxis("Horizontal") * Time.deltaTime * 150.0f;
            var speed = Input.GetAxis("Vertical") * 3.0f;

            transform.Rotate(0, rotation, 0);
            var forward = transform.TransformDirection(Vector3.forward);
            controller.SimpleMove(forward * speed);

            if (transform.position.y < -3)
            {
                transform.position = Vector3.zero;
                transform.rotation = Quaternion.identity;
            }
        }

        private void UpdatePhoton()
        {
            if (!photonView.isMine)
            {
                transform.position = Vector3.Lerp(transform.position, _correctPlayerPos, Time.deltaTime * 5);
                transform.rotation = Quaternion.Lerp(transform.rotation, _correctPlayerRot, Time.deltaTime * 5);
            }
        }

        // ReSharper disable once UnusedMember.Local (Justification: Used implicitly by Photon)
        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.isWriting)
            {
                // We own this player: send the others our data
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);

            }
            else
            {
                // Network player, receive data
                _correctPlayerPos = (Vector3)stream.ReceiveNext();
                _correctPlayerRot = (Quaternion)stream.ReceiveNext();
            }
        }
    }
}