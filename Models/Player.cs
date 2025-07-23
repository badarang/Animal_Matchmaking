using Animal_Matchmaking.Enums;

namespace Animal_Matchmaking.Models
{
    public class Player
    {
        public string Id { get; set; } = string.Empty;           // 플레이어 고유 ID
        public string Nickname { get; set; } = string.Empty;      // 닉네임
        public Animal SelectedAnimal { get; set; } = Animal.Cat; // 선택한 동물
        public int Level { get; set; } = 1;                       // 레벨
        public int Experience { get; set; } = 0;                  // 경험치
    }
} 