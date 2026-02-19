using System.Text;
using Network;
using TMPro;
using UnityEngine;

public class FinishedPlayersUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _finishedPlayersText;
    [SerializeField] private string _header = "🏁 Escaped:";

    [Header("Settings")]
    [SerializeField] private float _updateInterval = 0.5f;

    private float _timer;

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _updateInterval) return;
        _timer = 0f;
        RefreshList();
    }

    private void RefreshList()
    {
        if (_finishedPlayersText == null) return;

        var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);

        var sb = new StringBuilder();
        int rank = 1;

        foreach (var player in allPlayers)
        {
            if (!player.HasFinished) continue;
            sb.AppendLine($"{rank}. {player.PlayerName}");
            rank++;
        }

        if (rank > 1)
        {
            _finishedPlayersText.text = $"{_header}\n{sb}";
        }
        else
        {
            _finishedPlayersText.text = string.Empty;
        }
    }
}
