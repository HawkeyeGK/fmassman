namespace fmassman.Shared
{
    public class CosmosSettings
    {
        public string DatabaseName { get; set; } = "FMAMDB";
        public string PlayerContainer { get; set; } = "Players";
        public string RoleContainer { get; set; } = "Roles";
    }
}
