using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class BGMControl : MonoBehaviour
{
    public AudioSource audioSource;
    public Text btnText;
    public void PlayBGM() {
        if (audioSource.isPlaying) {
            audioSource.Stop();
            btnText.text = "Play BGM";
        } else {
            audioSource.Play(0);
            btnText.text = "Stop BGM";
        }
    }
}
