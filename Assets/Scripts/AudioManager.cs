using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour {
    public static AudioManager Instance;

    [Header("音频源")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("音量设置 (0-1)")]
    [Range(0, 1)] public float masterVolume = 1.0f;
    [Range(0, 1)] public float bgmVolume = 0.8f;
    [Range(0, 1)] public float sfxVolume = 1.0f;

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保证切场景音乐不断
        } else {
            Destroy(gameObject);
        }
    }

    // --- 背景音乐控制 ---
    public void PlayBGM(AudioClip clip, bool loop = true) {
        if (bgmSource.clip == clip) return;
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = bgmVolume * masterVolume;
        bgmSource.Play();
    }

    public void StopBGM() {
        bgmSource.Stop();
    }

    // --- 音效控制 ---
    // 播放一次性音效
    public void PlaySFX(AudioClip clip) {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume * masterVolume);
    }

    // 用于需要循环播放并手动停止的音效（例如骰子转动）
    public void PlayLoopingSFX(AudioClip clip) {
        if (clip == null) return;
        sfxSource.clip = clip;
        sfxSource.loop = true;
        sfxSource.volume = sfxVolume * masterVolume;
        sfxSource.Play();
    }

    public void StopSFX() {
        sfxSource.Stop();
    }

    // --- 后续设置功能接口 ---
    public void UpdateVolumes(float master, float bgm, float sfx) {
        masterVolume = master;
        bgmVolume = bgm;
        sfxVolume = sfx;
        bgmSource.volume = bgmVolume * masterVolume;
        // sfxSource 的 volume 会在下一次播放时生效
    }
}