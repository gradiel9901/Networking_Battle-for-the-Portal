using System.Text;
using Network;
using TMPro;
using UnityEngine;

// shows a list of players who've already finished, updates every half second
public class FinishedPlayersUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _finishedPlayersText;
    [SerializeField] private string _header = "🏁 Escaped:";

    [Header("Settings")]
    [SerializeField] private float _updateInterval = 0.5f; // dont update every frame, that'd be wasteful

    private float _timer;

    private void Update()
    {
        // only refresh the list every X seconds instead of every frame
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
            // skip players who havent finished yet
            if (!player.HasFinished) continue;
            sb.AppendLine($"{rank}. {player.PlayerName}");
            rank++;
        }

        // only show the header text if at least one person has finished
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
