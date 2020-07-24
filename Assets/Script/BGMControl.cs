using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class BGMControl : MonoBehaviour
{
    public AudioSource audioSource;
    public Text textBGM;
    public Slider sliderVolume;
    public void PlayBGM() {
        audioSource.enabled = !audioSource.enabled;
        textBGM.text = audioSource.enabled ? "Stop BGM" : "Play BGM";
        audioSource.volume = sliderVolume.value;
    }
    public void SetVoume(float value) {
        audioSource.volume = sliderVolume.value;
    }
}
