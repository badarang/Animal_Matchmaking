using Animal_Matchmaking.Enums;
using System.Collections.Generic;

namespace Animal_Matchmaking.Models
{
    public class GameRoom
    {
        public string RoomId { get; set; } = string.Empty;                    // 룸 ID
        public List<Player> Players { get; set; } = new();     // 참가자 목록
        public GameMode Mode { get; set; } = GameMode.Infinite; // 게임 모드
        public GameState State { get; set; } = GameState.Waiting; // 게임 상태
        public int RandomSeed { get; set; } = 0;               // 랜덤 시드
    }
} 