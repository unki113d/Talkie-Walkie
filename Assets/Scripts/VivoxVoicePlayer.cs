using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Vivox;
using Unity.Services.Vivox.AudioTaps;
using System.Threading.Tasks;

[RequireComponent(typeof(VivoxParticipantTap))]
public class VivoxVoicePlayer : MonoBehaviour
{
    public string channelName = "Global";
    private string playerName;

    private VivoxParticipantTap _tap;

    async void Start()
    {
        _tap = GetComponent<VivoxParticipantTap>();

        playerName = "Player" + Random.Range(1000, 9999);

        // 1. Init Services (если ты используешь Test Mode — не нужно передавать токены)
        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            await UnityServices.InitializeAsync();
        }

        // 2. Авторизация
        await VivoxService.Instance.LoginAsync(new LoginOptions
        {
            DisplayName = playerName
        });


        await VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.AudioOnly);
        // 4. Подключение VivoxParticipantTap
        _tap.ChannelName = channelName;
        _tap.ParticipantName = playerName;
    }

    void Update()
    {
        if (VivoxService.Instance.IsLoggedIn && VivoxService.Instance.ActiveChannels.TryGetValue(channelName, out var channel))
        {
            VivoxService.Instance.Set3DPosition(
                transform.position,           // speaker position
                transform.position,           // listener position
                transform.forward,            // listener facing direction
                transform.up,                 // listener up
                channelName,
                true                          // allow panning
            );
        }
    }
}
