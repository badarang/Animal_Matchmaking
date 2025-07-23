using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Animal_Matchmaking.Services;
using Animal_Matchmaking.Models;
using Animal_Matchmaking.Enums;

namespace Animal_Matchmaking.Hubs
{
    public class GameHub : Hub
    {
        private readonly ILogger<GameHub> _logger;
        private readonly MatchingService _matchingService;
        private readonly IHubContext<GameHub> _hubContext;

        public GameHub(ILogger<GameHub> logger, MatchingService matchingService, IHubContext<GameHub> hubContext)
        {
            _logger = logger;
            _matchingService = matchingService;
            _hubContext = hubContext;
        }

        // 클라이언트 연결 시 호출
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"플레이어 연결됨: {Context.ConnectionId}");
            _logger.LogInformation($"연결 시간: {DateTime.Now}");
            await base.OnConnectedAsync();
        }

        // 클라이언트 연결 해제 시 호출
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"플레이어 연결 해제: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        // Unity에서 호출할 수 있는 테스트 메서드
        public async Task SendMessage(string message)
        {
            _logger.LogInformation($"클라이언트 {Context.ConnectionId}에서 메시지 수신: {message}");
            
            // 모든 클라이언트에게 메시지 전송
            await Clients.All.SendAsync("ReceiveMessage", Context.ConnectionId, message);
        }

        // Unity에서 호출할 수 있는 개인 메시지 메서드
        public async Task SendToCaller(string message)
        {
            _logger.LogInformation($"클라이언트 {Context.ConnectionId}에서 개인 메시지: {message}");
            
            // 호출한 클라이언트에게만 메시지 전송
            await Clients.Caller.SendAsync("ReceiveMessage", "Server", message);
        }

        // 매칭 참가
        public async Task JoinMatchmaking(string nickname, Animal selectedAnimal)
        {
            try
            {
                var player = new Player
                {
                    Id = Context.ConnectionId,
                    Nickname = nickname,
                    SelectedAnimal = selectedAnimal,
                    Level = 1,
                    Experience = 0
                };

                var (success, waitingCount) = _matchingService.JoinMatchmaking(player, Context.ConnectionId);
                
                if (success)
                {
                    var message = waitingCount == 1 ? "매칭에 참가했습니다. 상대방을 기다리는 중..." : "매칭에 참가했습니다.";
                    await Clients.Caller.SendAsync("MatchmakingJoined", message);
                    _logger.LogInformation($"플레이어 {nickname}이 매칭에 참가했습니다. (대기 중: {waitingCount}명)");
                    
                    // 매칭 시도
                    var (matched, room, matchedPlayers) = _matchingService.TryMatchPlayers();
                    
                    if (matched && room != null && matchedPlayers != null)
                    {
                        await NotifyGameStart(room, matchedPlayers);
                    }
                }
                else
                {
                    await Clients.Caller.SendAsync("MatchmakingError", "매칭 참가에 실패했습니다.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"매칭 참가 중 오류: {ex.Message}");
                await Clients.Caller.SendAsync("MatchmakingError", "매칭 참가 중 오류가 발생했습니다.");
            }
        }

        // 매칭 취소
        public async Task LeaveMatchmaking()
        {
            try
            {
                var success = _matchingService.LeaveMatchmaking(Context.ConnectionId);
                
                if (success)
                {
                    await Clients.Caller.SendAsync("MatchmakingLeft", "매칭을 취소했습니다.");
                    _logger.LogInformation($"플레이어가 매칭을 취소했습니다: {Context.ConnectionId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"매칭 취소 중 오류: {ex.Message}");
            }
        }

        // 대기 중인 플레이어 수 조회
        public async Task GetWaitingCount()
        {
            var count = _matchingService.GetWaitingCount();
            await Clients.Caller.SendAsync("WaitingCount", count);
        }

        private async Task NotifyGameStart(GameRoom room, List<KeyValuePair<string, Player>> players)
        {
            var gameStartData = new
            {
                roomId = room.RoomId,
                players = room.Players.Select(p => new { p.Id, p.Nickname, p.SelectedAnimal }),
                randomSeed = room.RandomSeed,
                gameMode = room.Mode.ToString()
            };

            // 각 플레이어에게 게임 시작 알림
            foreach (var player in players)
            {
                await _hubContext.Clients.Client(player.Key).SendAsync("GameMatched", gameStartData);
            }
        }
    }
} 