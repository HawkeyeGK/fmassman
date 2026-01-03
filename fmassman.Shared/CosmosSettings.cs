namespace fmassman.Shared
{
    public class CosmosSettings
    {
        public string DatabaseName { get; set; } = "FMAMDB";
        public string PlayerContainer { get; set; } = "Players";
        public string RoleContainer { get; set; } = "Roles";
        public string TacticsContainer { get; set; } = "tactics";
        public string TagContainer { get; set; } = "tags";
        public string PositionContainer { get; set; } = "positions";
    }
}
