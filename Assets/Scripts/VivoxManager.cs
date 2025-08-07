using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Vivox;

public class VivoxManager : MonoBehaviour
{
    async void Start()
    {
        // ������������� Unity Services
        await UnityServices.InitializeAsync();

        // ���� � Vivox
        var playerName = "Player" + Random.Range(1000, 9999);
        var loginOptions = new LoginOptions
        {
            DisplayName = playerName
        };
        await VivoxService.Instance.LoginAsync(loginOptions);

        // ����������� � ���������� ������
        var channelName = "Global";
        await VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.AudioOnly);

        Debug.Log("���������� � Vivox ��� " + playerName);
    }
}
