﻿using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// 音频管理器，负责游戏中的音乐和音效播放。
/// 挂载在 Player 预制体上，每个玩家各有一份。
/// 创建后自动检测本地玩家并播放倒计时音乐。
/// </summary>
public class AudioManager : MonoBehaviour
{ 
    [Header("音频源")]
    public AudioSource countdownSource; // 倒计时音乐源
    public AudioSource gameStartSource; // 游戏开始音乐源
    
    [Header("音乐剪辑")]
    public AudioClip countdownMusic; // 倒计时时播放的音乐
    public AudioClip gameStartMusic; // 游戏开始时播放的音乐
    
    [Header("状态")]
    private bool isCountdownMusicPlayed = false; // 倒计时音乐是否已播放
    private bool isGameStartMusicPlayed = false; // 游戏开始音乐是否已播放
   
    void Awake()
    {
        isCountdownMusicPlayed = false;
        isGameStartMusicPlayed = false;
        
        AudioSource[] existingSources = GetComponents<AudioSource>();
        foreach (AudioSource source in existingSources)
        {
            Destroy(source);
        }
        
        countdownSource = gameObject.AddComponent<AudioSource>();
        countdownSource.loop = false;
        countdownSource.volume = 0.7f;
        countdownSource.playOnAwake = false;
        
        gameStartSource = gameObject.AddComponent<AudioSource>();
        gameStartSource.loop = true;
        gameStartSource.volume = 0.5f;
        gameStartSource.playOnAwake = false;
    }

    void Start()
    {
        StartCoroutine(TryPlayCountdownWhenReady());
    }

    /// <summary>
    /// 创建后等待本地玩家就绪，检测到倒计时时自动播放第一首歌
    /// </summary>
    IEnumerator TryPlayCountdownWhenReady()
    {
        yield return null;

        var ni = GetComponent<NetworkIdentity>();
        if (ni == null || !ni.isLocalPlayer)
            yield break;

        // 每帧检测，直到倒计时开始或超时（约 3 秒）
        for (int i = 0; i < 180; i++)
        {
            if (NetworkGameState.Instance != null && NetworkGameState.Instance.gameCountdown >= 0f)
            {
                PlayCountdownMusic();
                yield break;
            }
            yield return null;
        }
        PlayCountdownMusic();
    }
    
    /// <summary>
    /// 进入倒计时时播放音乐
    /// </summary>
    public void PlayCountdownMusic()
    {
        if (isCountdownMusicPlayed) return;
        if (countdownMusic == null)
        {
            Debug.LogWarning("AudioManager: countdownMusic 未赋值，请在 Player 预制体的 AudioManager 中拖入 AudioClip");
            return;
        }
        if (countdownSource == null) countdownSource = gameObject.AddComponent<AudioSource>();
        countdownSource.clip = countdownMusic;
        countdownSource.loop = false;
        countdownSource.volume = 0.7f;
        countdownSource.Play();
        isCountdownMusicPlayed = true;
        Debug.Log("播放倒计时音乐");
    }
    
    /// <summary>
    /// 游戏开始时播放音乐
    /// </summary>
    public void PlayGameStartMusic()
    {
        if (isGameStartMusicPlayed) return;
        if (gameStartMusic == null)
        {
            Debug.LogWarning("AudioManager: gameStartMusic 未赋值，请在 Player 预制体的 AudioManager 中拖入 AudioClip");
            return;
        }
        if (gameStartSource == null) gameStartSource = gameObject.AddComponent<AudioSource>();
        gameStartSource.clip = gameStartMusic;
        gameStartSource.loop = true;
        gameStartSource.volume = 0.5f;
        gameStartSource.Play();
        isGameStartMusicPlayed = true;
        Debug.Log("播放游戏开始音乐");
    }
    
    /// <summary>
    /// 停止所有音乐
    /// </summary>
    public void StopAllMusic()
    {
        if (countdownSource != null) countdownSource.Stop();
        if (gameStartSource != null) gameStartSource.Stop();
        
        isCountdownMusicPlayed = false;
        isGameStartMusicPlayed = false;
    }
    
    /// <summary>
    /// 重置音乐状态，允许重新播放
    /// </summary>
    public void ResetMusicState()
    {
        isCountdownMusicPlayed = false;
        isGameStartMusicPlayed = false;
    }
    
    /// <summary>
    /// 设置音乐音量
    /// </summary>
    /// <param name="volume">音量值，范围0-1</param>
    public void SetMusicVolume(float volume)
    {
        if (countdownSource != null) countdownSource.volume = volume;
        if (gameStartSource != null) gameStartSource.volume = volume;
    }
    
    /// <summary>
    /// 检查是否有音乐正在播放
    /// </summary>
    /// <returns>是否有音乐正在播放</returns>
    public bool IsMusicPlaying()
    {
        return (countdownSource != null && countdownSource.isPlaying) || (gameStartSource != null && gameStartSource.isPlaying);
    }
}