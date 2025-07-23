using Animal_Matchmaking.Models;
using Animal_Matchmaking.Enums;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Animal_Matchmaking.Services
{
    public class MatchingService
    {
        private readonly ILogger<MatchingService> _logger;
        private readonly ConcurrentDictionary<string, Player> _waitingPlayers = new();
        private readonly ConcurrentDictionary<string, GameRoom> _gameRooms = new();

        public MatchingService(ILogger<MatchingService> logger)
        {
            _logger = logger;
        }

        public (bool success, int waitingCount) JoinMatchmaking(Player player, string connectionId)
        {
            try
            {
                // 이미 대기 중인지 확인
                if (_waitingPlayers.ContainsKey(connectionId))
                {
                    _logger.LogWarning($"플레이어 {player.Nickname}이 이미 대기 중입니다.");
                    return (false, _waitingPlayers.Count);
                }
                
                _logger.LogInformation($"플레이어 {player.Nickname}이 매칭에 참가했습니다.");
                
                // 대기열에 추가
                _waitingPlayers.TryAdd(connectionId, player);
                
                return (true, _waitingPlayers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError($"매칭 참가 실패: {ex.Message}");
                return (false, _waitingPlayers.Count);
            }
        }

        public bool LeaveMatchmaking(string connectionId)
        {
            return _waitingPlayers.TryRemove(connectionId, out _);
        }

        public (bool matched, GameRoom? room, List<KeyValuePair<string, Player>>? matchedPlayers) TryMatchPlayers()
        {
            if (_waitingPlayers.Count < 2) 
                return (false, null, null);

            var players = _waitingPlayers.Take(2).ToList();
            
            if (players.Count == 2)
            {
                var room = CreateGameRoom(players);
                
                // 플레이어들을 대기열에서 제거
                foreach (var player in players)
                {
                    _waitingPlayers.TryRemove(player.Key, out _);
                }

                _logger.LogInformation($"게임 룸 {room.RoomId}가 생성되었습니다. 플레이어: {string.Join(", ", room.Players.Select(p => p.Nickname))}");
                
                return (true, room, players);
            }
            
            return (false, null, null);
        }

        private GameRoom CreateGameRoom(List<KeyValuePair<string, Player>> players)
        {
            var room = new GameRoom
            {
                RoomId = Guid.NewGuid().ToString(),
                Players = players.Select(p => p.Value).ToList(),
                Mode = GameMode.Infinite,
                State = GameState.Matching,
                RandomSeed = new Random().Next()
            };

            _gameRooms.TryAdd(room.RoomId, room);
            return room;
        }

        public int GetWaitingCount()
        {
            return _waitingPlayers.Count;
        }

        public List<Player> GetWaitingPlayers()
        {
            return _waitingPlayers.Values.ToList();
        }
    }
} 