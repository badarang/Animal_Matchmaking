namespace Animal_Matchmaking.Models
{
    public class PlayerState
    {
        public float PositionX { get; set; }   // X 좌표
        public float PositionY { get; set; }   // Y 좌표
        public float VelocityX { get; set; }   // X축 속도
        public float VelocityY { get; set; }   // Y축 속도
        public int Score { get; set; }         // 점수
        public float Energy { get; set; }      // 에너지
    }
} 