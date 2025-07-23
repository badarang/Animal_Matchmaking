using Animal_Matchmaking.Hubs;
using Animal_Matchmaking.Services;
using Serilog;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Serilog 설정
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// SignalR 서비스 추가
builder.Services.AddSignalR();

// 매칭 서비스 추가
builder.Services.AddSingleton<MatchingService>();

// CORS 정책 추가 (Unity에서 접근하기 위해)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins("http://localhost:8080", "https://localhost:8080", "http://localhost:3000")
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
    });
});

var app = builder.Build();

// CORS 미들웨어 추가
app.UseCors();

// WebSocket 미들웨어 추가
app.UseWebSockets();

// SignalR Hub 매핑
app.MapHub<GameHub>("/gamehub");

// WebSocket 엔드포인트 추가
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await HandleWebSocket(webSocket, context.RequestServices);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.MapGet("/", () => "Animal Matchmaking Server is running!");

// Unity에서 테스트할 수 있는 간단한 엔드포인트들
app.MapGet("/test", () => "Test endpoint is working!");
app.MapGet("/status", () => new { status = "running", timestamp = DateTime.Now });
app.MapPost("/echo", async (HttpContext context) => 
{
    using var reader = new StreamReader(context.Request.Body);
    var message = await reader.ReadToEndAsync();
    return $"Server received: {message}";
});

Log.Information("서버가 시작되었습니다!");

app.Run();

// WebSocket 핸들러
async Task HandleWebSocket(System.Net.WebSockets.WebSocket webSocket, IServiceProvider serviceProvider)
{
    var matchingService = serviceProvider.GetRequiredService<MatchingService>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var buffer = new byte[1024];
    
    try
    {
        while (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
            {
                var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                logger.LogInformation($"WebSocket 메시지 수신: {message}");
                
                var response = await ProcessWebSocketMessage(message, webSocket, matchingService);
                if (!string.IsNullOrEmpty(response))
                {
                    var responseBuffer = System.Text.Encoding.UTF8.GetBytes(response);
                    await webSocket.SendAsync(new ArraySegment<byte>(responseBuffer), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            else if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
            {
                break;
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError($"WebSocket 처리 오류: {ex.Message}");
    }
}

async Task<string> ProcessWebSocketMessage(string message, System.Net.WebSockets.WebSocket webSocket, MatchingService matchingService)
{
    try
    {
        if (message.StartsWith("JOIN:"))
        {
            var parts = message.Split(':');
            if (parts.Length >= 3)
            {
                var nickname = parts[1];
                var animalIndex = int.Parse(parts[2]);
                
                var player = new Animal_Matchmaking.Models.Player
                {
                    Id = webSocket.GetHashCode().ToString(),
                    Nickname = nickname,
                    SelectedAnimal = (Animal_Matchmaking.Enums.Animal)animalIndex,
                    Level = 1,
                    Experience = 0
                };

                var success = matchingService.JoinMatchmaking(player, webSocket.GetHashCode().ToString());
                
                if (success)
                {
                    var (matched, room, matchedPlayers) = matchingService.TryMatchPlayers();
                    
                    if (matched && room != null)
                    {
                        return $"MATCHED:룸ID:{room.RoomId}:시드:{room.RandomSeed}";
                    }
                    else
                    {
                        return "JOINED:매칭에 참가했습니다";
                    }
                }
                else
                {
                    return "ERROR:매칭 참가 실패";
                }
            }
        }
        else if (message == "LEAVE")
        {
            var success = matchingService.LeaveMatchmaking(webSocket.GetHashCode().ToString());
            return success ? "LEFT:매칭을 취소했습니다" : "ERROR:매칭 취소 실패";
        }
        
        return "";
    }
    catch (Exception ex)
    {
        return $"ERROR:{ex.Message}";
    }
}
