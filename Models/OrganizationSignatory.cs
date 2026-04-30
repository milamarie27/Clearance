namespace OnlineClearanceSystem.Models
{
    public class OrganizationSignatory
    {
        public int    Id         { get; set; }
        public string OrgRole    { get; set; } = "";
        public string PersonName { get; set; } = "";
        public string Status     { get; set; } = "";
    }
}